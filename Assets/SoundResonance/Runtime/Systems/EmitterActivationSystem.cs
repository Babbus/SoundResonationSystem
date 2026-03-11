using Unity.Burst;
using Unity.Entities;

namespace SoundResonance
{
    /// <summary>
    /// Consumes StrikeEvent components and activates emitters.
    ///
    /// When a StrikeEvent is enabled on an entity (by input/collision systems),
    /// this system:
    /// 1. Adds the strike force to CurrentAmplitude (additive energy model)
    /// 2. Records the strike amplitude on EmitterTag for downstream use
    /// 3. Enables EmitterTag so decay and audio systems process this entity
    /// 4. Disables StrikeEvent (consuming the one-shot event)
    ///
    /// Execution order: First in the resonance pipeline.
    /// Runs before ExponentialDecaySystem and EmitterDeactivationSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EmitterActivationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ActivateJob().ScheduleParallel();
        }

        /// <summary>
        /// Job that processes entities with an enabled StrikeEvent.
        /// The query naturally filters to only entities where StrikeEvent is enabled
        /// (IEnableableComponent default behavior).
        /// </summary>
        [BurstCompile]
        private partial struct ActivateJob : IJobEntity
        {
            private void Execute(
                ref ResonantObjectData data,
                ref EmitterTag emitter,
                in StrikeEvent strike,
                EnabledRefRW<StrikeEvent> strikeEnabled,
                EnabledRefRW<EmitterTag> emitterEnabled)
            {
                // Additive energy: multiple strikes accumulate amplitude
                data.CurrentAmplitude += strike.NormalizedForce;

                // Record strike amplitude on EmitterTag for audio bridge / downstream systems
                emitter.StrikeAmplitude = strike.NormalizedForce;

                // Enable emitter so decay and audio systems process this entity
                emitterEnabled.ValueRW = true;

                // Consume the one-shot event
                strikeEnabled.ValueRW = false;
            }
        }
    }
}
