using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SoundResonance
{
    /// <summary>
    /// MonoBehaviour bridge that collects ECS emitter data into a double-buffered
    /// NativeArray every LateUpdate frame for consumption by the audio thread.
    ///
    /// Threading model (lock-free double-buffer):
    /// - Main thread writes to buffer (_readIndex == 0 ? B : A) in LateUpdate
    /// - Audio thread reads from buffer (_readIndex == 0 ? A : B) via GetReadBuffer()
    /// - volatile _readIndex provides happens-before guarantee: main thread completes
    ///   all writes before flipping, audio thread reads _readIndex first to see completed data
    /// - No locks, mutexes, or monitors needed
    ///
    /// Execution order: LateUpdate runs after all ECS systems (SimulationSystemGroup)
    /// have completed, so CurrentAmplitude and EmitterTag states are final for this frame.
    /// </summary>
    public class ResonanceAudioBridge : MonoBehaviour
    {
        /// <summary>Maximum simultaneous voices for audio synthesis.</summary>
        public const int MaxVoices = 16;

        // Double buffer: audio thread reads one while main thread writes the other
        private NativeArray<VoiceData> _bufferA;
        private NativeArray<VoiceData> _bufferB;

        // 0 = audio reads A (main writes B), 1 = audio reads B (main writes A)
        private volatile int _readIndex;

        private VoicePool _voicePool;

        // Track entity activation across frames for IsNewStrike detection
        private HashSet<int> _previouslyActiveEntityIds;
        private HashSet<int> _currentlyActiveEntityIds;

        /// <summary>
        /// Returns the buffer the audio thread should read from.
        /// Called from OnAudioFilterRead / audio thread.
        /// </summary>
        public NativeArray<VoiceData> GetReadBuffer()
        {
            return _readIndex == 0 ? _bufferA : _bufferB;
        }

        /// <summary>Current read index for external synchronization checks.</summary>
        public int ReadIndex => _readIndex;

        private void Awake()
        {
            _bufferA = new NativeArray<VoiceData>(MaxVoices, Allocator.Persistent);
            _bufferB = new NativeArray<VoiceData>(MaxVoices, Allocator.Persistent);
            _voicePool = new VoicePool(MaxVoices);
            _previouslyActiveEntityIds = new HashSet<int>();
            _currentlyActiveEntityIds = new HashSet<int>();
        }

        private void OnDestroy()
        {
            if (_bufferA.IsCreated) _bufferA.Dispose();
            if (_bufferB.IsCreated) _bufferB.Dispose();
        }

        /// <summary>
        /// Collects active emitter data from ECS into the write buffer, then flips
        /// the read index so the audio thread sees the new data next callback.
        /// </summary>
        private void LateUpdate()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;

            // Query active emitters (EmitterTag enabled by default query filtering)
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<EmitterTag>(),
                ComponentType.ReadOnly<ResonantObjectData>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            var entities = query.ToEntityArray(Allocator.Temp);
            var dataArray = query.ToComponentDataArray<ResonantObjectData>(Allocator.Temp);
            var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Determine write buffer (opposite of read buffer)
            var writeBuffer = _readIndex == 0 ? _bufferB : _bufferA;

            // Clear all slots in write buffer
            for (int i = 0; i < MaxVoices; i++)
            {
                writeBuffer[i] = default;
            }

            // Swap tracking sets: previous = last frame's current, clear current
            var temp = _previouslyActiveEntityIds;
            _previouslyActiveEntityIds = _currentlyActiveEntityIds;
            _currentlyActiveEntityIds = temp;
            _currentlyActiveEntityIds.Clear();

            // Populate voice slots from active emitters
            int count = math.min(entities.Length, MaxVoices);
            for (int i = 0; i < count; i++)
            {
                int entityId = entities[i].Index;
                _currentlyActiveEntityIds.Add(entityId);

                int slot = _voicePool.GetOrAssign(entityId, dataArray[i].CurrentAmplitude);
                _voicePool.UpdateAmplitude(slot, dataArray[i].CurrentAmplitude);

                // Detect new activations this frame
                bool isNewThisFrame = !_previouslyActiveEntityIds.Contains(entityId);

                // Read EmitterTag to determine direct strike vs sympathetic
                var emitterTag = em.GetComponentData<EmitterTag>(entities[i]);

                // If newly appearing with StrikeAmplitude > 0: direct strike
                // If newly appearing with StrikeAmplitude ~ 0: sympathetic activation
                bool isDirectStrike = isNewThisFrame && emitterTag.StrikeAmplitude > 0.001f;
                bool isSympathetic = isNewThisFrame && !isDirectStrike;

                writeBuffer[slot] = new VoiceData
                {
                    Frequency = dataArray[i].NaturalFrequency,
                    Amplitude = dataArray[i].CurrentAmplitude,
                    StrikeAmplitude = emitterTag.StrikeAmplitude,
                    QFactor = dataArray[i].QFactor,
                    Position = transforms[i].Position,
                    Shape = dataArray[i].Shape,
                    Active = 1,
                    IsNewStrike = (byte)(isDirectStrike ? 1 : 0),
                    IsSympathetic = (byte)(isSympathetic ? 1 : 0)
                };
            }

            // Release voices for entities that disappeared this frame
            foreach (int prevId in _previouslyActiveEntityIds)
            {
                if (!_currentlyActiveEntityIds.Contains(prevId))
                {
                    int slot = _voicePool.GetAssignedSlot(prevId);
                    if (slot >= 0)
                    {
                        _voicePool.ReleaseVoice(slot);
                    }
                }
            }

            // Atomic swap: audio thread will see new data on next read
            _readIndex = _readIndex == 0 ? 1 : 0;

            // Dispose temp arrays
            entities.Dispose();
            dataArray.Dispose();
            transforms.Dispose();
        }
    }
}
