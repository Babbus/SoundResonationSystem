using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoundResonance
{
    /// <summary>
    /// Hybrid MonoBehaviour that bridges mouse input to the ECS StrikeEvent component.
    ///
    /// Architecture: This is the input side of the click-to-resonance pipeline.
    /// On left-click, it performs a Physics.Raycast to identify which resonant object
    /// was hit, looks up the corresponding ECS entity, and enables the StrikeEvent
    /// component with NormalizedForce = 1.0f. The ECS systems then take over:
    ///   EmitterActivationSystem reads the StrikeEvent -> adds energy -> enables EmitterTag
    ///   ExponentialDecaySystem decays the amplitude each frame
    ///   EmitterDeactivationSystem disables EmitterTag when amplitude falls below threshold
    ///
    /// Entity lookup: Since resonant objects live directly in the scene (no SubScene),
    /// we use a position-matching approach. When a raycast hits a collider on a GameObject
    /// with ResonantObjectAuthoring, we query all ECS entities with ResonantObjectData
    /// and LocalToWorld, then find the entity whose world position is closest to the
    /// hit object's transform position. This is O(n) where n = number of resonant objects,
    /// which is perfectly acceptable for the expected object counts.
    ///
    /// Why MonoBehaviour and not an ISystem:
    /// - Needs UnityEngine.Physics.Raycast (not available in Burst)
    /// - Needs Camera.main.ScreenPointToRay (managed API)
    /// - Needs Mouse.current from Input System (managed API)
    /// - Input bridging is inherently a managed-world concern
    /// </summary>
    public class StrikeInputManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Maximum distance for the raycast pick. Objects beyond this distance cannot be struck.")]
        private float maxRaycastDistance = 100f;

        private EntityManager _entityManager;
        private Camera _mainCamera;
        private EntityQuery _resonantQuery;
        private bool _initialized;

        private void Start()
        {
            TryInitialize();
        }

        /// <summary>
        /// Attempts to cache references to the ECS world and camera.
        /// Deferred initialization handles the case where the ECS world
        /// is not yet available on the first frame.
        /// </summary>
        private bool TryInitialize()
        {
            if (_initialized) return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;

            _entityManager = world.EntityManager;
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                Debug.LogWarning("[StrikeInputManager] No main camera found. " +
                                 "Ensure a camera is tagged 'MainCamera'.");
                return false;
            }

            // Build query for all entities that have resonant data and a world transform.
            // This query is reused every frame a click occurs.
            _resonantQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<ResonantObjectData>(),
                ComponentType.ReadWrite<StrikeEvent>(),
                ComponentType.ReadOnly<LocalToWorld>());

            _initialized = true;
            return true;
        }

        private void Update()
        {
            // Check for left mouse button press using the New Input System
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            // Ensure ECS world is ready (may not be on first frame)
            if (!_initialized && !TryInitialize()) return;

            // Verify the world is still alive (handles domain reload / play mode transitions)
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _initialized = false;
                return;
            }

            HandleClick();
        }

        /// <summary>
        /// Performs raycast from mouse position, identifies the resonant object hit,
        /// looks up the corresponding ECS entity, and enables StrikeEvent on it.
        /// </summary>
        private void HandleClick()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance)) return;

            // Check if the hit object is a resonant object
            var authoring = hit.collider.GetComponent<ResonantObjectAuthoring>();
            if (authoring == null) return;

            // Find the matching ECS entity by world position proximity
            Entity targetEntity = FindEntityByPosition(authoring.transform.position);

            if (targetEntity == Entity.Null)
            {
                Debug.LogWarning($"[StrikeInputManager] Hit '{authoring.name}' but could not find " +
                                 "matching ECS entity. Ensure the object has been baked correctly.");
                return;
            }

            // Enable StrikeEvent and set force -- the EmitterActivationSystem will pick this up
            _entityManager.SetComponentEnabled<StrikeEvent>(targetEntity, true);
            _entityManager.SetComponentData(targetEntity, new StrikeEvent
            {
                NormalizedForce = 1.0f
            });
        }

        /// <summary>
        /// Finds the ECS entity whose LocalToWorld position is closest to the given
        /// world position. Returns Entity.Null if no match is found within tolerance.
        ///
        /// Tolerance is generous (0.5 units) to account for any floating-point differences
        /// between the GameObject transform and the baked LocalToWorld component.
        /// </summary>
        private Entity FindEntityByPosition(Vector3 worldPosition)
        {
            const float toleranceSq = 0.5f * 0.5f;
            float3 targetPos = new float3(worldPosition.x, worldPosition.y, worldPosition.z);

            var entities = _resonantQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            Entity bestEntity = Entity.Null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < entities.Length; i++)
            {
                var ltw = _entityManager.GetComponentData<LocalToWorld>(entities[i]);
                float3 entityPos = ltw.Position;
                float distSq = math.distancesq(targetPos, entityPos);

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestEntity = entities[i];
                }
            }

            entities.Dispose();

            // Only return if within tolerance
            if (bestDistSq > toleranceSq)
                return Entity.Null;

            return bestEntity;
        }
    }
}
