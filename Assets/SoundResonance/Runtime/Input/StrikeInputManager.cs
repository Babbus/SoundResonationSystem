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
    /// On left-click, projects all resonant entity positions to screen space and finds
    /// the closest one to the mouse cursor. No Physics.Raycast or colliders needed —
    /// this works with SubScene-baked entities whose colliders are stripped during baking.
    ///
    /// For 2-3 objects this O(n) screen-space check is trivial. Scales fine to dozens.
    /// </summary>
    public class StrikeInputManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Maximum screen-space distance (pixels) to register a click on an entity.")]
        private float pickRadius = 80f;

        private EntityManager _entityManager;
        private Camera _mainCamera;
        private EntityQuery _resonantQuery;
        private bool _initialized;

        private void Start()
        {
            TryInitialize();
        }

        private bool TryInitialize()
        {
            if (_initialized) return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;

            _entityManager = world.EntityManager;
            _mainCamera = Camera.main;

            if (_mainCamera == null)
            {
                Debug.LogWarning("[StrikeInputManager] No main camera found.");
                return false;
            }

            _resonantQuery = _entityManager.CreateEntityQuery(
                new EntityQueryBuilder(Unity.Collections.Allocator.Temp)
                    .WithAllRW<ResonantObjectData, StrikeEvent>()
                    .WithAll<LocalToWorld>()
                    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState));

            _initialized = true;
            return true;
        }

        private void Update()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            if (!_initialized && !TryInitialize()) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _initialized = false;
                return;
            }

            HandleClick();
        }

        private void HandleClick()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            var entities = _resonantQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            if (entities.Length == 0)
            {
                Debug.LogWarning($"[StrikeInputManager] Click at {mousePos} but no resonant entities found. " +
                                 "Is the SubScene loaded and baked?");
                entities.Dispose();
                return;
            }

            Entity closest = Entity.Null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < entities.Length; i++)
            {
                var ltw = _entityManager.GetComponentData<LocalToWorld>(entities[i]);
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(ltw.Position);

                // Skip entities behind the camera
                if (screenPos.z < 0f) continue;

                float dist = Vector2.Distance(mousePos, new Vector2(screenPos.x, screenPos.y));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = entities[i];
                }
            }

            entities.Dispose();

            if (closest == Entity.Null || closestDist > pickRadius)
            {
                Debug.Log($"[StrikeInputManager] Click at {mousePos} — closest entity was {closestDist:F0}px away (limit: {pickRadius}px). {entities.Length} entities in scene.");
                return;
            }

            _entityManager.SetComponentEnabled<StrikeEvent>(closest, true);
            _entityManager.SetComponentData(closest, new StrikeEvent
            {
                NormalizedForce = 1.0f
            });

            var data = _entityManager.GetComponentData<ResonantObjectData>(closest);
            Debug.Log($"[StrikeInputManager] Struck entity! Amplitude: {data.CurrentAmplitude:F4}, Freq: {data.NaturalFrequency:F1}Hz, Q: {data.QFactor:F0}");
        }
    }
}
