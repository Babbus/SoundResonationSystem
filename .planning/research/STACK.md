# Technology Stack

**Project:** Sound Resonation System - Runtime ECS Systems, Procedural Audio, and Hybrid Bridge
**Researched:** 2026-03-11
**Scope:** Subsequent milestone stack -- covers what's needed to add runtime systems, audio synthesis, and hybrid bridge to existing physics/component layer

## Confirmed Existing Stack (Locked)

These are already in the project and locked for thesis consistency. Do not upgrade.

| Technology | Version | Purpose | Status |
|------------|---------|---------|--------|
| Unity | 6.0.3.9f1 | Engine | Locked |
| com.unity.entities | 1.3.9 | ECS framework | Locked |
| com.unity.burst | 1.8.28 (packaged as 07790c2d06d9) | Burst compiler | Locked |
| com.unity.mathematics | 1.2.1 | Math library (float, math.sin, math.exp) | Locked |
| com.unity.collections | (dependency of entities) | NativeArray, NativeList, etc. | Locked |
| com.unity.entities.graphics | 1.4.18 | Rendering integration | Locked |
| com.unity.inputsystem | 1.18.0 | Input handling | Locked |
| URP | 17.3.0 | Render pipeline | Locked |

## Recommended Stack for New Milestone

### Runtime ECS Systems

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| `ISystem` (not SystemBase) | Entities 1.3.9 | All runtime systems | Burst-compilable OnCreate/OnUpdate/OnDestroy; value-type representation; faster than SystemBase. The existing project uses unmanaged components exclusively -- ISystem is the natural fit. **Verified:** Entities 1.3 docs confirm ISystem is Burst-compatible and preferred for performance. | HIGH |
| `SystemAPI` | Entities 1.3.9 | Data access within systems | Source-generated caching of ComponentLookup, EntityQuery, etc. Eliminates manual cache boilerplate. Works in ISystem via `ref SystemState`. **Verified:** Entities 1.3 docs confirm full support in ISystem. | HIGH |
| `IJobEntity` | Entities 1.3.9 | Parallel iteration over resonant entities | Source-generated IJobChunk wrapper; reusable across systems; supports `[BurstCompile]`, `ScheduleParallel()`. Lighter compile time than Entities.ForEach (which doesn't work with ISystem anyway). **Verified:** Entities 1.3 docs confirm IJobEntity works identically in ISystem and SystemBase. | HIGH |
| `[BurstCompile]` on ISystem methods | Burst 1.8.28 | Compile OnUpdate to native code | Apply `[BurstCompile]` to the ISystem struct itself to enable Burst on OnCreate/OnUpdate/OnDestroy. Apply to IJobEntity structs for job compilation. **Verified:** Burst docs confirm attribute on struct + methods pattern. | HIGH |
| `[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]` | Burst 1.8.28 | Audio math optimization | FloatMode.Fast enables FMA (fused multiply-add) for sin/cos/exp calculations in resonance math. Acceptable for audio synthesis where exact IEEE compliance is unnecessary. Standard precision (3.5 ulp) is adequate for audible-range computation. **Verified:** Burst docs enumerate all float modes. | HIGH |
| `ComponentLookup<T>` | Entities 1.3.9 | Random-access component reads in propagation | ResonancePropagation needs to read emitter data while iterating receivers. ComponentLookup provides random-access by Entity. Use `[ReadOnly]` attribute in jobs. **Verified:** Entities 1.3 docs and system comparison examples show this pattern. | HIGH |
| `IEnableableComponent` (existing) | Entities 1.3.9 | Zero-cost event mechanism for StrikeEvent and EmitterTag | Already baked into components. Enable/disable from worker threads without ECB or sync points. Queries automatically filter by enabled state. **Verified:** Entities 1.3 enableable docs confirm no structural change, thread-safe toggle. | HIGH |

### System Group and Ordering

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| `SimulationSystemGroup` (default) | Entities 1.3.9 | Group for all resonance runtime systems | Default group; runs in Update phase of player loop. All resonance physics is simulation-tier work. **Verified:** Entities 1.3 docs list three root groups: Initialization, Simulation, Presentation. | HIGH |
| `[UpdateInGroup]` + `[UpdateBefore]`/`[UpdateAfter]` | Entities 1.3.9 | System execution ordering | Chain: EmitterActivation -> ResonancePropagation -> EmitterDeactivation. Attributes enforce order within SimulationSystemGroup. **Verified:** Entities 1.3 system-update-order docs. | HIGH |
| Custom `ResonanceSystemGroup : ComponentSystemGroup` | Entities 1.3.9 | Isolate resonance systems | Optional but recommended: groups all resonance systems into a single update group for profiling clarity and ordering control. Nest inside SimulationSystemGroup. **Verified:** Entities 1.3 docs show custom group creation pattern. | HIGH |

### Procedural Audio Synthesis

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| `MonoBehaviour.OnAudioFilterRead(float[], int)` | Unity 6 built-in | Real-time PCM sample generation | Unity's only hook for per-sample audio generation. Runs on the audio thread (NOT main thread). Receives interleaved float[] buffer and channel count. Must fill buffer within ~21ms at 48kHz (1024 samples). No Burst, no jobs -- runs in managed code on audio thread. | HIGH |
| `AudioSource` component | Unity 6 built-in (com.unity.modules.audio) | Audio output pipeline | Required companion for OnAudioFilterRead. Must be on same GameObject. Can be configured with spatialBlend for 3D positioning, no clip needed for procedural output. Already in project modules. | HIGH |
| `NativeArray<float>` with `Allocator.Persistent` | Collections (entities dependency) | Shared amplitude buffer between ECS and audio thread | Thread-safe blittable container. ECS writes amplitude/frequency/phase data; audio thread reads it. Persistent allocation survives across frames. Use `[NativeDisableParallelForRestriction]` for audio thread access. | HIGH |
| `math.sin()` from Unity.Mathematics | Mathematics 1.2.1 | Sine wave generation in audio callback | Direct sine synthesis: `sample = amplitude * math.sin(phase)`. Phase accumulates per sample: `phase += 2*PI*frequency/sampleRate`. Already used in ResonanceMath for similar calculations. | HIGH |

### Hybrid ECS-to-MonoBehaviour Bridge

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| Managed component (`class : IComponentData`) | Entities 1.3.9 | Store reference to MonoBehaviour AudioSource on entity | Managed components can hold class references (AudioSource, Transform). Cannot be used in Burst jobs or parallel code. Access via `SystemAPI.ManagedAPI.GetComponent<T>()`. **Verified:** Entities 1.3 managed-components docs confirm: no jobs, no Burst, requires parameterless constructor, GC-collected. | HIGH |
| `SystemAPI.ManagedAPI` | Entities 1.3.9 | Access managed components from ISystem | Provides GetComponent, GetSingleton, HasComponent for managed types. Enables ISystem to read managed component references on the main thread after jobs complete. **Verified:** Entities 1.3 SystemAPI docs list full ManagedAPI namespace. | HIGH |
| Main-thread sync pattern: jobs then main-thread read | Entities 1.3.9 | Bridge data flow | Pattern: (1) IJobEntity writes amplitude/phase to unmanaged ResonantObjectData in parallel, (2) main-thread foreach reads ResonantObjectData + managed AudioBridge component, (3) copies data to NativeArray shared with audio thread. This avoids managed access in jobs. | HIGH |
| `NativeArray<T>` or `SharedStatic<T>` | Collections + Burst | Thread-safe data sharing with audio thread | **Recommended: NativeArray<float> per entity** -- allocate persistent NativeArray to hold (amplitude, frequency, phase) tuple. ECS main-thread code writes to it; MonoBehaviour.OnAudioFilterRead reads from it. NativeArray is blittable and thread-safe for single-writer/single-reader without locks. SharedStatic is an alternative for global state but less flexible per-entity. **Verified:** Burst docs confirm SharedStatic for C#/HPC# sharing; Collections docs confirm NativeArray thread safety. | HIGH |

### Testing

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| `com.unity.test-framework` | 1.6.0 (already installed) | PlayMode integration tests | Existing EditMode tests use this. PlayMode tests will verify runtime systems by creating World, spawning entities, stepping simulation, asserting amplitude changes. | HIGH |
| `World.CreateSystem<T>()` + manual update | Entities 1.3.9 | Isolated system testing | Create a test World, add systems manually, call `world.Update()` to step, assert component values. Standard ECS testing pattern. | MEDIUM |

### Optional Future: FMOD Integration

| Technology | Version | Purpose | Why | Confidence |
|------------|---------|---------|-----|------------|
| FMOD for Unity | 2.02.x+ | Professional audio backend | Listed in PROJECT.md as optional. Would replace OnAudioFilterRead with FMOD programmer instruments and DSP callbacks. Defer entirely -- thesis scope is Unity AudioSource first. | LOW |

## What NOT to Use

| Technology | Why Not | Use Instead |
|------------|---------|-------------|
| `SystemBase` | Managed, GC-allocated, not Burst-compilable. Slower than ISystem. Entities.ForEach is deprecated-path. | `ISystem` for all runtime systems |
| `Entities.ForEach` | Not compatible with ISystem. Higher compile times. Legacy pattern in Entities 1.x. | `IJobEntity` or `SystemAPI.Query` foreach |
| `IJobChunk` directly | More boilerplate than IJobEntity for the same result. IJobEntity source-generates IJobChunk internally. | `IJobEntity` unless chunk-level control is needed |
| `EntityCommandBuffer` for enable/disable | Enableable components can toggle without ECB. ECBs create sync points. | `SetComponentEnabled<T>()` directly in jobs via `ComponentLookup` |
| Unity DSPGRAPH / experimental audio | Deprecated/experimental. Never left preview. Not available in Unity 6 stable. | `OnAudioFilterRead` for procedural audio |
| `AudioClip.Create` + `SetData` | Per-frame allocation, GC pressure, not real-time. Creates clips rather than streaming audio. | `OnAudioFilterRead` for continuous synthesis |
| Managed arrays in ECS components | Breaks Burst compatibility, causes GC in hot path. | `NativeArray<T>` with Allocator.Persistent |
| `SharedStatic<T>` for per-entity data | Global singleton pattern; doesn't scale to multiple resonant entities each needing their own audio state. | `NativeArray<T>` per entity, referenced from managed component |
| `SystemBase` + `Entities.ForEach` for managed access | Tempting for hybrid bridge but locks you out of Burst on the system itself. | ISystem with split pattern: Burst jobs for physics, main-thread ManagedAPI for bridge |
| `Physics.Raycast` from ECS (Unity Physics) | Would require adding com.unity.physics package. Overkill for click-to-strike. | `UnityEngine.Physics.Raycast` from MonoBehaviour input handler, then enable StrikeEvent via EntityManager |
| `[BurstCompile(FloatMode = FloatMode.Deterministic)]` | 15-30% slower. Cross-platform determinism is irrelevant for single-player thesis demo. | `FloatMode.Fast` for audio math, `FloatMode.Default` (Strict) for physics accuracy |

## Alternatives Considered

| Category | Recommended | Alternative | Why Not Alternative |
|----------|-------------|-------------|---------------------|
| System type | ISystem | SystemBase | SystemBase cannot Burst-compile OnUpdate; allocates GC; Entities.ForEach is legacy |
| Job type | IJobEntity | IJobChunk | IJobEntity generates IJobChunk internally with less boilerplate; sufficient for this project's needs |
| Audio synthesis | OnAudioFilterRead | AudioClip.Create + SetData | SetData is not real-time streaming; creates GC pressure per frame |
| Audio synthesis | OnAudioFilterRead | Unity DSPGraph | DSPGraph never left experimental; removed/unsupported in Unity 6 |
| ECS-Audio bridge | Managed component + NativeArray | Class-based singleton manager | Managed component keeps reference ownership per-entity; singleton breaks when multiple objects need audio |
| Data sharing | NativeArray (Persistent) | SharedStatic | SharedStatic is global; NativeArray is per-entity and more flexible |
| Input raycast | MonoBehaviour Physics.Raycast | ECS Unity.Physics raycast | Adding com.unity.physics package for one raycast is unnecessary dependency bloat |
| System ordering | Attribute-based ([UpdateBefore/After]) | Manual world.GetOrCreateSystem ordering | Attributes are declarative, verified at compile time, recommended by Entities docs |

## Installation

No new packages required. All recommended technologies are already available through the existing `com.unity.entities` 1.3.9, `com.unity.burst` 1.8.28, `com.unity.mathematics` 1.2.1, and `com.unity.collections` dependencies.

```
Existing manifest.json already contains everything needed.
No package additions required for this milestone.
```

The only "installation" is creating new C# files in the existing project structure:
- `Assets/SoundResonance/Runtime/Systems/` -- ISystem implementations
- `Assets/SoundResonance/Runtime/Audio/` -- OnAudioFilterRead MonoBehaviour
- `Assets/SoundResonance/Runtime/Hybrid/` -- Managed components and bridge system
- `Assets/SoundResonance/Tests/PlayMode/` -- Integration tests

## Architecture-Relevant Stack Notes

### Threading Model

```
Main Thread (ECS):
  SimulationSystemGroup
    ResonanceSystemGroup
      EmitterActivationSystem  [ISystem, BurstCompile]  -- consumes StrikeEvent, enables EmitterTag
      ResonancePropagationSystem [ISystem, BurstCompile] -- computes amplitude via Lorentzian + decay
      EmitterDeactivationSystem [ISystem, BurstCompile]  -- disables EmitterTag when amplitude < threshold
    AudioBridgeSystem [ISystem, NO Burst]  -- reads ResonantObjectData + managed AudioBridge, copies to NativeArray

Audio Thread (Unity):
  OnAudioFilterRead  -- reads NativeArray, generates sine samples
```

Key constraint: OnAudioFilterRead runs on the **audio thread**, not the main thread. All data shared between ECS (main thread) and audio (audio thread) must be through thread-safe containers (NativeArray). No locks needed for single-writer (main) / single-reader (audio) pattern with atomic-width writes (float).

### Burst Boundaries

- **Burst-compiled:** EmitterActivation, ResonancePropagation, EmitterDeactivation (IJobEntity + ISystem OnUpdate)
- **NOT Burst-compiled:** AudioBridgeSystem (accesses managed components), OnAudioFilterRead (runs in managed code on audio thread)
- **Already Burst-compiled:** ResonanceMath static methods (existing)

### Memory Model

- All resonance physics data stays in unmanaged ECS components (cache-friendly, Burst-accessible)
- Audio bridge data (amplitude, frequency, phase per entity) copied to persistent NativeArrays each frame
- Managed components hold only references (AudioSource, NativeArray pointer) -- not in hot path
- No per-frame allocations in steady state

## Sources

All findings verified against local package documentation in the project's Library/PackageCache:

- `com.unity.entities@732b1f537003/Documentation~/systems-isystem.md` -- ISystem API, Burst compatibility
- `com.unity.entities@732b1f537003/Documentation~/systems-comparison.md` -- ISystem vs SystemBase comparison matrix
- `com.unity.entities@732b1f537003/Documentation~/systems-systemapi.md` -- SystemAPI and ManagedAPI namespace
- `com.unity.entities@732b1f537003/Documentation~/systems-update-order.md` -- System groups and ordering attributes
- `com.unity.entities@732b1f537003/Documentation~/iterating-data-ijobentity.md` -- IJobEntity usage and attributes
- `com.unity.entities@732b1f537003/Documentation~/components-managed.md` -- Managed component limitations
- `com.unity.entities@732b1f537003/Documentation~/components-enableable-use.md` -- Enableable component thread safety
- `com.unity.burst@07790c2d06d9/Documentation~/compilation-burstcompile.md` -- BurstCompile attribute, FloatMode, FloatPrecision
- `com.unity.burst@07790c2d06d9/Documentation~/csharp-shared-static.md` -- SharedStatic for C#/HPC# data sharing
- `com.unity.collections@aea9d3bd5e19/Documentation~/collection-types.md` -- NativeArray, NativeList, NativeRingQueue types

**Confidence note:** WebSearch and WebFetch were unavailable during this research session. All findings are based on the official Unity package documentation bundled with the project (Entities 1.3.9, Burst 1.8.28, Collections). OnAudioFilterRead behavior is based on established Unity API knowledge (HIGH confidence -- this API has been stable since Unity 4.x and the signature/threading model has not changed). The DSPGraph deprecation claim is MEDIUM confidence -- based on training data that DSPGraph never left preview and was not included in Unity 6 stable packages; verify if this is a concern.
