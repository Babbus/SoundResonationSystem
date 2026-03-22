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
    /// PlayMode integration tests for sympathetic propagation (ECS-04).
    ///
    /// These tests verify that the SympatheticPropagationSystem correctly drives
    /// nearby receivers at matching natural frequencies via Lorentzian frequency
    /// response and inverse-square distance attenuation.
    ///
    /// Assertion strategy: monotonic/relative comparisons only -- never exact values.
    /// DeltaTime is inconsistent in test environments.
    /// </summary>
    public class SympatheticPropagationTests
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
        /// Creates a resonant entity at the given position with the specified
        /// material parameters. EmitterTag and StrikeEvent are both added disabled.
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

            _entityManager.AddComponentData(entity, new EmitterTag { StrikeAmplitude = 0f });
            _entityManager.SetComponentEnabled<EmitterTag>(entity, false);

            _entityManager.AddComponentData(entity, new StrikeEvent { NormalizedForce = 0f });
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
        // Test 1: Matched frequency receiver gains sympathetic energy
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MatchedFrequencyReceivesSympatheticEnergy()
        {
            var emitter = CreateResonantEntity(440f, 100f, new float3(0f, 0f, 0f));
            var receiver = CreateResonantEntity(440f, 100f, new float3(1f, 0f, 0f));

            Strike(emitter);
            yield return SimulateFrames(5);

            float receiverAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(receiver).CurrentAmplitude;
            Assert.That(receiverAmplitude, Is.GreaterThan(0f),
                "Receiver at matching frequency should gain amplitude from nearby emitter");
        }

        // -------------------------------------------------------------------
        // Test 2: Mismatched frequency receiver does not respond
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MismatchedFrequencyDoesNotRespond()
        {
            var emitter = CreateResonantEntity(440f, 100f, new float3(0f, 0f, 0f));
            var receiver = CreateResonantEntity(880f, 100f, new float3(1f, 0f, 0f));

            Strike(emitter);
            yield return SimulateFrames(5);

            float receiverAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(receiver).CurrentAmplitude;
            Assert.That(receiverAmplitude, Is.LessThan(0.01f),
                "Receiver at mismatched frequency should have negligible response");
        }

        // -------------------------------------------------------------------
        // Test 3: Closer receiver gets more energy than farther receiver
        // -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator CloserReceiverGetsMoreEnergy()
        {
            var emitter = CreateResonantEntity(440f, 100f, new float3(0f, 0f, 0f));
            var closeReceiver = CreateResonantEntity(440f, 100f, new float3(1f, 0f, 0f));
            var farReceiver = CreateResonantEntity(440f, 100f, new float3(5f, 0f, 0f));

            Strike(emitter);
            yield return SimulateFrames(5);

            float closeAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(closeReceiver).CurrentAmplitude;
            float farAmplitude =
                _entityManager.GetComponentData<ResonantObjectData>(farReceiver).CurrentAmplitude;

            Assert.That(closeAmplitude, Is.GreaterThan(farAmplitude),
                "Closer receiver should have more amplitude than farther receiver");
        }
    }
}
