using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;

namespace SoundResonance.Tests
{
    /// <summary>
    /// PlayMode integration tests for the single-entity ECS resonance pipeline.
    ///
    /// These tests create entities programmatically in the default World, advance
    /// the simulation via World.Update(), and verify the full lifecycle:
    /// strike activation, exponential decay, threshold deactivation, and re-excitation.
    ///
    /// Assertion strategy: monotonic decrease and relative ordering only.
    /// DeltaTime is inconsistent in test environments, so we never assert against
    /// exact exponential curve values.
    /// </summary>
    public class EmitterLifecycleTests
    {
        private World _world;
        private EntityManager _entityManager;
        private readonly List<Entity> _testEntities = new List<Entity>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            _entityManager = _world.EntityManager;
            _testEntities.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var entity in _testEntities)
            {
                if (_entityManager.Exists(entity))
                {
                    _entityManager.DestroyEntity(entity);
                }
            }

            _testEntities.Clear();
            yield return null;
        }

        /// <summary>
        /// Creates a resonant entity with the given material parameters at the origin.
        /// EmitterTag and StrikeEvent are both added disabled.
        /// LocalTransform is added at float3.zero so the entity is visible to
        /// SympatheticPropagationSystem queries.
        /// </summary>
        private Entity CreateResonantEntity(
            float naturalFrequency,
            float qFactor,
            float deactivationThreshold = 0.001f)
        {
            return CreateResonantEntity(naturalFrequency, qFactor, float3.zero, deactivationThreshold);
        }

        /// <summary>
        /// Creates a resonant entity with the given material parameters at a specific position.
        /// EmitterTag and StrikeEvent are both added disabled.
        /// </summary>
        private Entity CreateResonantEntity(
            float naturalFrequency,
            float qFactor,
            float3 position,
            float deactivationThreshold = 0.001f)
        {
            var entity = _entityManager.CreateEntity();

            _entityManager.AddComponentData(entity, new ResonantObjectData
            {
                NaturalFrequency = naturalFrequency,
                QFactor = qFactor,
                Shape = ShapeType.Plate,
                CurrentAmplitude = 0f,
                Phase = 0f,
                DeactivationThreshold = deactivationThreshold
            });

            _entityManager.AddComponentData(entity, new EmitterTag
            {
                StrikeAmplitude = 0f
            });
            _entityManager.SetComponentEnabled<EmitterTag>(entity, false);

            _entityManager.AddComponentData(entity, new StrikeEvent
            {
                NormalizedForce = 0f
            });
            _entityManager.SetComponentEnabled<StrikeEvent>(entity, false);

            _entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));

            _testEntities.Add(entity);
            return entity;
        }

        /// <summary>
        /// Enables a StrikeEvent on the given entity with the specified force.
        /// </summary>
        private void Strike(Entity entity, float normalizedForce = 1.0f)
        {
            _entityManager.SetComponentData(entity, new StrikeEvent
            {
                NormalizedForce = normalizedForce
            });
            _entityManager.SetComponentEnabled<StrikeEvent>(entity, true);
        }

        /// <summary>
        /// Advances the simulation by the given number of frames.
        /// Each frame calls World.Update() then yields to allow frame progression.
        /// </summary>
        private IEnumerator SimulateFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _world.Update();
                yield return null;
            }
        }

        // -------------------------------------------------------------------
        // Test 1: Strike activates emitter
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator StrikeActivatesEmitter()
        {
            var entity = CreateResonantEntity(440f, 1000f);

            // Verify initial state: emitter disabled, no amplitude
            Assert.That(
                _entityManager.IsComponentEnabled<EmitterTag>(entity),
                Is.False,
                "EmitterTag should start disabled");
            Assert.That(
                _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude,
                Is.EqualTo(0f),
                "CurrentAmplitude should start at zero");

            // Strike the entity
            Strike(entity, 1.0f);

            // Advance one frame to process the strike
            yield return SimulateFrames(1);

            // Verify activation
            Assert.That(
                _entityManager.IsComponentEnabled<EmitterTag>(entity),
                Is.True,
                "EmitterTag should be enabled after strike");
            Assert.That(
                _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude,
                Is.GreaterThan(0f),
                "CurrentAmplitude should be above zero after strike");
            Assert.That(
                _entityManager.IsComponentEnabled<StrikeEvent>(entity),
                Is.False,
                "StrikeEvent should be consumed (disabled) after processing");
        }

        // -------------------------------------------------------------------
        // Test 2: Amplitude decays monotonically over time
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AmplitudeDecaysOverTime()
        {
            // Q=100, f0=200Hz gives a moderate decay rate
            var entity = CreateResonantEntity(200f, 100f);

            Strike(entity);
            yield return SimulateFrames(1);

            float initialAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude;
            Assert.That(initialAmplitude, Is.GreaterThan(0f),
                "Amplitude should be positive after strike");

            // Record amplitude over several frames and verify monotonic decrease
            float previousAmplitude = initialAmplitude;
            for (int frame = 0; frame < 10; frame++)
            {
                _world.Update();
                yield return null;

                float currentAmplitude =
                    _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude;

                // Allow for deactivation (amplitude goes to zero)
                if (currentAmplitude == 0f)
                    break;

                Assert.That(currentAmplitude, Is.LessThan(previousAmplitude),
                    $"Amplitude must monotonically decrease. Frame {frame}: " +
                    $"previous={previousAmplitude}, current={currentAmplitude}");

                previousAmplitude = currentAmplitude;
            }

            // Final amplitude should be less than initial
            float finalAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude;
            Assert.That(finalAmplitude, Is.LessThan(initialAmplitude),
                "Final amplitude should be less than initial post-strike amplitude");
        }

        // -------------------------------------------------------------------
        // Test 3: Deactivation at threshold
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DeactivationAtThreshold()
        {
            // Low Q (fast decay) + high frequency = rapid amplitude loss
            // High deactivation threshold so it triggers quickly
            var entity = CreateResonantEntity(1000f, 10f, deactivationThreshold: 0.5f);

            Strike(entity);
            yield return SimulateFrames(1);

            Assert.That(
                _entityManager.IsComponentEnabled<EmitterTag>(entity),
                Is.True,
                "EmitterTag should be enabled immediately after strike");

            // Advance enough frames for amplitude to decay below threshold
            yield return SimulateFrames(30);

            // Verify deactivation
            var data = _entityManager.GetComponentData<ResonantObjectData>(entity);
            Assert.That(
                _entityManager.IsComponentEnabled<EmitterTag>(entity),
                Is.False,
                "EmitterTag should be disabled after amplitude drops below threshold");
            Assert.That(data.CurrentAmplitude, Is.EqualTo(0f),
                "CurrentAmplitude should be reset to zero on deactivation");
            Assert.That(data.Phase, Is.EqualTo(0f),
                "Phase should be reset to zero on deactivation");
        }

        // -------------------------------------------------------------------
        // Test 4: Re-excitation adds energy (additive model)
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ReExcitationAddsEnergy()
        {
            // Moderate decay so amplitude is still positive after a few frames
            var entity = CreateResonantEntity(440f, 500f);

            // First strike
            Strike(entity);
            yield return SimulateFrames(1);

            // Let it decay for a few frames
            yield return SimulateFrames(3);

            float decayedAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude;
            Assert.That(decayedAmplitude, Is.GreaterThan(0f),
                "Amplitude should still be positive after a few frames of decay");
            Assert.That(decayedAmplitude, Is.LessThan(1.0f),
                "Amplitude should have decayed below initial strike force");

            // Strike again
            Strike(entity, 1.0f);
            yield return SimulateFrames(1);

            float reExcitedAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude;

            // Amplitude should have increased from the second strike (additive)
            Assert.That(reExcitedAmplitude, Is.GreaterThan(decayedAmplitude),
                "Re-excitation should increase amplitude above decayed value");
        }

        // -------------------------------------------------------------------
        // Test 5: Re-activation after deactivation
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ReActivationAfterDeactivation()
        {
            // Fast decay to reach deactivation quickly
            var entity = CreateResonantEntity(1000f, 10f, deactivationThreshold: 0.5f);

            // First strike and wait for deactivation
            Strike(entity);
            yield return SimulateFrames(1);

            Assert.That(
                _entityManager.IsComponentEnabled<EmitterTag>(entity),
                Is.True,
                "EmitterTag should be enabled after first strike");

            // Advance until deactivated
            yield return SimulateFrames(30);

            Assert.That(
                _entityManager.IsComponentEnabled<EmitterTag>(entity),
                Is.False,
                "EmitterTag should be disabled after decay");

            // Strike again after deactivation
            Strike(entity);
            yield return SimulateFrames(1);

            // Verify re-activation
            Assert.That(
                _entityManager.IsComponentEnabled<EmitterTag>(entity),
                Is.True,
                "EmitterTag should be re-enabled after second strike");
            Assert.That(
                _entityManager.GetComponentData<ResonantObjectData>(entity).CurrentAmplitude,
                Is.GreaterThan(0f),
                "CurrentAmplitude should be above zero after re-strike");
        }

        // -------------------------------------------------------------------
        // Test 6: High-Q material retains more amplitude than low-Q
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DifferentMaterialsDifferentDecayRates()
        {
            // Steel-like: high Q = slow decay
            var steelEntity = CreateResonantEntity(440f, 10000f);
            // Wood-like: low Q = fast decay
            var woodEntity = CreateResonantEntity(440f, 100f);

            // Strike both with the same force
            Strike(steelEntity);
            Strike(woodEntity);
            yield return SimulateFrames(1);

            float steelInitial =
                _entityManager.GetComponentData<ResonantObjectData>(steelEntity).CurrentAmplitude;
            float woodInitial =
                _entityManager.GetComponentData<ResonantObjectData>(woodEntity).CurrentAmplitude;

            // Both should start at the same amplitude (same strike force)
            Assert.That(steelInitial, Is.GreaterThan(0f));
            Assert.That(woodInitial, Is.GreaterThan(0f));

            // Advance several frames
            yield return SimulateFrames(10);

            float steelAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(steelEntity).CurrentAmplitude;
            float woodAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(woodEntity).CurrentAmplitude;

            // High-Q (steel) should retain more amplitude than low-Q (wood)
            Assert.That(steelAmplitude, Is.GreaterThan(woodAmplitude),
                $"Steel (Q=10000) should retain more amplitude than wood (Q=100). " +
                $"Steel={steelAmplitude}, Wood={woodAmplitude}");
        }
    }
}
