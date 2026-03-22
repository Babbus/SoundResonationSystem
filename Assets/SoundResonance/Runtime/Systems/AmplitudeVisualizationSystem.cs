using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace SoundResonance
{
    /// <summary>
    /// Maps each entity's CurrentAmplitude to a color via URPMaterialPropertyBaseColor.
    /// Idle objects are neutral gray; vibrating objects glow from warm orange to bright
    /// red/white as amplitude increases. Runs after deactivation so the color reflects
    /// the final amplitude for the frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EmitterDeactivationSystem))]
    public partial struct AmplitudeVisualizationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new ColorJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
        private partial struct ColorJob : IJobEntity
        {
            // Neutral gray when idle
            private static readonly float4 IdleColor = new float4(0.5f, 0.5f, 0.5f, 1f);

            // Warm orange at low amplitude
            private static readonly float4 LowColor = new float4(1.0f, 0.6f, 0.1f, 1f);

            // Bright red at high amplitude
            private static readonly float4 HighColor = new float4(1.0f, 0.1f, 0.05f, 1f);

            private void Execute(
                in ResonantObjectData data,
                ref URPMaterialPropertyBaseColor color)
            {
                if (data.CurrentAmplitude <= 0f)
                {
                    color.Value = IdleColor;
                    return;
                }

                // Normalize amplitude with a saturating curve: t = 1 - exp(-amplitude * scale)
                // This maps [0, inf) to [0, 1) with most of the visual range in [0, 1] amplitude
                float t = 1f - math.exp(-data.CurrentAmplitude * 5f);

                // Two-stage lerp: idle → orange → red
                if (t < 0.5f)
                {
                    float localT = t * 2f;
                    color.Value = math.lerp(IdleColor, LowColor, localT);
                }
                else
                {
                    float localT = (t - 0.5f) * 2f;
                    color.Value = math.lerp(LowColor, HighColor, localT);
                }
            }
        }
    }
}
