using System.Collections.Generic;

namespace SoundResonance
{
    /// <summary>
    /// Manages a fixed-size pool of voice slots for audio synthesis.
    /// Assigns entities to slots persistently across frames, with amplitude-based
    /// stealing when the pool is full (lowest amplitude voice gets replaced).
    ///
    /// Uses int entity IDs (Entity.Index) rather than Entity structs so the pool
    /// can be tested in EditMode without requiring an EntityManager.
    /// </summary>
    public class VoicePool
    {
        private readonly int _poolSize;
        private readonly Dictionary<int, int> _entityIdToSlot;
        private readonly int[] _slotToEntityId;
        private readonly float[] _slotAmplitudes;

        private const int NoEntity = -1;

        /// <summary>
        /// Creates a voice pool with the specified number of slots.
        /// </summary>
        /// <param name="poolSize">Maximum number of simultaneous voices. Default 16 for production.</param>
        public VoicePool(int poolSize)
        {
            _poolSize = poolSize;
            _entityIdToSlot = new Dictionary<int, int>(poolSize);
            _slotToEntityId = new int[poolSize];
            _slotAmplitudes = new float[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                _slotToEntityId[i] = NoEntity;
            }
        }

        /// <summary>
        /// Gets an existing slot for the entity, or assigns a new one.
        /// If pool is full, steals the slot with the lowest amplitude.
        /// </summary>
        /// <param name="entityId">Entity.Index identifying the resonant object.</param>
        /// <param name="currentAmplitude">Current amplitude for stealing priority.</param>
        /// <returns>Slot index [0, poolSize).</returns>
        public int GetOrAssign(int entityId, float currentAmplitude)
        {
            // Already assigned? Return existing slot.
            if (_entityIdToSlot.TryGetValue(entityId, out int existingSlot))
            {
                return existingSlot;
            }

            // Find first free slot.
            for (int i = 0; i < _poolSize; i++)
            {
                if (_slotToEntityId[i] == NoEntity)
                {
                    AssignSlot(i, entityId, currentAmplitude);
                    return i;
                }
            }

            // Pool full: steal lowest amplitude slot.
            int minSlot = 0;
            float minAmp = _slotAmplitudes[0];
            for (int i = 1; i < _poolSize; i++)
            {
                if (_slotAmplitudes[i] < minAmp)
                {
                    minAmp = _slotAmplitudes[i];
                    minSlot = i;
                }
            }

            ReleaseVoice(minSlot);
            AssignSlot(minSlot, entityId, currentAmplitude);
            return minSlot;
        }

        /// <summary>
        /// Releases a voice slot, making it available for reassignment.
        /// </summary>
        /// <param name="slotIndex">Slot to release.</param>
        public void ReleaseVoice(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _poolSize) return;

            int entityId = _slotToEntityId[slotIndex];
            if (entityId != NoEntity)
            {
                _entityIdToSlot.Remove(entityId);
            }

            _slotToEntityId[slotIndex] = NoEntity;
            _slotAmplitudes[slotIndex] = 0f;
        }

        /// <summary>
        /// Returns the assigned slot for an entity, or -1 if not assigned.
        /// </summary>
        public int GetAssignedSlot(int entityId)
        {
            return _entityIdToSlot.TryGetValue(entityId, out int slot) ? slot : -1;
        }

        /// <summary>
        /// Updates the amplitude for a slot (used for stealing priority).
        /// </summary>
        public void UpdateAmplitude(int slotIndex, float amplitude)
        {
            if (slotIndex >= 0 && slotIndex < _poolSize)
            {
                _slotAmplitudes[slotIndex] = amplitude;
            }
        }

        /// <summary>
        /// Returns true if the slot is occupied by an entity.
        /// </summary>
        public bool IsSlotActive(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < _poolSize && _slotToEntityId[slotIndex] != NoEntity;
        }

        private void AssignSlot(int slotIndex, int entityId, float amplitude)
        {
            _slotToEntityId[slotIndex] = entityId;
            _slotAmplitudes[slotIndex] = amplitude;
            _entityIdToSlot[entityId] = slotIndex;
        }
    }
}
