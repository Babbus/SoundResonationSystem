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

        [SerializeField]
        [Tooltip("Camera movement speed in units per second.")]
        private float moveSpeed = 5f;

        private EntityManager _entityManager;
        private Camera _mainCamera;
        private EntityQuery _resonantQuery;
        private bool _initialized;

        // Entity currently being damped by right-click hold
        private Entity _dampedEntity;
        private bool _isDamping;

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

            if (!_initialized && !TryInitialize()) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                _initialized = false;
                return;
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
                HandleClick();

            if (Mouse.current.rightButton.wasPressedThisFrame)
                HandleDampStart();
            else if (Mouse.current.rightButton.wasReleasedThisFrame)
                HandleDampRelease();

            HandleCameraMovement();
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

        /// <summary>
        /// Right-click press: silence the closest entity and mark it as damped.
        /// While damped, sympathetic propagation cannot re-activate it.
        /// </summary>
        private void HandleDampStart()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            var entities = _resonantQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            if (entities.Length == 0)
            {
                entities.Dispose();
                return;
            }

            Entity closest = Entity.Null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < entities.Length; i++)
            {
                var ltw = _entityManager.GetComponentData<LocalToWorld>(entities[i]);
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(ltw.Position);

                if (screenPos.z < 0f) continue;

                float dist = Vector2.Distance(mousePos, new Vector2(screenPos.x, screenPos.y));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = entities[i];
                }
            }

            entities.Dispose();

            if (closest == Entity.Null || closestDist > pickRadius) return;

            // Zero amplitude, disable emitter, and set damped flag
            var data = _entityManager.GetComponentData<ResonantObjectData>(closest);
            data.CurrentAmplitude = 0f;
            data.Phase = 0f;
            data.Damped = true;
            _entityManager.SetComponentData(closest, data);
            _entityManager.SetComponentEnabled<EmitterTag>(closest, false);

            _dampedEntity = closest;
            _isDamping = true;

            Debug.Log($"[StrikeInputManager] Damping entity (hold to mute). Freq: {data.NaturalFrequency:F1}Hz");
        }

        /// <summary>
        /// Right-click release: remove damped flag so the entity can resonate again.
        /// </summary>
        private void HandleDampRelease()
        {
            if (!_isDamping || _dampedEntity == Entity.Null) return;

            var data = _entityManager.GetComponentData<ResonantObjectData>(_dampedEntity);
            data.Damped = false;
            _entityManager.SetComponentData(_dampedEntity, data);

            Debug.Log($"[StrikeInputManager] Released damping. Freq: {data.NaturalFrequency:F1}Hz");

            _dampedEntity = Entity.Null;
            _isDamping = false;
        }

        private void HandleCameraMovement()
        {
            if (_mainCamera == null || Keyboard.current == null) return;

            var kb = Keyboard.current;
            var dir = Vector3.zero;

            if (kb.wKey.isPressed) dir += _mainCamera.transform.forward;
            if (kb.sKey.isPressed) dir -= _mainCamera.transform.forward;
            if (kb.aKey.isPressed) dir -= _mainCamera.transform.right;
            if (kb.dKey.isPressed) dir += _mainCamera.transform.right;

            if (dir.sqrMagnitude > 0f)
                _mainCamera.transform.position += dir.normalized * moveSpeed * Time.deltaTime;
        }
    }
}
