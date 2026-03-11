using Unity.Burst;
using Unity.Entities;

namespace SoundResonance
{
    /// <summary>
    /// Disables EmitterTag when amplitude drops below the per-material deactivation threshold.
    ///
    /// Uses data.DeactivationThreshold (per-material, baked from MaterialProfileSO)
    /// rather than the global ResonanceMath.AmplitudeThreshold. This allows each material
    /// to define its own cutoff: steel rings longer (lower threshold) while rubber
    /// stops quickly (higher threshold).
    ///
    /// When deactivating, resets CurrentAmplitude and Phase to zero for a clean state
    /// on the next activation.
    ///
    /// Execution order: Last in the resonance pipeline, after ExponentialDecaySystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ExponentialDecaySystem))]
    public partial struct EmitterDeactivationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new DeactivateJob().ScheduleParallel();
        }

        /// <summary>
        /// Job that checks active emitters and disables those below threshold.
        /// Only runs on entities where EmitterTag is enabled (default query filtering).
        /// </summary>
        [BurstCompile]
        private partial struct DeactivateJob : IJobEntity
        {
            private void Execute(ref ResonantObjectData data, EnabledRefRW<EmitterTag> emitterEnabled)
            {
                if (data.CurrentAmplitude < data.DeactivationThreshold)
                {
                    // Clean reset for next activation
                    data.CurrentAmplitude = 0f;
                    data.Phase = 0f;

                    // Disable emitter — stops decay and audio processing
                    emitterEnabled.ValueRW = false;
                }
            }
        }
    }
}
