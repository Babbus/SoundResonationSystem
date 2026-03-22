using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SoundResonance
{
    /// <summary>
    /// Snapshot of an active emitter's data for use in the parallel propagation job.
    /// Collected on the main thread in Pass 1, consumed read-only by the job in Pass 2.
    /// </summary>
    public struct EmitterSnapshot
    {
        public float3 Position;
        public float NaturalFrequency;
        public float CurrentAmplitude;
    }

    /// <summary>
    /// Drives sympathetic vibration from active emitters to nearby inactive receivers.
    ///
    /// Two-pass architecture:
    ///   Pass 1 (main thread): Collect snapshots of all active emitters (enabled EmitterTag).
    ///   Pass 2 (parallel job): For each entity (including those with disabled EmitterTag),
    ///     skip active emitters (no cascade / no self-driving), accumulate driving force from
    ///     all emitter snapshots using Lorentzian frequency response and inverse-square
    ///     distance attenuation, then apply DrivenOscillatorStep to update amplitude.
    ///
    /// Execution order: After EmitterActivationSystem (so strike amplitudes are set),
    /// before ExponentialDecaySystem (so sympathetic energy is added before decay).
    ///
    /// Direct-only propagation: Active emitters are skipped in Pass 2 to prevent
    /// cascade chains (emitter A drives receiver B, which becomes active and drives C).
    /// Only directly struck emitters act as sources.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterActivationSystem))]
    [UpdateBefore(typeof(ExponentialDecaySystem))]
    public partial struct SympatheticPropagationSystem : ISystem
    {
        private EntityQuery _emitterQuery;

        public void OnCreate(ref SystemState state)
        {
            _emitterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<EmitterTag, ResonantObjectData, LocalTransform>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Pass 1: Collect active emitter snapshots on main thread
            int emitterCount = _emitterQuery.CalculateEntityCount();
            if (emitterCount == 0) return;

            var emitterData = _emitterQuery.ToComponentDataArray<ResonantObjectData>(Allocator.TempJob);
            var emitterTransforms = _emitterQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            var snapshots = new NativeArray<EmitterSnapshot>(emitterCount, Allocator.TempJob);
            for (int i = 0; i < emitterCount; i++)
            {
                snapshots[i] = new EmitterSnapshot
                {
                    Position = emitterTransforms[i].Position,
                    NaturalFrequency = emitterData[i].NaturalFrequency,
                    CurrentAmplitude = emitterData[i].CurrentAmplitude
                };
            }

            float dt = SystemAPI.Time.DeltaTime;

            // Pass 2: Schedule parallel receiver job
            state.Dependency = new PropagationJob
            {
                Emitters = snapshots,
                EmitterCount = emitterCount,
                DeltaTime = dt
            }.ScheduleParallel(state.Dependency);

            // Chain disposal after job completion
            emitterData.Dispose(state.Dependency);
            emitterTransforms.Dispose(state.Dependency);
            snapshots.Dispose(state.Dependency);
        }

        /// <summary>
        /// Parallel job that applies sympathetic driving force to each entity.
        /// Iterates ALL entities (IgnoreComponentEnabledState) so receivers with
        /// disabled EmitterTag are included. Active emitters are skipped explicitly.
        /// </summary>
        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct PropagationJob : IJobEntity
        {
            [ReadOnly] public NativeArray<EmitterSnapshot> Emitters;
            public int EmitterCount;
            public float DeltaTime;

            /// <summary>
            /// Minimum Lorentzian response to consider a frequency "matched" enough for
            /// sympathetic coupling. Rejects fat-tail responses from mismatched frequencies.
            /// At Q=100 (wood), response at octave separation ≈ 0.003 — well below this.
            /// At Q=100 (wood), response within ~4Hz of natural ≈ 0.7 — well above this.
            /// </summary>
            private const float ResponseThreshold = 0.05f;

            /// <summary>
            /// Multiplier for the drive time constant during sympathetic coupling.
            /// The physics-accurate tau for steel (Q=10000) at 440Hz is ~7.2 seconds,
            /// which is too slow for a real-time demo. This scales the effective dt
            /// so coupling builds up ~50x faster while preserving relative behavior
            /// (closer/matched still beats farther/mismatched).
            /// </summary>
            private const float PropagationTimeScale = 50f;

            private void Execute(
                ref ResonantObjectData data,
                in LocalTransform transform,
                EnabledRefRW<EmitterTag> emitterEnabled)
            {
                if (EmitterCount == 0) return;

                // Skip active emitters: direct-only propagation, no cascade
                if (emitterEnabled.ValueRO) return;

                float totalDrivingForce = 0f;
                float3 receiverPos = transform.Position;

                for (int i = 0; i < EmitterCount; i++)
                {
                    var emitter = Emitters[i];

                    // Frequency culling: skip emitters outside 2:1 ratio
                    float freqRatio = emitter.NaturalFrequency / data.NaturalFrequency;
                    if (freqRatio < 0.5f || freqRatio > 2.0f) continue;

                    // Distance
                    float dist = math.distance(receiverPos, emitter.Position);

                    // Distance culling: skip beyond 10m (squared comparison avoids sqrt)
                    if (dist * dist > 100f) continue;

                    // Lorentzian frequency response: how strongly receiver responds
                    float response = ResonanceMath.LorentzianResponse(
                        emitter.NaturalFrequency, data.NaturalFrequency, data.QFactor);

                    // Reject mismatched frequencies below threshold
                    if (response < ResponseThreshold) continue;

                    // Inverse-square distance attenuation
                    float attenuation = ResonanceMath.InverseSquareAttenuation(dist);

                    // Accumulate driving force
                    totalDrivingForce += emitter.CurrentAmplitude * response * attenuation;
                }

                if (totalDrivingForce <= 0f) return;

                // Driven oscillator step with scaled time for faster sympathetic coupling
                float newAmplitude = ResonanceMath.DrivenOscillatorStep(
                    data.CurrentAmplitude, totalDrivingForce,
                    DeltaTime * PropagationTimeScale,
                    data.NaturalFrequency, data.QFactor);

                data.CurrentAmplitude = newAmplitude;

                // Enable EmitterTag if above deactivation threshold
                if (data.CurrentAmplitude > data.DeactivationThreshold)
                {
                    emitterEnabled.ValueRW = true;
                }
            }
        }
    }
}
