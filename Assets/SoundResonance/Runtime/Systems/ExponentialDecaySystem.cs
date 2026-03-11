using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Applies exponential amplitude decay to all active emitters each frame.
    ///
    /// Uses the standard damped oscillator decay formula:
    ///   A(t+dt) = A(t) * exp(-dt * decayRate)
    /// where decayRate = omega0 / (2 * Q) = (2*pi*f0) / (2*Q) = pi*f0/Q
    ///
    /// This matches ResonanceMath.ExponentialDecay but computed inline for Burst.
    /// Higher Q-factor = slower decay (steel rings longer than rubber).
    ///
    /// Execution order: After EmitterActivationSystem, before EmitterDeactivationSystem.
    /// Only processes entities where EmitterTag is enabled.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterActivationSystem))]
    public partial struct ExponentialDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            new DecayJob { DeltaTime = dt }.ScheduleParallel();
        }

        /// <summary>
        /// Job that applies per-frame exponential decay to active emitters.
        /// Uses 'in EmitterTag' to filter to only enabled emitters without needing
        /// write access to the enable bit.
        /// </summary>
        [BurstCompile]
        private partial struct DecayJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(ref ResonantObjectData data, in EmitterTag emitter)
            {
                // Guard against invalid material data that would produce NaN
                if (data.NaturalFrequency <= 0f || data.QFactor <= 0f)
                    return;

                // decayRate = omega0 / (2*Q) where omega0 = 2*pi*f0
                // Simplifies to: pi * f0 / Q
                float decayRate = (2f * math.PI * data.NaturalFrequency) / (2f * data.QFactor);
                data.CurrentAmplitude *= math.exp(-DeltaTime * decayRate);
            }
        }
    }
}
