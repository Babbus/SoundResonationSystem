using Unity.Entities;

namespace SoundResonance
{
    /// <summary>
    /// Tag component marking an entity as an active sound emitter (currently vibrating).
    ///
    /// Uses IEnableableComponent so we can toggle emission without structural changes.
    /// Structural changes (adding/removing components) cause chunk fragmentation and
    /// sync points — expensive in ECS. IEnableableComponent stores an enable bit per
    /// entity in the chunk header, toggled with SetComponentEnabled() at zero cost.
    ///
    /// Workflow:
    /// 1. Baker adds EmitterTag disabled on all resonant objects
    /// 2. EmitterActivationSystem enables it when a StrikeEvent fires
    /// 3. ResonanceSystem only processes entities where EmitterTag is enabled
    /// 4. When amplitude decays below threshold, EmitterTag is disabled
    /// </summary>
    public struct EmitterTag : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// Amplitude at the moment the object was struck.
        /// Used as the initial value for exponential decay after driving stops.
        /// </summary>
        public float StrikeAmplitude;
    }
}
