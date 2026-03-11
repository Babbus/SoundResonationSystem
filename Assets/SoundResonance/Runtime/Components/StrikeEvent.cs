using Unity.Entities;

namespace SoundResonance
{
    /// <summary>
    /// One-shot event component: signals that this object has been struck/excited.
    ///
    /// Uses IEnableableComponent as a zero-cost event mechanism:
    /// 1. StrikeAuthoring (MonoBehaviour) enables this component on user input
    /// 2. EmitterActivationSystem consumes it: reads the force, enables EmitterTag,
    ///    sets initial amplitude, then disables StrikeEvent
    /// 3. The component stays on the entity but disabled — no structural change
    ///
    /// Why not use a buffer/event queue:
    /// For our use case (one strike at a time per object), a single enableable component
    /// is simpler and avoids the overhead of dynamic buffers. If we needed to queue
    /// multiple simultaneous strikes, we'd use DynamicBuffer&lt;StrikeEvent&gt; instead.
    /// </summary>
    public struct StrikeEvent : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// Normalized strike force [0, 1]. Determines initial vibration amplitude.
        /// 1.0 = maximum strike (like hitting a bell with a mallet).
        /// 0.1 = gentle tap.
        /// </summary>
        public float NormalizedForce;
    }
}
