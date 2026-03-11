# Phase 1: Single-Object ECS Pipeline - Research

**Researched:** 2026-03-11
**Domain:** Unity DOTS/ECS (Entities 1.3.9), exponential decay simulation, hybrid input
**Confidence:** HIGH

## Summary

Phase 1 requires building three ECS systems (activation, decay, deactivation) plus a hybrid input bridge for mouse-click raycast. The project already has substantial foundation code: ECS components (`EmitterTag`, `StrikeEvent`, `ResonantObjectData`), physics math (`ResonanceMath`, `FrequencyCalculator`, `ShapeClassifier`), authoring/baking (`ResonantObjectAuthoring`), material data (`MaterialProfileSO`, `BlittableMaterialData`), and editor tooling. What remains is the runtime systems and input handling.

The standard approach for Unity Entities 1.3.x is to use `ISystem` (not `SystemBase`) with `IJobEntity` for Burst-compiled parallel iteration, and `EnabledRefRW<T>` for toggling enableable components from within jobs. For input, the project lacks `com.unity.physics` (DOTS physics), so the recommended approach is a hybrid MonoBehaviour that performs standard `Physics.Raycast` against GameObjects kept outside the SubScene, with a MonoBehaviour-to-Entity mapping to bridge click events into ECS via `EntityManager.SetComponentEnabled`.

**Primary recommendation:** Build three `ISystem` + `IJobEntity` systems (activate, decay, deactivate) using the existing `ResonanceMath.ExponentialDecay` for per-frame decay, plus one hybrid `MonoBehaviour` for raycast input that enables `StrikeEvent` on the target entity.

## Standard Stack

### Core (Already Installed)
| Package | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| com.unity.entities | 1.3.9 | ECS runtime: systems, components, queries | Foundation of DOTS architecture |
| com.unity.entities.graphics | 1.4.18 | ECS rendering (Entities Graphics) | Renders ECS entities via URP |
| com.unity.burst | (bundled) | Native code compilation for jobs | 10-100x perf vs managed C# |
| com.unity.mathematics | (bundled) | Burst-compatible math (float3, math.*) | Required for Burst jobs |
| com.unity.collections | (bundled) | NativeArray, NativeList, etc. | Required for ECS data |
| com.unity.inputsystem | 1.18.0 | New Input System for mouse handling | Modern input API |
| com.unity.test-framework | 1.6.0 | NUnit-based test runner | EditMode + PlayMode tests |
| com.unity.render-pipelines.universal | 17.3.0 | URP rendering | Standard render pipeline |

### Supporting (Not Installed -- Recommendation)
| Package | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| com.unity.modules.physics | 1.0.0 (installed) | Built-in PhysX colliders + Physics.Raycast | Raycast click detection on GameObjects |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hybrid MonoBehaviour raycast | com.unity.physics (DOTS Physics) | Full DOTS raycasting but adds heavy package dependency, overkill for click-to-strike |
| ISystem | SystemBase | SystemBase is managed, cannot Burst-compile OnUpdate; ISystem is preferred |
| SystemAPI.Query (main thread) | IJobEntity (parallel) | SystemAPI.Query simpler for small entity counts; IJobEntity scales for N-body in Phase 2 |

**No new packages need to be installed.** The existing stack is sufficient for Phase 1. The hybrid MonoBehaviour approach for input uses `com.unity.modules.physics` (built-in PhysX) which is already present.

## Architecture Patterns

### Existing Project Structure
```
Assets/SoundResonance/
  Runtime/
    Authoring/
      ResonantObjectAuthoring.cs       # Baker -- DONE
    Components/
      EmitterTag.cs                     # IEnableableComponent -- DONE
      ResonantObjectData.cs             # Core data component -- DONE
      StrikeEvent.cs                    # IEnableableComponent -- DONE
    Physics/
      FrequencyCalculator.cs            # Frequency from geometry -- DONE
      NoteNameHelper.cs                 # Hz to note name -- DONE
      ResonanceMath.cs                  # Decay, Lorentzian, etc. -- DONE
      ShapeClassifier.cs               # Bounding box to shape -- DONE
    ScriptableObjects/
      BlittableMaterialData.cs          # Burst-safe material struct -- DONE
      MaterialDatabase.cs              # Material presets -- DONE
      MaterialProfileSO.cs             # SO for material authoring -- DONE
    Systems/                            # <<< NEW -- Phase 1 builds these
      EmitterActivationSystem.cs        # Consumes StrikeEvent, enables EmitterTag
      ExponentialDecaySystem.cs         # Per-frame amplitude decay
      EmitterDeactivationSystem.cs      # Disables EmitterTag below threshold
    Input/                              # <<< NEW -- Phase 1 builds this
      StrikeInputAuthoring.cs           # MonoBehaviour for raycast input
  Editor/
    Inspectors/
      MaterialPresetGenerator.cs        # Editor tool -- DONE
      ResonantObjectAuthoringEditor.cs  # Custom inspector -- DONE
  Tests/
    EditMode/
      FrequencyCalculatorTests.cs       # -- DONE
      ResonanceMathTests.cs             # -- DONE
      ShapeClassifierTests.cs           # -- DONE
    PlayMode/                           # <<< NEW -- Phase 1 adds system tests
```

### Pattern 1: ISystem with IJobEntity (Burst-Compiled ECS System)
**What:** Use `partial struct` implementing `ISystem` with `[BurstCompile]`, schedule work via `IJobEntity`.
**When to use:** All ECS systems in this project.
**Example:**
```csharp
// Source: Unity Entities 1.3 documentation
[BurstCompile]
public partial struct ExponentialDecaySystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        new DecayJob { DeltaTime = dt }.ScheduleParallel();
    }
}

[BurstCompile]
partial struct DecayJob : IJobEntity
{
    public float DeltaTime;

    // EnabledRefRO ensures this only runs on entities with EmitterTag ENABLED
    void Execute(ref ResonantObjectData data, EnabledRefRO<EmitterTag> emitterEnabled)
    {
        // Use existing ResonanceMath.ExponentialDecay
        float omega0 = 2f * math.PI * data.NaturalFrequency;
        float decayRate = omega0 / (2f * data.QFactor);
        data.CurrentAmplitude *= math.exp(-DeltaTime * decayRate);
    }
}
```

### Pattern 2: EnabledRefRW for Component Toggle in Jobs
**What:** Use `EnabledRefRW<T>` as an `IJobEntity.Execute` parameter to toggle enableable components from within Burst-compiled jobs, without structural changes or sync points.
**When to use:** EmitterActivationSystem (consume StrikeEvent), EmitterDeactivationSystem (disable EmitterTag).
**Constraint:** Cannot use both `EnabledRefRW<T>` and `RefRW<T>` for the same component T in the same job. If you need to both read/write component data AND toggle its enabled state, use `EnabledRefRW<T>` (which grants data access) or split into separate jobs.
**Example:**
```csharp
// Source: Unity Entities 1.3 changelog + docs
[BurstCompile]
partial struct DeactivateJob : IJobEntity
{
    // This job only iterates entities where EmitterTag is ENABLED
    // (enableable components: disabled = absent from query by default)
    void Execute(in ResonantObjectData data, EnabledRefRW<EmitterTag> emitterTag)
    {
        if (data.CurrentAmplitude < ResonanceMath.AmplitudeThreshold)
        {
            emitterTag.ValueRW = false; // Disable EmitterTag -- zero cost, no structural change
        }
    }
}
```

### Pattern 3: Hybrid MonoBehaviour Input Bridge
**What:** A MonoBehaviour (outside SubScene) handles mouse input via Physics.Raycast against standard colliders, maps the hit GameObject to an ECS Entity, and enables `StrikeEvent` on that entity via `EntityManager`.
**When to use:** Click-to-strike input (INP-01). This avoids adding the heavy `com.unity.physics` package.
**Key constraint:** Objects in SubScene have colliders stripped during baking. Two solutions:
  - **Option A (recommended for thesis):** Keep resonant GameObjects in the main scene, not a SubScene. Use `ResonantObjectAuthoring` with manual runtime conversion, or just keep standard colliders on GameObjects that also have the authoring component. The Baker still works if the objects are in a SubScene, but for click detection we need colliders.
  - **Option B (hybrid proxy):** Put resonant objects in SubScene for baking, but maintain separate collider proxy GameObjects in the main scene that store Entity references. More complex, better for production.

**Recommended approach for Phase 1:** Place resonant objects directly in the scene (not SubScene) and use a runtime baking/conversion approach. OR, use the simplest possible approach: a MonoBehaviour on each resonant GameObject that stores its baked Entity reference and provides click detection.

**Simplest practical approach:** Use `ResonantObjectAuthoring` in a SubScene for baking. For input, create a separate MonoBehaviour-based input manager that does a Physics.Raycast and finds the entity via `EntityManager` query by position or a lookup mechanism. Since test scene has only 2-3 objects, a simple approach works.

**Actually simplest (recommended):** Keep resonant objects in SubScene for proper ECS baking. The input handler MonoBehaviour does a screen-point raycast, but instead of hitting SubScene colliders (stripped), it uses a simple screen-space distance check against entity positions queried from ECS via `SystemAPI.Query`. For 2-3 objects, this is trivial.

```csharp
// Hybrid input approach -- MonoBehaviour that bridges to ECS
public class StrikeInputManager : MonoBehaviour
{
    private EntityManager entityManager;
    private Camera mainCamera;

    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // Raycast approach: query all ResonantObjectData entities,
        // project their world positions to screen space, find closest to click
        var query = entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ResonantObjectData>(),
            ComponentType.ReadOnly<Unity.Transforms.LocalToWorld>());

        // ... find closest entity to click position ...
        // entityManager.SetComponentEnabled<StrikeEvent>(closestEntity, true);
        // entityManager.SetComponentData(closestEntity, new StrikeEvent { NormalizedForce = 1.0f });
    }
}
```

### Pattern 4: System Execution Order
**What:** Control system update order using `[UpdateInGroup]`, `[UpdateBefore]`, and `[UpdateAfter]` attributes.
**When to use:** The three systems must execute in a specific order each frame:
  1. `EmitterActivationSystem` -- consume StrikeEvents first (sets amplitude)
  2. `ExponentialDecaySystem` -- apply decay to current amplitude
  3. `EmitterDeactivationSystem` -- check threshold and disable if needed

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ExponentialDecaySystem))]
public partial struct EmitterActivationSystem : ISystem { ... }

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EmitterActivationSystem))]
[UpdateBefore(typeof(EmitterDeactivationSystem))]
public partial struct ExponentialDecaySystem : ISystem { ... }

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ExponentialDecaySystem))]
public partial struct EmitterDeactivationSystem : ISystem { ... }
```

### Anti-Patterns to Avoid
- **Using SystemBase instead of ISystem:** SystemBase is managed, cannot Burst-compile OnUpdate, and `Entities.ForEach` is deprecated. Use `ISystem` + `IJobEntity`.
- **Structural changes for state toggling:** Never add/remove components to toggle active state. Use `IEnableableComponent` + `SetComponentEnabled` (already designed correctly in existing code).
- **Using `ref` and `EnabledRefRW` for the same component in one job:** This is explicitly disallowed. Split into separate jobs or use only `EnabledRefRW`.
- **Entities.ForEach:** Deprecated. Use `IJobEntity` or `SystemAPI.Query` instead.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Exponential decay math | Custom decay formula | `ResonanceMath.ExponentialDecay()` or inline `math.exp(-dt * decayRate)` | Already implemented with correct physics (omega0 / 2Q decay rate) |
| Frame-rate independent decay | Fixed timestep accumulator | `math.exp(-deltaTime * rate)` with `SystemAPI.Time.DeltaTime` | Exponential decay is inherently frame-rate independent when using `exp(-dt*rate)` |
| Event system | Custom event queue / buffer | `IEnableableComponent` toggle pattern | Already designed into `StrikeEvent` component -- zero cost, no structural change |
| Component enable/disable | Add/remove component pattern | `SetComponentEnabled` / `EnabledRefRW` | Avoids chunk fragmentation and sync points |
| Natural frequency calculation | Runtime frequency computation | Baked values from `ResonantObjectAuthoring` Baker | Already computed at edit-time via `FrequencyCalculator` |
| Material property lookup | Runtime SO lookup | Baked `QFactor` in `ResonantObjectData` | Already baked by Baker, stored directly on entity |

**Key insight:** The existing codebase already handles the hard physics and data authoring problems. Phase 1 systems are thin -- they just apply `ResonanceMath` formulas per-frame using already-baked data. The main engineering challenge is the input bridge and correct system ordering.

## Common Pitfalls

### Pitfall 1: Forgetting Per-Material Deactivation Threshold
**What goes wrong:** Using a global `AmplitudeThreshold` constant for all materials when the CONTEXT.md specifies per-material thresholds.
**Why it happens:** `ResonanceMath.AmplitudeThreshold` exists as a constant (0.0001f) which tempts developers to use it directly.
**How to avoid:** Add a `DeactivationThreshold` field to `ResonantObjectData` (or a new component), baked from material properties. The deactivation system compares `CurrentAmplitude < entity.DeactivationThreshold`.
**Warning signs:** All materials deactivate at the same amplitude level despite having different loss factors.

### Pitfall 2: Decay Applied Before Activation in Same Frame
**What goes wrong:** If decay runs before activation, a freshly struck object gets one frame of decay applied to its initial amplitude, potentially with stale data.
**Why it happens:** Default system ordering is non-deterministic. Systems may execute in any order without explicit ordering attributes.
**How to avoid:** Use `[UpdateBefore]`/`[UpdateAfter]` attributes to enforce: Activation -> Decay -> Deactivation.
**Warning signs:** Amplitude is slightly less than expected on the first frame after a strike.

### Pitfall 3: StrikeEvent Not Consumed (Re-fires Every Frame)
**What goes wrong:** The activation system reads StrikeEvent but forgets to disable it, causing the strike to re-trigger every frame.
**Why it happens:** With `IEnableableComponent`, the event stays enabled until explicitly disabled.
**How to avoid:** The activation system must disable `StrikeEvent` after processing: `strikeEnabled.ValueRW = false;`.
**Warning signs:** Amplitude grows without bound even with a single click (looks like continuous driving, not a single strike).

### Pitfall 4: Re-Excitation Overwrites Instead of Adding
**What goes wrong:** A new strike resets `CurrentAmplitude` to the strike force instead of adding to it.
**Why it happens:** Naive implementation: `data.CurrentAmplitude = strikeEvent.NormalizedForce`.
**How to avoid:** Per CONTEXT.md decision, use additive energy: `data.CurrentAmplitude += strikeEvent.NormalizedForce`. No amplitude cap.
**Warning signs:** Re-striking a ringing object causes amplitude to drop if current amplitude > strike force.

### Pitfall 5: SubScene Collider Stripping
**What goes wrong:** Objects in SubScene lose their standard colliders during baking, breaking Physics.Raycast.
**Why it happens:** Entities Graphics companion component system strips non-companion MonoBehaviours including Collider components.
**How to avoid:** Either (a) use a non-SubScene approach for test objects, (b) use DOTS Physics package, or (c) use screen-space entity picking without colliders.
**Warning signs:** Physics.Raycast returns no hits on SubScene objects in PlayMode.

### Pitfall 6: Using Managed Types in Burst-Compiled Code
**What goes wrong:** Burst compilation fails silently or throws errors when managed types (string, class references, UnityEngine.Object) are used in ISystem or IJobEntity.
**Why it happens:** Burst requires unmanaged, blittable types only.
**How to avoid:** All ECS components must be unmanaged structs (already the case). ISystem methods marked `[BurstCompile]` cannot call managed APIs.
**Warning signs:** Burst compilation warnings/errors in Console, or performance not matching expectations.

## Code Examples

### EmitterActivationSystem (Consumes StrikeEvent, Enables EmitterTag)
```csharp
// Consumes StrikeEvent and activates the emitter
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct EmitterActivationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new ActivateJob().ScheduleParallel();
    }
}

[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]  // Need this? No -- see note below
partial struct ActivateJob : IJobEntity
{
    // StrikeEvent is IEnableableComponent -- query only matches entities where it's ENABLED
    // So this job naturally only runs on entities that have been struck
    void Execute(
        ref ResonantObjectData data,
        in StrikeEvent strike,
        EnabledRefRW<StrikeEvent> strikeEnabled,
        EnabledRefRW<EmitterTag> emitterEnabled)
    {
        // Additive energy (per CONTEXT.md decision)
        data.CurrentAmplitude += strike.NormalizedForce;

        // Enable EmitterTag so decay/deactivation systems process this entity
        emitterEnabled.ValueRW = true;

        // Consume the event -- disable StrikeEvent so it doesn't fire again
        strikeEnabled.ValueRW = false;
    }
}
```

**Note on query matching:** Since `StrikeEvent` implements `IEnableableComponent`, queries naturally exclude entities where it's disabled. The `ActivateJob` only iterates entities with `StrikeEvent` enabled -- exactly the ones that were just struck. No `EntityQueryOptions.IgnoreComponentEnabledState` needed.

### ExponentialDecaySystem (Per-Frame Decay)
```csharp
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
}

[BurstCompile]
partial struct DecayJob : IJobEntity
{
    public float DeltaTime;

    // Only runs on entities where EmitterTag is ENABLED (active emitters)
    void Execute(ref ResonantObjectData data, in EmitterTag emitter)
    {
        // Inline the decay formula from ResonanceMath for Burst efficiency
        // A(t+dt) = A(t) * exp(-dt * omega0 / (2*Q))
        float omega0 = 2f * math.PI * data.NaturalFrequency;
        float decayRate = omega0 / (2f * data.QFactor);
        data.CurrentAmplitude *= math.exp(-DeltaTime * decayRate);
    }
}
```

### EmitterDeactivationSystem (Threshold Check)
```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ExponentialDecaySystem))]
public partial struct EmitterDeactivationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new DeactivateJob().ScheduleParallel();
    }
}

[BurstCompile]
partial struct DeactivateJob : IJobEntity
{
    // Only runs on entities where EmitterTag is ENABLED
    void Execute(ref ResonantObjectData data, EnabledRefRW<EmitterTag> emitterEnabled)
    {
        // Per-material threshold (see Pitfall 1 -- needs threshold field)
        if (data.CurrentAmplitude < ResonanceMath.AmplitudeThreshold)
        {
            data.CurrentAmplitude = 0f;  // Clean zero
            data.Phase = 0f;            // Reset phase for next strike
            emitterEnabled.ValueRW = false;
        }
    }
}
```

### Per-Frame Exponential Decay Formula (Physics Derivation)
```csharp
// The decay formula used in ExponentialDecaySystem:
//
// From the damped harmonic oscillator: A(t) = A0 * exp(-gamma * t)
// where gamma = omega0 / (2*Q) is the decay rate
//
// Per-frame discrete update:
//   A(t+dt) = A(t) * exp(-gamma * dt)
//
// This is INHERENTLY frame-rate independent because:
//   After N frames of dt each: A = A0 * exp(-gamma * N*dt) = A0 * exp(-gamma * T)
//   Same result regardless of frame rate, as long as total time T is the same.
//
// Example decay times (time to reach 0.0001 amplitude from 1.0):
//   Steel bar 440Hz (Q=10000): ~14.5 seconds (rings very long)
//   Glass plate 800Hz (Q=1000): ~0.73 seconds
//   Wood bar 200Hz (Q=100):    ~0.073 seconds (thuds quickly)
//
// Formula: t_threshold = -ln(threshold) / gamma = -ln(0.0001) * 2*Q / omega0
//        = 9.21 * 2*Q / (2*pi*f0) = 2.93 * Q / f0
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `SystemBase` + `Entities.ForEach` | `ISystem` + `IJobEntity` | Entities 1.0 (2023) | ForEach deprecated, IJobEntity is the replacement |
| Add/remove components for state | `IEnableableComponent` | Entities 1.0 (2023) | Zero-cost toggle, no structural changes |
| Classic MonoBehaviour conversion | SubScene Baking | Entities 1.0 (2023) | Bakers run in Editor, serialize to disk |
| SystemBase.OnUpdate (managed) | ISystem.OnUpdate [BurstCompile] | Entities 1.0 (2023) | Full Burst compilation of system update logic |

**Deprecated/outdated:**
- `Entities.ForEach`: Marked obsolete, will be removed in future major release. Use `IJobEntity` or `SystemAPI.Query`.
- `SystemBase` for new code: Still functional but `ISystem` preferred for Burst compatibility.
- `ConvertToEntity`: Removed. Use SubScene baking with Baker classes.

## Existing Code Analysis

### What's Already Built (DO NOT Rebuild)
1. **All ECS components** -- `EmitterTag`, `StrikeEvent`, `ResonantObjectData` are fully designed with correct `IEnableableComponent` usage
2. **All physics math** -- `ResonanceMath` has `ExponentialDecay`, `LorentzianResponse`, `DrivenOscillatorStep`, `AmplitudeThreshold`
3. **Shape classification + frequency calculation** -- `ShapeClassifier`, `FrequencyCalculator` with 3 geometric models (bar, plate, shell)
4. **Material system** -- `MaterialProfileSO`, `BlittableMaterialData`, `MaterialDatabase` with 10 preset materials
5. **Authoring + Baker** -- `ResonantObjectAuthoring` bakes all computed properties into ECS components
6. **Editor tooling** -- Custom inspector showing computed frequency/note, material preset generator
7. **Assembly definitions** -- Runtime, Editor, Tests.EditMode, Tests.PlayMode all configured
8. **Unit tests** -- FrequencyCalculator, ResonanceMath, ShapeClassifier tests exist

### What Needs to Be Built (Phase 1 Scope)
1. **EmitterActivationSystem** -- ISystem that consumes StrikeEvent, enables EmitterTag, adds energy
2. **ExponentialDecaySystem** -- ISystem that applies per-frame exponential decay
3. **EmitterDeactivationSystem** -- ISystem that disables EmitterTag below threshold
4. **StrikeInputManager** -- MonoBehaviour for mouse click -> StrikeEvent bridge
5. **Per-material deactivation threshold** -- Add threshold field to component or new component
6. **Test scene** -- SubScene with 2-3 objects (steel bar, glass plate, wood bar) with different materials
7. **PlayMode tests** -- Validate the full strike-activate-decay-deactivate lifecycle

### Component Modifications Needed
- `ResonantObjectData`: May need a `DeactivationThreshold` field (per-material threshold per CONTEXT.md). Currently only `ResonanceMath.AmplitudeThreshold` exists as a global constant.
- `EmitterTag.StrikeAmplitude`: Currently exists but may not be needed -- the additive energy model means we just add to `CurrentAmplitude` directly. Consider whether this field serves a purpose for re-excitation tracking.

## Open Questions

1. **Per-material deactivation threshold storage**
   - What we know: CONTEXT.md says per-material threshold. Currently `ResonanceMath.AmplitudeThreshold` is a global constant (0.0001f).
   - What's unclear: Should this be a new field on `ResonantObjectData` (baked from material), or a separate component?
   - Recommendation: Add `DeactivationThreshold` field to `ResonantObjectData`, baked from `MaterialProfileSO`. Simpler than a new component, keeps all per-entity resonance data together.

2. **Input bridge architecture (SubScene collider problem)**
   - What we know: Standard colliders are stripped from SubScene GameObjects. Project does not have `com.unity.physics`.
   - What's unclear: Best approach for thesis demo with 2-3 objects.
   - Recommendation: Use a simple screen-to-world-space approach -- the input MonoBehaviour queries ECS entities' `LocalToWorld` positions, projects them to screen space, and finds the closest entity to the mouse click. For 2-3 objects this is trivial and avoids both SubScene collider issues and adding `com.unity.physics`. Alternatively, keep test objects outside SubScene with manual entity creation (less clean but simpler).

3. **EmitterTag.StrikeAmplitude field purpose**
   - What we know: The field exists on `EmitterTag` but the additive model just adds to `CurrentAmplitude`.
   - What's unclear: Whether this field should be removed, repurposed, or kept for future use.
   - Recommendation: Keep it for now (no breaking change), but the activation system should use additive `CurrentAmplitude` per CONTEXT.md, not overwrite with `StrikeAmplitude`.

4. **Phase field reset on deactivation**
   - What we know: `ResonantObjectData.Phase` is a phase accumulator for audio sine generation (Phase 3).
   - What's unclear: Whether to reset Phase to 0 on deactivation or preserve it.
   - Recommendation: Reset to 0 on deactivation -- clean slate for next activation. Phase 3 audio will start fresh.

## Sources

### Primary (HIGH confidence)
- Unity Entities 1.3.9 package -- installed in project, `manifest.json` verified
- [ISystem overview | Entities 1.3](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/systems-isystem.html) -- ISystem API, Burst compatibility
- [SystemBase overview | Entities 1.3](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/systems-systembase.html) -- SystemBase API, not deprecated but ISystem preferred
- [Enableable components | Entities 1.0](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/components-enableable-use.html) -- SetComponentEnabled, query behavior
- [Enableable components overview | Entities 1.0](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/components-enableable-intro.html) -- IEnableableComponent concept
- [Companion components | Entities Graphics 1.4](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.4/manual/companion-components.html) -- Confirmed colliders are NOT companion components (stripped from SubScene)
- [Entities 1.3 Changelog](https://docs.unity3d.com/Packages/com.unity.entities@1.3/changelog/CHANGELOG.html) -- EnabledRefRW in IJobEntity confirmed
- Existing project source code -- all components, physics, authoring, tests reviewed

### Secondary (MEDIUM confidence)
- [IJobEntity documentation | Entities 1.0](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/iterating-data-ijobentity.html) -- Execute parameters, scheduling (verified for 1.3 via changelog)
- [Entities 1.3 changelog](https://docs.unity3d.com/Packages/com.unity.entities@1.3/changelog/CHANGELOG.html) -- EnabledRefRW/RO in IJobEntity.Execute() confirmed
- [Systems comparison | Entities 1.0](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-comparison.html) -- ISystem vs SystemBase comparison
- [Unity Discussions: enableable components event pattern](https://discussions.unity.com/t/how-to-implement-an-event-driven-pattern-on-ecs/751618) -- Community patterns for events

### Tertiary (LOW confidence)
- Screen-space entity picking approach (no official docs found -- derived from understanding of ECS queries + LocalToWorld + Camera.WorldToScreenPoint). Needs validation during implementation.
- `EnabledRefRW<T>` + `RefRW<T>` same-component constraint: found in changelog notes but specific behavior in IJobEntity needs validation during implementation.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- packages verified in project manifest, APIs verified in official docs
- Architecture (ISystem/IJobEntity patterns): HIGH -- official docs + changelog confirmed for Entities 1.3
- Architecture (input bridge): MEDIUM -- hybrid approach is well-understood but the specific screen-space picking approach for SubScene entities needs implementation validation
- Pitfalls: HIGH -- derived from official docs (enableable query semantics, companion component stripping) and code review (existing component design)
- Exponential decay formula: HIGH -- already implemented and tested in `ResonanceMath`

**Research date:** 2026-03-11
**Valid until:** 2026-04-11 (30 days -- Unity Entities 1.3 is stable release)
