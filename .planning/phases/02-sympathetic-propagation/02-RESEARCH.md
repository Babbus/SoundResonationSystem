# Phase 2: Sympathetic Propagation - Research

**Researched:** 2026-03-22
**Domain:** Unity ECS N-body pairwise frequency matching with distance attenuation
**Confidence:** HIGH

## Summary

Phase 2 implements the thesis headline feature: when a struck object vibrates, nearby objects at matching natural frequencies begin vibrating sympathetically. This is an N-body problem where every active emitter must be checked against every potential receiver for frequency match and distance attenuation.

The existing codebase already provides all the physics math needed (`ResonanceMath.LorentzianResponse`, `InverseSquareAttenuation`, `DrivenOscillatorStep`). The main engineering challenge is the ECS system design for the emitter-to-receiver pair iteration pattern, which requires a two-pass approach: first collect active emitters into a temporary NativeArray, then run a parallel job over all receivers that reads from that array.

All entities already have `ResonantObjectData` (frequency, Q-factor, amplitude), `EmitterTag` (enableable), and `LocalTransform` (position from `TransformUsageFlags.Dynamic` baking). The propagation system slots into the existing pipeline between `EmitterActivationSystem` and `ExponentialDecaySystem`.

**Primary recommendation:** Use a two-pass architecture -- main-thread EntityQuery collection of active emitters into a NativeArray, followed by a Burst-compiled parallel IJobEntity over all entities that sums driving forces from all emitters.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Use real physics (Lorentzian response + inverse-square attenuation) with a visibility floor
- Visibility floor ensures even weak sympathetic coupling produces an observable response during demos
- Multi-emitter behavior is additive (superposition principle): sum all driving forces linearly
- Two emitters at the same frequency double the driving strength on a receiver
- No amplitude cap -- matches physics superposition
- Direct only: only directly struck objects emit and drive receivers
- Sympathetically vibrating objects respond but do NOT become emitters themselves
- No cascade risk, sufficient for thesis scope
- Extend existing Phase 1 test scene (reuse TestSceneSetup.cs infrastructure)
- Tuning fork pair arrangement: two identical-material objects at same frequency placed near each other
- Plus a mismatched-frequency object as control
- Keep it minimal -- distance attenuation verified in tests only, not visual scene layout
- PlayMode tests only -- no debug logs or temporary gizmos
- Follows Phase 1 test infrastructure pattern (CreateResonantEntity + SimulateFrames helpers)
- Monotonic/relative assertion strategy (no exact values, only comparisons)

### Claude's Discretion
- Propagation system job structure (ComponentLookup vs. two-pass with NativeArray)
- Distance and frequency culling thresholds for performance
- Visibility floor implementation approach (fixed constant vs. material-derived)
- Exact object count and placement in extended test scene
- Multiple simultaneous emitter test (implement if naturally covered by architecture)

### Deferred Ideas (OUT OF SCOPE)
- Chain/cascade propagation (receivers becoming emitters)
- Debug gizmo visualization of emitter-receiver connections
- Multiple simultaneous emitter test case (not a priority)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| ECS-04 | Sympathetic propagation system computes Lorentzian frequency response between emitter-receiver pairs with distance and frequency culling | Two-pass NativeArray pattern for emitter collection + parallel receiver job using existing ResonanceMath functions; frequency/distance culling thresholds for O(N*M) performance |
</phase_requirements>

## Standard Stack

### Core (Already in Project)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Unity.Entities | 1.0.x | ECS framework -- ISystem, IJobEntity, EntityQuery | Already used by Phase 1 systems |
| Unity.Burst | 1.x | Burst compilation for math-heavy inner loop | Already used on all systems |
| Unity.Mathematics | 1.x | float3 math, math.distancesq, math.exp | Already used in ResonanceMath |
| Unity.Collections | 2.x | NativeArray for emitter data collection | Already referenced in test asmdef |
| Unity.Transforms | 1.x | LocalTransform.Position for entity positions | Already baked via TransformUsageFlags.Dynamic |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NUnit | 3.x | Test assertions | Already used in EmitterLifecycleTests |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Two-pass NativeArray | ComponentLookup per-receiver | ComponentLookup requires entity references and has random-access cache penalties; two-pass keeps emitter data contiguous for the inner loop |
| EntityQuery.ToComponentDataArray | Manual IJobChunk collection | ToComponentDataArray is simpler and sufficient for small entity counts (thesis scale) |

## Architecture Patterns

### Recommended System Placement
```
SimulationSystemGroup execution order:
  1. EmitterActivationSystem          (existing -- processes strikes)
  2. SympatheticPropagationSystem      (NEW -- drives receivers from emitters)
  3. ExponentialDecaySystem            (existing -- decays all active amplitudes)
  4. EmitterDeactivationSystem         (existing -- disables below threshold)
```

The propagation system runs AFTER activation (so newly struck emitters are active) and BEFORE decay (so sympathetically driven amplitude gets decayed in the same frame). This is critical: if propagation ran after decay, receivers would get one frame of extra amplitude.

### Pattern 1: Two-Pass Emitter Collection + Parallel Receiver Job

**What:** Collect all active emitter data (position, frequency, amplitude) into a NativeArray on the main thread, then run a parallel job over ALL entities that computes the summed driving force from all emitters.

**When to use:** When every receiver needs to check against every emitter (N-body pairwise).

**Why two-pass:** In Unity ECS, a parallel IJobEntity cannot safely iterate a second EntityQuery internally. The standard approach is to snapshot the "driving" set into a NativeArray, then pass it as `[ReadOnly]` to the receiver job. This keeps the inner loop cache-friendly (contiguous emitter data) and Burst-optimizable.

**Data structure for emitter snapshot:**
```csharp
// Burst-compatible struct for emitter data snapshot
public struct EmitterSnapshot
{
    public float3 Position;
    public float NaturalFrequency;
    public float CurrentAmplitude;
}
```

**System structure:**
```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(EmitterActivationSystem))]
[UpdateBefore(typeof(ExponentialDecaySystem))]
public partial struct SympatheticPropagationSystem : ISystem
{
    private EntityQuery _emitterQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Query for entities with enabled EmitterTag
        _emitterQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<EmitterTag, ResonantObjectData, LocalTransform>()
            .Build(ref state);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Pass 1: Collect active emitters into NativeArray (main thread)
        var emitterEntities = _emitterQuery.ToEntityArray(Allocator.TempJob);
        var emitterData = _emitterQuery.ToComponentDataArray<ResonantObjectData>(Allocator.TempJob);
        var emitterTransforms = _emitterQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

        int emitterCount = emitterEntities.Length;
        var snapshots = new NativeArray<EmitterSnapshot>(emitterCount, Allocator.TempJob);

        for (int i = 0; i < emitterCount; i++)
        {
            snapshots[i] = new EmitterSnapshot
            {
                Position = emitterTransforms[i].Position,
                NaturalFrequency = emitterData[i].NaturalFrequency,
                CurrentAmplitude = emitterData[i].CurrentAmplitude
            };
        }

        // Pass 2: Parallel job over all entities, sum driving forces
        float dt = SystemAPI.Time.DeltaTime;
        new PropagationJob
        {
            Emitters = snapshots,
            EmitterCount = emitterCount,
            DeltaTime = dt
        }.ScheduleParallel();

        // Dispose native arrays after job completes
        emitterEntities.Dispose(state.Dependency);
        emitterData.Dispose(state.Dependency);
        emitterTransforms.Dispose(state.Dependency);
        snapshots.Dispose(state.Dependency);
    }
}
```

**Receiver job structure:**
```csharp
[BurstCompile]
[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]
private partial struct PropagationJob : IJobEntity
{
    [ReadOnly] public NativeArray<EmitterSnapshot> Emitters;
    public int EmitterCount;
    public float DeltaTime;

    private void Execute(
        ref ResonantObjectData data,
        in LocalTransform transform,
        EnabledRefRW<EmitterTag> emitterEnabled)
    {
        if (EmitterCount == 0) return;

        // Skip entities that ARE active emitters (direct-only: no cascade)
        if (emitterEnabled.ValueRO) return;

        float totalDrivingForce = 0f;
        float3 receiverPos = transform.Position;

        for (int i = 0; i < EmitterCount; i++)
        {
            var emitter = Emitters[i];

            // Frequency culling: skip if too far apart in frequency
            float freqRatio = emitter.NaturalFrequency / data.NaturalFrequency;
            if (freqRatio < 0.5f || freqRatio > 2.0f) continue;

            // Distance
            float dist = math.distance(receiverPos, emitter.Position);

            // Lorentzian frequency response
            float response = ResonanceMath.LorentzianResponse(
                emitter.NaturalFrequency, data.NaturalFrequency, data.QFactor);

            // Inverse-square distance attenuation
            float attenuation = ResonanceMath.InverseSquareAttenuation(dist);

            // Driving force = emitter amplitude * frequency response * distance attenuation
            totalDrivingForce += emitter.CurrentAmplitude * response * attenuation;
        }

        if (totalDrivingForce <= 0f) return;

        // Apply visibility floor
        totalDrivingForce = math.max(totalDrivingForce, VisibilityFloor);

        // Driven oscillator step: smoothly approach target amplitude
        float newAmplitude = ResonanceMath.DrivenOscillatorStep(
            data.CurrentAmplitude, totalDrivingForce, DeltaTime,
            data.NaturalFrequency, data.QFactor);

        data.CurrentAmplitude = newAmplitude;

        // Enable EmitterTag so decay and downstream systems process this entity
        if (data.CurrentAmplitude > data.DeactivationThreshold)
            emitterEnabled.ValueRW = true;
    }
}
```

### Pattern 2: Emitter Exclusion via Skip Logic

**What:** The propagation job iterates ALL entities (including active emitters) but skips entities whose EmitterTag is already enabled. This implements "direct-only" propagation without needing a separate receiver query.

**Why:** An entity that was directly struck already has its amplitude set by `EmitterActivationSystem`. The propagation system should only drive entities that are NOT active emitters. The `emitterEnabled.ValueRO` check handles this naturally.

**Important:** This requires `[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]` on the job, since we need to iterate entities regardless of EmitterTag state, then manually check the state.

### Pattern 3: Visibility Floor

**What:** A minimum driving force that ensures even weak sympathetic coupling produces a non-zero amplitude change detectable in tests and (future) visual feedback.

**Recommendation:** Use a fixed constant rather than material-derived.

**Rationale:**
- The visibility floor is a thesis demo convenience, not a physics parameter
- Material-derived would couple it to Q-factor or loss factor, but the purpose is presentation-layer, not physics-layer
- A fixed constant of ~0.001f is simple, tunable, and clearly documented as a demo aid
- It should be a `const` in the propagation system, not baked into component data

```csharp
/// <summary>
/// Minimum driving force floor to ensure weak sympathetic responses
/// are observable in tests and future visual feedback.
/// This is a thesis demo convenience, not a physics parameter.
/// </summary>
private const float VisibilityFloor = 0.001f;
```

### Anti-Patterns to Avoid

- **Cascade propagation:** Do NOT let sympathetically vibrating receivers become emitters. The `if (emitterEnabled.ValueRO) return;` skip prevents this. Breaking this would create infinite feedback loops.
- **ComponentLookup for N-body:** Do NOT use `ComponentLookup<ResonantObjectData>` to randomly read emitter data from within the receiver job. The random access pattern defeats CPU caching. Collect into NativeArray instead.
- **Writing amplitude from multiple threads:** The two-pass pattern ensures each receiver is written by exactly one thread (IJobEntity guarantees this). Do NOT try to write emitter data from the receiver job.
- **Running propagation after decay:** Would give receivers one extra frame of un-decayed amplitude, causing visible pumping.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Frequency selectivity curve | Custom gaussian or threshold-based filter | `ResonanceMath.LorentzianResponse()` | Already implemented, physically correct Lorentzian with fat tails |
| Distance falloff | Custom linear or step function | `ResonanceMath.InverseSquareAttenuation()` | Already implemented, inverse-square is physically exact |
| Driven oscillator integration | Euler integration of amplitude | `ResonanceMath.DrivenOscillatorStep()` | Already implemented, frame-rate-independent exponential smoothing |
| Emitter collection | Manual chunk iteration | `EntityQuery.ToComponentDataArray()` | Built-in ECS API, handles chunk traversal correctly |
| Entity position access | Custom position tracking | `LocalTransform.Position` | Baked by Unity's transform system from `TransformUsageFlags.Dynamic` |

**Key insight:** The entire physics pipeline was designed in Phase 1 with sympathetic propagation in mind. `DrivenOscillatorStep` exists specifically for this use case -- it smoothly drives a receiver toward a target amplitude with frame-rate independence.

## Common Pitfalls

### Pitfall 1: Self-Driving
**What goes wrong:** An emitter drives itself sympathetically (distance = 0, perfect frequency match = response 1.0, infinite driving force).
**Why it happens:** The propagation job iterates ALL entities including emitters.
**How to avoid:** Skip entities where `emitterEnabled.ValueRO == true` (they are active emitters, not receivers).
**Warning signs:** Amplitude of struck objects increases instead of decaying after strike.

### Pitfall 2: Enableable Component Query Aliasing
**What goes wrong:** Using `in EmitterTag` and `EnabledRefRW<EmitterTag>` on the same component causes an aliasing error.
**Why it happens:** Phase 1 discovered this: `in T` (read-only) and `EnabledRefRW<T>` (write enable bit) create conflicting aliases.
**How to avoid:** Use `ref EmitterTag` if you need both data access and enable-bit write, OR only use `EnabledRefRW<EmitterTag>` and read the enable state from `emitterEnabled.ValueRO`.
**Warning signs:** Compilation error about aliasing.

### Pitfall 3: NativeArray Disposal Race
**What goes wrong:** NativeArray from `ToComponentDataArray` is disposed before the scheduled job completes.
**Why it happens:** `ScheduleParallel()` returns immediately; disposal must be deferred.
**How to avoid:** Use `nativeArray.Dispose(state.Dependency)` to chain disposal after the job completes.
**Warning signs:** "ObjectDisposedException" or "NativeArray has been deallocated" errors.

### Pitfall 4: Propagation Before Activation
**What goes wrong:** A newly struck object isn't in the emitter query because the propagation system runs before activation.
**Why it happens:** Incorrect `[UpdateAfter]` ordering.
**How to avoid:** `[UpdateAfter(typeof(EmitterActivationSystem))]` and `[UpdateBefore(typeof(ExponentialDecaySystem))]`.
**Warning signs:** Struck object doesn't drive receivers until the next frame.

### Pitfall 5: Visibility Floor Applied to Non-Driven Receivers
**What goes wrong:** The visibility floor causes ALL receivers to gain amplitude even when totalDrivingForce is zero.
**Why it happens:** Applying `math.max(force, floor)` before checking if force > 0.
**How to avoid:** Only apply visibility floor when `totalDrivingForce > 0` (at least one emitter contributed).
**Warning signs:** All objects in the scene slowly gain amplitude without being struck.

### Pitfall 6: IgnoreComponentEnabledState Missing
**What goes wrong:** The propagation job only processes entities with ENABLED EmitterTag, missing all potential receivers (which have disabled EmitterTag).
**Why it happens:** Default ECS query filtering skips disabled enableable components.
**How to avoid:** `[WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)]` on the job struct, then manual state checks.
**Warning signs:** No sympathetic response at all -- receivers are never iterated.

## Code Examples

### Complete Emitter Snapshot Struct
```csharp
// Source: Custom design for this phase
using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Lightweight snapshot of active emitter data for the propagation inner loop.
    /// Collected once per frame on the main thread, consumed by parallel receiver jobs.
    /// </summary>
    public struct EmitterSnapshot
    {
        public float3 Position;
        public float NaturalFrequency;
        public float CurrentAmplitude;
    }
}
```

### Test Helper: CreateResonantEntity with Position
```csharp
// Extends existing Phase 1 test pattern with LocalTransform for position
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

    // Add LocalTransform for position (needed by propagation system)
    _entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));

    _testEntities.Add(entity);
    return entity;
}
```

### Test: Matched Frequency Responds
```csharp
[UnityTest]
public IEnumerator MatchedFrequencyReceivesSympatheticEnergy()
{
    // Emitter and receiver at same frequency, close together
    var emitter = CreateResonantEntity(440f, 100f, new float3(0f, 0f, 0f));
    var receiver = CreateResonantEntity(440f, 100f, new float3(1f, 0f, 0f));

    // Strike the emitter only
    Strike(emitter);
    yield return SimulateFrames(5);

    // Receiver should have gained amplitude sympathetically
    float receiverAmplitude =
        _entityManager.GetComponentData<ResonantObjectData>(receiver).CurrentAmplitude;
    Assert.That(receiverAmplitude, Is.GreaterThan(0f),
        "Receiver at matching frequency should gain amplitude from nearby emitter");
}
```

### Test: Mismatched Frequency Rejected
```csharp
[UnityTest]
public IEnumerator MismatchedFrequencyDoesNotRespond()
{
    // Emitter at 440Hz, receiver at 880Hz (octave apart)
    var emitter = CreateResonantEntity(440f, 100f, new float3(0f, 0f, 0f));
    var receiver = CreateResonantEntity(880f, 100f, new float3(1f, 0f, 0f));

    Strike(emitter);
    yield return SimulateFrames(5);

    float receiverAmplitude =
        _entityManager.GetComponentData<ResonantObjectData>(receiver).CurrentAmplitude;
    // With Q=100, Lorentzian response at r=0.5 is extremely small
    // Receiver amplitude should be near zero (below visibility floor threshold)
    Assert.That(receiverAmplitude, Is.LessThan(0.01f),
        "Receiver at mismatched frequency should have negligible response");
}
```

### Test: Distance Attenuation
```csharp
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
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Entities.ForEach` | `IJobEntity` + `SystemAPI.Query` | Entities 1.0 | 4x faster compilation; project already uses IJobEntity |
| `ComponentDataFromEntity` | `ComponentLookup` | Entities 1.0 | Renamed API; same functionality |
| `Translation` component | `LocalTransform` | Entities 1.0 | Unified transform struct with Position, Rotation, Scale |
| `LocalToWorldSystem` separate | Built into transform hierarchy | Entities 1.0 | `LocalTransform.Position` sufficient when no parent hierarchy |

**Relevant to this phase:**
- Entities baked with `TransformUsageFlags.Dynamic` get `LocalTransform` component automatically
- `LocalTransform.Position` is a `float3` -- directly usable in `math.distance()` calls
- `EntityQuery.ToComponentDataArray<T>(Allocator)` is the standard way to snapshot component data for cross-entity reads

## Open Questions

1. **Visibility floor value tuning**
   - What we know: Floor must be above `DeactivationThreshold` (0.001f default) to prevent immediate deactivation, but small enough not to be physically unrealistic
   - What's unclear: Exact value that looks good in Phase 4 visual feedback
   - Recommendation: Start with 0.001f (matches AmplitudeThreshold), adjust if needed in Phase 4. The floor only affects the minimum observable response, not the physics.

2. **Frequency culling threshold**
   - What we know: Lorentzian response drops off rapidly. For Q=100, response at r=0.5 or r=2.0 is ~0.0003 (negligible). For Q=10000, it's even smaller.
   - What's unclear: Whether 0.5-2.0 ratio range is tight enough for performance at scale
   - Recommendation: Use 0.5-2.0 octave range initially (covers any plausible sympathetic coupling). This is generous; in practice, high-Q materials only respond to nearly-exact matches.

3. **Distance culling threshold**
   - What we know: Inverse-square drops to 0.01 at 10x reference distance (10m)
   - What's unclear: Maximum scene scale for thesis demo
   - Recommendation: Use `math.distancesq` with a squared max distance threshold (e.g., 100f for 10m) to avoid the sqrt. Skip pairs beyond this distance entirely.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | NUnit 3.x via Unity Test Runner |
| Config file | `Assets/SoundResonance/Tests/PlayMode/SoundResonance.Tests.PlayMode.asmdef` |
| Quick run command | Unity Test Runner > PlayMode > SoundResonance.Tests namespace |
| Full suite command | Unity Test Runner > Run All (EditMode + PlayMode) |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | File Exists? |
|--------|----------|-----------|-------------|
| ECS-04a | Matched frequency receiver gains amplitude | PlayMode integration | No -- Wave 0 |
| ECS-04b | Mismatched frequency receiver stays near zero | PlayMode integration | No -- Wave 0 |
| ECS-04c | Closer receiver gets more amplitude than farther | PlayMode integration | No -- Wave 0 |
| ECS-04d | Multiple simultaneous emitters drive independently | PlayMode integration | No -- Wave 0 (if naturally covered) |

### Sampling Rate
- **Per task commit:** Run propagation-specific PlayMode tests
- **Per wave merge:** Full PlayMode + EditMode suite
- **Phase gate:** All ECS-04 tests green before verify

### Wave 0 Gaps
- [ ] `Assets/SoundResonance/Tests/PlayMode/SympatheticPropagationTests.cs` -- covers ECS-04a/b/c/d
- [ ] Update `CreateResonantEntity` helper to accept `float3 position` parameter (or create overload)
- [ ] Existing `EmitterLifecycleTests.cs` must still pass (regression)

## Sources

### Primary (HIGH confidence)
- Project codebase: `ResonanceMath.cs`, `EmitterActivationSystem.cs`, `ExponentialDecaySystem.cs`, `EmitterDeactivationSystem.cs`, `EmitterLifecycleTests.cs` -- verified current implementations and patterns
- [Unity Entities 1.0 - Look up arbitrary data](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-looking-up-data.html) -- ComponentLookup usage and ReadOnly requirement for parallel jobs
- [Unity Entities 1.0 - LocalTransform](https://docs.unity3d.com/Packages/com.unity.entities@1.0/api/Unity.Transforms.LocalTransform.html) -- Position field on baked entities
- [Unity Entities 1.0 - IJobEntity](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/iterating-data-ijobentity.html) -- Job scheduling and parallel execution

### Secondary (MEDIUM confidence)
- [Unity Discussions - ComponentLookup in parallel IJobEntity](https://discussions.unity.com/t/when-is-it-safe-to-use-componentlookup-in-a-parallel-ijobentity-to-modify-the-value-of-a-component/1522884) -- NativeDisableParallelForRestriction safety guidelines
- [Unity Discussions - Access component of another entity in IJobEntity](https://discussions.unity.com/t/how-to-access-the-component-of-another-entity-in-ijobentity/915392) -- Confirmed two-pass as community pattern
- [EntityComponentSystemSamples - entities-jobs.md](https://github.com/Unity-Technologies/EntityComponentSystemSamples/blob/master/EntitiesSamples/Docs/entities-jobs.md) -- Official sample patterns for job scheduling

### Tertiary (LOW confidence)
- Visibility floor value (0.001f) -- educated guess based on AmplitudeThreshold; may need tuning in Phase 4
- Frequency culling range (0.5-2.0 ratio) -- physics-informed estimate, not tested at scale

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - all libraries already in project, no new dependencies needed
- Architecture: HIGH - two-pass pattern is well-documented in Unity ECS; all physics functions already exist
- Pitfalls: HIGH - Phase 1 encountered and resolved most ECS enableable-component gotchas; same patterns apply

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable -- no dependencies on fast-moving libraries)
