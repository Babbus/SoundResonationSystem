# Project Research Summary

**Project:** Sound Resonation System — Runtime ECS Systems, Procedural Audio, and Hybrid Bridge
**Domain:** Unity DOTS real-time sympathetic resonance simulation with physics-derived procedural audio
**Researched:** 2026-03-11
**Confidence:** HIGH (stack verified against installed package docs; architecture and pitfalls are HIGH-confidence established patterns)

## Executive Summary

This project is a Unity DOTS physics simulation where material properties (Young's modulus, density, damping ratio) determine how objects vibrate and resonate sympathetically. The hard work — resonance math, component design, baking pipeline, editor tools — is complete. What remains is the runtime layer: ECS systems that animate the physics per frame, a thread-safe bridge from ECS to Unity's audio subsystem, and a procedural synthesizer that turns amplitude/frequency data into PCM audio. All required packages are already installed. No new dependencies are needed. The implementation is primarily file creation and plumbing.

The recommended approach is a strict three-layer runtime architecture. Layer one is Burst-compiled ISystem structs (EmitterActivationSystem, ResonanceDecaySystem, ResonancePropagationSystem) running in SimulationSystemGroup that operate entirely on unmanaged components. Layer two is a single MonoBehaviour bridge (ResonanceAudioBridge) that runs in LateUpdate, after all ECS writes are complete, and copies a snapshot of active emitter state into a persistent NativeArray. Layer three is OnAudioFilterRead on the audio thread, which reads only from that NativeArray and generates waveforms at sample rate (48kHz), maintaining its own per-voice phase accumulators independently of ECS. This separation is non-negotiable — it resolves the five most critical audio pitfalls simultaneously.

The primary risks are all well-understood and preventable. The audio thread safety problem (reading ECS data from OnAudioFilterRead) causes intermittent crashes and is addressed by the shared-buffer pattern. The quadratic N-by-N propagation cost is addressed by culling inactive emitters and applying distance thresholds before computing Lorentzian response. Phase discontinuity clicks are prevented by never resetting the phase accumulator on re-strike and by having the audio callback maintain its own sample-rate phase, not consuming ECS frame-rate phase directly. All other pitfalls are detected and corrected during early implementation if Burst strict mode is enabled from day one.

## Key Findings

### Recommended Stack

No package additions are required. The entire runtime stack (ISystem, IJobEntity, SystemAPI, BurstCompile, NativeArray, OnAudioFilterRead, com.unity.test-framework) is available through the already-installed Entities 1.3.9, Burst 1.8.28, Mathematics 1.2.1, and Collections packages. The only work is creating new .cs files in directories that already exist in the project structure (`Runtime/Systems/`, `Runtime/Audio/`, `Runtime/Hybrid/`).

**Core technologies:**
- `ISystem` + `[BurstCompile]`: All runtime systems — value-type struct systems compile to native code via Burst, no GC overhead, preferred over SystemBase for this project's unmanaged-component-only design
- `IJobEntity` + `ScheduleParallel()`: Parallel entity iteration — source-generated, simpler than IJobChunk, compatible with [BurstCompile], appropriate for propagation's receiver-side work
- `SystemAPI.Query` + `EnabledRefRW<T>`: Inline main-thread iteration with enableable-component toggling — no sync points, no structural changes, correct filter semantics via `.WithAll<T>()`
- `OnAudioFilterRead(float[], int)`: Real-time PCM synthesis hook — runs on audio thread, only path for continuous procedural audio in Unity 6, requires zero allocations inside the callback
- `NativeArray<float>` (Allocator.Persistent): ECS-to-audio-thread data bridge — blittable, thread-safe for single-writer/single-reader, avoids managed allocation in hot path
- `[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]`: Audio math optimization — enables FMA for sin/cos/exp in resonance calculations, acceptable accuracy for audible-range synthesis
- Managed component (`class : IComponentData`): AudioSource reference storage on entity — necessary to hold MonoBehaviour references per-entity, accessed via `SystemAPI.ManagedAPI` on main thread only, never in jobs

**Do not use:** SystemBase, Entities.ForEach, Unity DSPGraph (discontinued/experimental), AudioClip.Create+SetData, SharedStatic for per-entity audio state, EntityCommandBuffer for enable/disable of IEnableableComponent.

### Expected Features

The feature dependency chain is linear: Strike Input enables EmitterTag, Decay updates CurrentAmplitude and Phase, Audio Bridge copies snapshot to NativeArray, Synthesizer generates PCM. Propagation is parallel to the audio path — it updates amplitudes in ECS and those amplitudes flow into audio naturally. Visual feedback is independent of audio and can be developed concurrently once CurrentAmplitude is being written.

**Must have (table stakes) — estimated 3-4 days:**
- Strike Input via Raycast — thesis reviewer must be able to excite objects; MonoBehaviour raycast enables StrikeEvent on hit entity via EntityManager
- Emitter Activation System — consumes StrikeEvent, enables EmitterTag, sets initial amplitude; straightforward ISystem
- Exponential Decay System + Phase Accumulation — objects ring and decay; uses existing `ResonanceMath.ExponentialDecay()`, passes SystemAPI.Time.DeltaTime
- Emitter Deactivation — disables EmitterTag when CurrentAmplitude < AmplitudeThreshold; part of decay system, one conditional
- ECS-to-MonoBehaviour Audio Bridge — thread-safe data transfer via NativeArray snapshot in LateUpdate; the architectural load-bearing component
- Procedural Audio Synthesis (single sine) — OnAudioFilterRead reads snapshot, generates sine at sample rate with local phase accumulator
- Distance-Based Attenuation — InverseSquareAttenuation() already exists; apply during propagation
- Multiple Simultaneous Emitters — additive mixing in single OnAudioFilterRead; requires voice count management

**Should have (differentiators) — estimated 2-3 additional days:**
- Sympathetic Resonance Propagation — the thesis headline; each active emitter drives receivers via DrivenOscillatorStep + LorentzianResponse, culled by distance and frequency mismatch
- Visual Amplitude Feedback — scale or tint driven by CurrentAmplitude; low complexity, high thesis reviewer impact
- Multi-Harmonic Audio — 2-3 overtones at integer multiples of f0 with 1/n amplitude rolloff; transforms "electronic" sine into realistic material sound
- Real-Time Parameter Display (HUD) — frequency, amplitude, Q-factor overlay; proves physics are live, not pre-baked

**Defer to post-MVP:**
- Resonance Chain Reactions — emerges from correct propagation but needs energy conservation safeguards; validate two-body first
- Performance Profiling Dashboard — build when writing the performance chapter
- Material Comparison Scene — scene design task, not engineering; build when preparing thesis demonstration
- FMOD integration — not in thesis scope; mention as future work

**Anti-features (deliberately excluded):** Full modal FEA from mesh geometry, FMOD/Wwise middleware, spatial HRTF audio, FFT spectral analysis at runtime, designer-tunable artistic parameters, continuous driving forces (bowing/wind), runtime material creation, networked multiplayer.

### Architecture Approach

The runtime architecture adds three new layers on top of the existing five-layer stack (Data -> Physics -> Components -> Authoring -> Editor). These three new layers operate across distinct execution contexts with incompatible threading constraints: Burst-compiled ISystem jobs on worker threads (ECS), a managed MonoBehaviour on the main thread (bridge), and the Unity audio callback on a dedicated audio thread (synthesis). The key design insight is that these three contexts cannot share data directly — only through thread-safe NativeArrays written by exactly one context and read by one other.

**Major components:**

1. **StrikeInputSystem** (ISystem, main thread) — Raycast from camera to physics collider, enable StrikeEvent with normalized force on hit entity; requires main thread for Physics.Raycast, no Burst
2. **EmitterActivationSystem** (ISystem, [BurstCompile]) — Queries entities with enabled StrikeEvent, copies force to EmitterTag.StrikeAmplitude, enables EmitterTag, disables StrikeEvent; uses EnabledRefRW to avoid sync points
3. **ResonanceDecaySystem** (ISystem, [BurstCompile]) — Per frame for all enabled EmitterTag entities: apply ExponentialDecay to CurrentAmplitude, accumulate Phase (mod 2PI), disable EmitterTag when amplitude below threshold; all math via ResonanceMath
4. **ResonancePropagationSystem** (ISystem, [BurstCompile]) — Outer loop: active emitters; inner loop: all receivers, culled by distance then LorentzianResponse threshold; calls DrivenOscillatorStep to update receiver amplitude; O(E x R) with distance + frequency culling bringing effective cost to O(E x K)
5. **ResonanceAudioBridge** (MonoBehaviour, LateUpdate) — Queries all enabled emitter entities after ECS systems complete, copies (NaturalFrequency, CurrentAmplitude, Phase) per entity into a persistent NativeArray of EmitterSnapshot structs, updates active count; sole writer to shared buffer
6. **ResonanceSynthesizer** (OnAudioFilterRead, audio thread) — Reads EmitterSnapshot NativeArray, maintains local per-voice phase accumulators advancing at 2*PI*freq/sampleRate per sample, sums all voices with linear amplitude ramp to prevent zipper noise, clamps to [-1, 1]

**System execution order within SimulationSystemGroup:**
`StrikeInputSystem` -> `EmitterActivationSystem` -> `ResonanceDecaySystem` -> `ResonancePropagationSystem`
Then (outside ECS): `ResonanceAudioBridge.LateUpdate()` -> `OnAudioFilterRead` (audio thread, asynchronous)

**File layout** (all directories already exist):
- `Runtime/Systems/` — StrikeInputSystem, EmitterActivationSystem, ResonanceDecaySystem, ResonancePropagationSystem
- `Runtime/Hybrid/` — ResonanceAudioBridge (MonoBehaviour + managed component definitions)
- `Runtime/Audio/` — EmitterSnapshot struct, ResonanceSynthesizer, VoiceManager (Phase 4)
- `Tests/PlayMode/` — integration tests using ECSTestsFixture

### Critical Pitfalls

1. **Audio thread reads ECS data directly** — OnAudioFilterRead runs concurrently with ECS world updates on a separate thread; EntityManager access from audio thread causes crashes. Prevention: bridge copies to NativeArray in LateUpdate; audio thread reads only from that buffer, never from ECS.

2. **N x N propagation quadratic explosion** — naive nested iteration over all emitter-receiver pairs is O(N^2); with 50 entities that is 2,500 pairs per frame. Prevention: outer loop queries only active emitters (EmitterTag enabled); inner loop culls by InverseSquareAttenuation distance first, then by LorentzianResponse < threshold before running DrivenOscillatorStep.

3. **Sync points from EntityManager.SetComponentEnabled** — calling the managed SetComponentEnabled inside an ISystem creates a sync point that serializes all running jobs. Prevention: use EnabledRefRW<T> accessed through SystemAPI.Query or IJobEntity — these toggle the chunk enable bit without structural changes or sync points.

4. **Phase discontinuity clicks** — resetting Phase to 0 on re-strike, or using the ECS frame-rate phase directly in audio synthesis, produces waveform discontinuities that are audible as clicks/pops. Prevention: never reset Phase; audio callback maintains its own per-sample phase accumulator at 48kHz using snapshot frequency; ECS phase is amplitude-envelope authority only.

5. **Managed type capture in Burst jobs** — any string, class reference, or delegate in an ISystem struct or IJobEntity causes silent Burst fallback to 10-100x slower managed execution. Prevention: enable Burst strict mode from project start so violations are compile errors; use only NativeArray/NativeList/FixedString in job contexts.

## Implications for Roadmap

All four research files agree on the same build dependency order. There are no conflicts between STACK.md, FEATURES.md, ARCHITECTURE.md, and PITFALLS.md — they independently converge on the same four-phase structure. The rationale is strict dependency: each phase produces the data and system state the next phase requires to be testable.

### Phase 1: Core ECS Runtime Systems

**Rationale:** ECS systems have no dependency on audio. They can be implemented, unit-tested, and validated before any audio code exists. Activation must exist before decay (nothing to decay). Decay must produce visible CurrentAmplitude changes before the bridge has anything to copy. Strike input last because activation and decay must be working to produce observable results of striking. This is the lowest-risk phase — all patterns are established in the existing codebase.

**Delivers:** Working physics simulation. Strike an entity, watch CurrentAmplitude evolve over time and decay to zero. Completely verifiable without audio.

**Implements:** StrikeInputSystem, EmitterActivationSystem, ResonanceDecaySystem (with Emitter Deactivation inline)

**Features addressed (from FEATURES.md):** Strike Input, Emitter Activation, Exponential Decay, Emitter Deactivation, Phase Accumulation, Frame-Rate Independence

**Pitfalls to avoid:** Sync points via EntityManager.SetComponentEnabled (#3); frame-rate-dependent decay via constant multiplier (#7); EnableableComponent query confusion (#10); managed types in Burst jobs (#6)

**Research flag:** Standard patterns — ISystem, IJobEntity, IEnableableComponent are well-documented in the installed Entities 1.3.9 package. No additional research phase needed; verify specific API signatures against `Library/PackageCache/com.unity.entities@732b1f537003/Documentation~` during implementation.

### Phase 2: Sympathetic Resonance Propagation

**Rationale:** Propagation is the thesis headline feature. It must be built after the activation and decay systems are verified, because propagation reads the output of those systems (active emitters with non-zero amplitude) and writes back to receiver amplitudes, which are then consumed by decay. Building propagation before decay is verified would make propagation behavior untestable. Propagation is also the highest-complexity system and benefits from all surrounding infrastructure being stable.

**Delivers:** The core thesis demonstration — striking object A causes object B at a matching frequency to begin vibrating sympathetically.

**Implements:** ResonancePropagationSystem with distance culling + Lorentzian frequency matching

**Features addressed (from FEATURES.md):** Sympathetic Resonance Propagation, Distance-Based Attenuation, Multiple Simultaneous Emitters, Resonance Chain Reactions (emerges from correct implementation)

**Pitfalls to avoid:** N x N quadratic explosion (#2); phase discontinuities when receivers start vibrating (#4); EnableableComponent query confusion for active-emitter filtering (#10)

**Research flag:** Needs attention during planning. The propagation system design (how to structure the outer/inner loop, how to access receiver components from the emitter iteration, whether to use ComponentLookup or restructure as a two-pass job) requires explicit design decisions before implementation. Consider `/gsd:research-phase` or a design spike before coding.

### Phase 3: Hybrid Bridge and Audio Synthesis

**Rationale:** The audio bridge can only be built after ECS systems are writing amplitude data (Phases 1-2). The synthesizer can only be built after the bridge exists. This phase introduces the audio thread boundary — the project's most dangerous pitfall zone. The single-AudioSource additive mixer pattern is strongly recommended over per-entity AudioSources; the architecture decision must be made before any audio code is written because it shapes all downstream audio implementation.

**Delivers:** Audible sine-wave output corresponding to struck and resonating objects. End-to-end observable thesis result: click object, hear tone, watch it decay.

**Implements:** ResonanceAudioBridge (MonoBehaviour), EmitterSnapshot struct, ResonanceSynthesizer (OnAudioFilterRead with per-voice local phase accumulators and linear amplitude ramping)

**Features addressed (from FEATURES.md):** ECS-to-MonoBehaviour Audio Bridge, Procedural Audio Synthesis (single sine), voice mixing for Multiple Simultaneous Emitters

**Pitfalls to avoid:** Audio thread reads ECS data directly (#1); Entity-to-AudioSource lifecycle nightmare (#9); sample rate mismatch between ECS and audio callback (#8); phase discontinuity clicks (#4); allocations inside OnAudioFilterRead (#5 variant); MonoBehaviour reading incomplete jobs (#5)

**Research flag:** Needs careful design documentation before implementation. The bridge/synthesizer split and the exact threading contract (what is written where and when) should be sketched in pseudocode and reviewed before any code is written. The audio thread safety contract is easy to violate subtly.

### Phase 4: Polish, Multi-Harmonic Audio, and Integration Testing

**Rationale:** After the end-to-end loop works (strike -> ECS decay -> bridge -> sine output), enhancement features become low-risk additions. Multi-harmonic audio (overtones) is additive on top of the synthesizer. Visual feedback is an independent reader of CurrentAmplitude. Integration testing validates the full chain. This phase also includes voice management (limiting, priority, gain normalization) which is easier to implement correctly once the base mixer is running.

**Delivers:** A polished thesis demonstration. Realistic harmonic timbres, visual amplitude feedback, HUD parameter display, and a pre-built Material Comparison scene showing Steel vs. Glass vs. Wood vs. Rubber behavior.

**Implements:** Multi-Harmonic Audio (2-3 overtones per voice), Visual Amplitude Feedback, Real-Time Parameter Display (HUD), VoiceManager (voice limiting, amplitude normalization), end-to-end integration tests, Material Comparison Scene (scene design)

**Features addressed (from FEATURES.md):** Multi-Harmonic Audio, Visual Amplitude Feedback, Real-Time Parameter Display, Configurable Strike Force, Performance Profiling Dashboard, Material Comparison Scene

**Pitfalls to avoid:** Phase float drift at extended runtime (#11); Burst managed reference creep in new code (#6); ECS test World setup issues (#12); mix headroom/clipping as voice count grows

**Research flag:** Standard patterns — multi-harmonic synthesis is additive sines (trivial extension), visual feedback is a CurrentAmplitude reader (straightforward), ECS test fixtures are documented. No research phase needed; focus on integration testing infrastructure early.

### Phase Ordering Rationale

- **Dependency enforcement:** Each phase can only be tested if the previous phase's outputs exist. This prevents building systems in a vacuum where bugs are invisible.
- **Risk front-loading:** The most dangerous pitfall zone (audio thread safety, Phase 3) is approached only after ECS patterns are established and verified in Phase 1. Developers who arrive at Phase 3 with solid ISystem patterns will write cleaner bridge code.
- **Propagation isolation (Phase 2):** Sympathetic resonance is the thesis claim but is isolated to its own phase. This means the core audio loop (Phase 3) can be developed and validated even if propagation has bugs — the two paths are independent in the ECS data model.
- **Architecture front-running:** The single-AudioSource mixer pattern and the NativeArray bridge contract must be decided before Phase 3 code is written. A wrong architecture decision here is the project's single highest-cost rework scenario.

### Research Flags

Needs dedicated design work before implementation:
- **Phase 2 (Propagation):** Job structure for two-level iteration (emitters outer, receivers inner) using ComponentLookup vs. alternative two-pass approach; explicit design decision required
- **Phase 3 (Audio Bridge):** Threading contract documentation before any code is written; the audio thread safety boundary is the highest-risk implementation surface in the project

Standard patterns (no additional research needed):
- **Phase 1 (ECS Runtime Systems):** ISystem, IJobEntity, IEnableableComponent patterns are fully documented in the installed package; verify API signatures against local docs during implementation
- **Phase 4 (Polish):** Additive harmonic synthesis and visual feedback are straightforward extensions of Phase 3 infrastructure

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All recommendations verified against installed package documentation in Library/PackageCache. No external sources required — everything is already in the project. |
| Features | MEDIUM | Feature set derived from project documentation and domain knowledge; no external benchmark or user research. The thesis scope is self-defined, so "table stakes" means the thesis claim, not market expectations. |
| Architecture | HIGH | Three-layer runtime pattern (ECS jobs / MonoBehaviour bridge / audio callback) is well-established Unity DOTS practice. OnAudioFilterRead threading model is a stable Unity API with high-confidence behavior. |
| Pitfalls | HIGH | All critical pitfalls are grounded in fundamental constraints (audio thread architecture, algorithmic complexity, DSP phase theory, Burst compilation restrictions) that are independent of version-specific API changes. |

**Overall confidence:** HIGH

### Gaps to Address

- **EnableableComponent query filter semantics in Entities 1.3.9:** PITFALLS.md flags MEDIUM confidence on whether `.WithAll<T>()` correctly filters by enabled state vs. component existence. Verify against `Library/PackageCache/com.unity.entities@732b1f537003/Documentation~/components-enableable-use.md` before writing the first query in Phase 1.
- **PropagationSystem job structure:** How to access receiver components from inside an emitter-iterating job — whether ComponentLookup<ResonantObjectData> with [ReadOnly] is sufficient or whether the system needs to be restructured as two jobs (emitter-reads-snapshot, then receiver-writes) — needs a design decision before Phase 2 implementation.
- **Audio buffer size and latency at runtime:** The bridge copies data in LateUpdate (~60Hz), but the audio callback runs at ~48kHz. The gap is ~21ms of potential staleness. For this thesis project that is acceptable, but if audio quality review reveals zipper noise or amplitude lag, an amplitude interpolation scheme (linear ramp from previous to current amplitude across the audio buffer) should be added. This is deferred to Phase 4 polish.
- **DSPGraph status in Unity 6:** STACK.md rates this MEDIUM confidence. The recommendation to avoid DSPGraph is correct regardless — it was experimental and the project has no need for it — but the exact status (removed vs. hidden vs. unsupported) need not be verified since OnAudioFilterRead fully satisfies the thesis requirements.

## Sources

### Primary (HIGH confidence — local package documentation)
- `Library/PackageCache/com.unity.entities@732b1f537003/Documentation~/systems-isystem.md` — ISystem API, Burst compatibility
- `Library/PackageCache/com.unity.entities@732b1f537003/Documentation~/systems-comparison.md` — ISystem vs SystemBase decision matrix
- `Library/PackageCache/com.unity.entities@732b1f537003/Documentation~/iterating-data-ijobentity.md` — IJobEntity scheduling and attributes
- `Library/PackageCache/com.unity.entities@732b1f537003/Documentation~/components-managed.md` — Managed component constraints
- `Library/PackageCache/com.unity.entities@732b1f537003/Documentation~/components-enableable-use.md` — IEnableableComponent thread safety
- `Library/PackageCache/com.unity.burst@07790c2d06d9/Documentation~/compilation-burstcompile.md` — BurstCompile attribute, FloatMode, FloatPrecision
- `Library/PackageCache/com.unity.collections@aea9d3bd5e19/Documentation~/collection-types.md` — NativeArray thread safety

### Primary (HIGH confidence — stable Unity APIs)
- `OnAudioFilterRead(float[], int)` threading model — stable since Unity 5.x, audio thread behavior well-established
- NativeArray cross-thread access pattern — documented DOTS pattern for ECS-to-audio bridging

### Secondary (MEDIUM confidence — project source analysis)
- Existing project source files: `ResonantObjectData.cs`, `EmitterTag.cs`, `StrikeEvent.cs`, `ResonanceMath.cs` — feature completeness assessment and component extension requirements
- Existing planning docs: `.planning/PROJECT.md`, `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/CONCERNS.md` — scope constraints and known concerns

### Secondary (MEDIUM confidence — domain knowledge)
- DSP fundamentals: phase accumulation, sample-rate synthesis, waveform discontinuity theory — standard digital signal processing
- Computational physics: N-body interaction optimization patterns (distance culling, spatial partitioning)
- Unity Entities 1.x architectural patterns: system group scheduling, sync point behavior, ECB patterns

---
*Research completed: 2026-03-11*
*Ready for roadmap: yes*
