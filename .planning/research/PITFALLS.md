# Domain Pitfalls

**Domain:** Unity DOTS procedural audio + ECS runtime resonance simulation
**Project:** Sound Resonation System (Unity 6, Entities 1.3.9, Burst 1.8.28)
**Researched:** 2026-03-11

---

## Critical Pitfalls

Mistakes that cause rewrites, crashes, or fundamental architecture failures.

---

### Pitfall 1: OnAudioFilterRead runs on the audio thread, not the main thread

**What goes wrong:** `OnAudioFilterRead` is called on Unity's audio thread at the DSP sample rate (typically 48000 Hz in blocks of 256-1024 samples). Developers read ECS component data directly from inside this callback, causing race conditions, stale data, or hard crashes. Unlike `Update()` or `LateUpdate()`, this callback is unsynchronized with the main thread and the ECS job system.

**Why it happens:** Unity's documentation buries this detail. Developers assume all MonoBehaviour callbacks run on the main thread. The callback looks innocuous -- just fill a float array -- but it executes concurrently with ECS system updates.

**Consequences:**
- Reading `ResonantObjectData.CurrentAmplitude` or `.Phase` from the audio thread while an ISystem job writes to it produces torn reads (partially written floats on some architectures, though x86 float reads are atomic, relying on this is undefined behavior in C#).
- Calling any EntityManager API from the audio thread will throw or crash.
- Intermittent audio glitches that are nearly impossible to reproduce in debugging.

**Prevention:**
- Copy amplitude and frequency data from ECS to a shared `NativeArray<float>` or plain `float[]` on the main thread (in `LateSimulationSystemGroup` or a MonoBehaviour `LateUpdate`).
- The audio thread reads ONLY from this shared buffer, never from ECS directly.
- Use `volatile` fields or `Interlocked` operations if using plain fields, but a double-buffer pattern is more robust: main thread writes to buffer A while audio thread reads buffer B, then swap.
- Keep the audio callback as minimal as possible -- multiply amplitude by sine of phase, increment phase, write to output buffer. No allocations, no API calls.

**Detection (warning signs):**
- Any `EntityManager`, `SystemAPI`, or `ComponentLookup` reference inside or reachable from `OnAudioFilterRead`.
- Audio pops/clicks that appear only under load or with many entities.
- Sporadic `InvalidOperationException` from the job safety system.

**Phase relevance:** Must be addressed in the Audio Synthesis phase. The hybrid bridge architecture decision is load-bearing -- get it wrong and you rebuild the entire audio pipeline.

**Confidence:** HIGH -- this is fundamental Unity audio architecture, well-documented behavior.

---

### Pitfall 2: N x N entity pair iteration explodes with quadratic cost

**What goes wrong:** Resonance propagation requires each emitter to potentially drive every receiver. The naive approach iterates all emitter-receiver pairs in a nested loop, producing O(N^2) complexity. With 50 entities, that is 2,500 pairs per frame. With 200, it is 40,000. The system becomes the frame-time bottleneck long before the entity count seems "large."

**Why it happens:** The resonance physics genuinely requires pairwise interaction (emitter frequency drives receiver via Lorentzian response, attenuated by distance). There is no obvious spatial shortcut when you first design it.

**Consequences:**
- Frame time scales quadratically with entity count.
- Burst compilation helps the constant factor but not the algorithmic complexity.
- The thesis claim of "real-time simulation" becomes false at modest entity counts.

**Prevention:**
- **Distance culling first:** Use `InverseSquareAttenuation` to skip pairs beyond a maximum interaction distance. At 10m reference distance, objects 100m away contribute 0.01% amplitude -- below `AmplitudeThreshold`. This turns N x N into N x K where K is the local neighborhood size.
- **Frequency culling second:** Skip pairs where LorentzianResponse < threshold. A steel object (Q=10000) at 440Hz will not respond to a 200Hz emitter. Pre-sort or bucket entities by frequency band to avoid computing Lorentzian for obviously mismatched pairs.
- **Only iterate active emitters:** The `EmitterTag` IEnableableComponent already filters to active emitters. Ensure the outer loop queries only enabled EmitterTag entities, not all ResonantObjectData entities.
- **Consider spatial hashing** if entity counts exceed ~100: partition world space into cells, only check emitter-receiver pairs in adjacent cells.

**Detection (warning signs):**
- Frame time grows noticeably when adding entities, even at 30-50 count.
- Profiler shows the propagation system dominating `SimulationSystemGroup`.
- Two nested `foreach` over `SystemAPI.Query` with no early-exit conditions.

**Phase relevance:** Core to the Resonance Propagation system design. Must be addressed when implementing `ResonancePropagationSystem`, not retrofitted later.

**Confidence:** HIGH -- this is algorithmic reality, independent of any API.

---

### Pitfall 3: Sync points from structural changes kill ECS parallelism

**What goes wrong:** A developer enables/disables `EmitterTag` or `StrikeEvent` using `EntityManager.SetComponentEnabled()` (the managed/main-thread version) from inside a system that runs between parallel jobs. This creates a sync point -- the job system must complete ALL scheduled jobs before the structural change can proceed, serializing the entire frame.

**Why it happens:** Confusion between `EntityManager.SetComponentEnabled()` (requires sync point) and `EnabledRefRW<T>` accessed through `SystemAPI.Query` or `IJobEntity` (no sync point, safe in parallel with chunk-level access). The Entities API has both paths and the naming does not make the cost obvious.

**Consequences:**
- Every frame that activates or deactivates an emitter forces a full sync point.
- Parallel job scheduling becomes effectively single-threaded around that system.
- Performance degrades linearly with the number of state transitions per frame.

**Prevention:**
- In `EmitterActivationSystem` and `EmitterDeactivationSystem`, use `EnabledRefRW<EmitterTag>` and `EnabledRefRW<StrikeEvent>` from within `IJobEntity` or `SystemAPI.Query` foreach -- these operate on the chunk's enable bits without structural changes and without sync points.
- Use `EntityCommandBuffer` (ECB) from `EndSimulationEntityCommandBufferSystem` if you must defer enable/disable to a safe point, though for IEnableableComponent this is unnecessary -- direct `EnabledRefRW` is the correct path.
- Profile with the Entities Structural Changes profiler marker to detect unexpected sync points.

**Detection (warning signs):**
- `EntityManager.SetComponentEnabled` called inside `OnUpdate` of any ISystem.
- Profiler showing "Sync Point" markers in the Entities module.
- `CompleteDependency()` calls scattered in system code.

**Phase relevance:** Must be correct from the first implementation of EmitterActivation/Deactivation systems. Retrofitting is painful because it changes the system's job scheduling model.

**Confidence:** HIGH -- this is core Entities 1.x architecture, well-documented.

---

### Pitfall 4: Phase accumulator discontinuities cause audio clicks

**What goes wrong:** The `ResonantObjectData.Phase` field is a phase accumulator for sine wave synthesis. If phase is reset to 0 when a strike occurs, or if the audio thread reads a phase value that jumps discontinuously between frames, the output waveform has a discontinuity. A discontinuity in a waveform is a click/pop -- an impulse that is audible and unpleasant regardless of volume.

**Why it happens:** Developers think of amplitude as the important value and treat phase as secondary. They reset phase on new strikes, or they recompute phase from scratch each frame rather than accumulating it continuously.

**Consequences:**
- Audible clicks on every strike event.
- Audible clicks when multiple emitters drive the same receiver (phase interference artifacts).
- Clicks at system start/stop boundaries.

**Prevention:**
- **Never reset phase to 0.** When a new strike occurs, keep the existing phase and only change amplitude. The component already has `Phase` as a continuous accumulator -- honor this.
- **Accumulate phase continuously:** `phase += 2 * PI * frequency * deltaTime; phase %= 2 * PI;` in the ECS system. The audio thread should receive the current phase and frequency, then interpolate between samples at the audio sample rate.
- **Fade amplitude, not phase:** When deactivating an emitter (amplitude < threshold), let amplitude reach zero smoothly via `ExponentialDecay` before disabling the EmitterTag. Never abruptly set amplitude to 0.
- **Sample-rate phase increment in audio callback:** The ECS runs at frame rate (60-144Hz). Audio runs at 48000Hz. The audio callback must increment phase at `2 * PI * frequency / sampleRate` per sample, not use the frame-rate phase directly. The ECS phase is a synchronization reference, not the audio phase.

**Detection (warning signs):**
- Clicks when striking objects, especially when striking a second time while still ringing.
- Phase field being assigned `= 0f` anywhere in runtime code.
- Audio output that sounds "correct" at low frequencies but clicks at high frequencies (where phase jumps are proportionally larger per sample).

**Phase relevance:** Must be designed correctly in both the ECS propagation system (phase accumulation) and the audio synthesis bridge (sample-rate interpolation). Spans two implementation phases.

**Confidence:** HIGH -- this is fundamental DSP knowledge, applies to all procedural audio.

---

## Moderate Pitfalls

Mistakes that cause significant debugging time or technical debt.

---

### Pitfall 5: Querying ECS data from MonoBehaviour without proper completion

**What goes wrong:** The hybrid bridge MonoBehaviour (which holds the AudioSource and implements `OnAudioFilterRead`) needs amplitude/frequency data from ECS. Developers call `EntityManager.GetComponentData<ResonantObjectData>(entity)` in `Update()` or `LateUpdate()` without ensuring that ECS jobs writing to that component have completed.

**Why it happens:** MonoBehaviour lifecycle runs outside the ECS system update loop. When `LateUpdate` fires, there may be scheduled-but-incomplete jobs that write to `ResonantObjectData`. The job safety system will either throw an exception or force an implicit sync point.

**Prevention:**
- Place data extraction in a dedicated ISystem that runs in `PresentationSystemGroup` (after `LateSimulationSystemGroup`). This system copies data to a `NativeArray` or shared buffer. The MonoBehaviour reads from this buffer, never from ECS directly.
- Alternatively, use `EntityQuery.CompleteDependency()` before reading, but this creates a sync point -- acceptable only if the bridge system is the last thing to run before rendering.
- The cleanest pattern: ISystem writes to a singleton `NativeArray<AudioEntityData>` component. MonoBehaviour reads from this array. One sync point, predictable location.

**Detection:**
- `InvalidOperationException` mentioning job safety handles.
- Implicit sync points appearing in the Profiler during `LateUpdate`.
- Audio data being one frame stale (reads happening before write jobs complete, getting last frame's values).

**Phase relevance:** Hybrid Bridge implementation phase.

**Confidence:** HIGH.

---

### Pitfall 6: Burst job safety violations from captured managed references

**What goes wrong:** An `IJobEntity` or Burst-compiled `ISystem.OnUpdate` captures a managed reference (string, class, managed array, delegate) in its closure or struct fields. Burst cannot compile this and either fails silently (falls back to managed execution at 10-100x slower) or throws a compile error.

**Why it happens:** The `ResonantObjectData` struct is clean (all blittable), but developers add helper fields or debugging data as strings/managed types. Or they try to use `Debug.Log` inside a Burst job. Or they capture a `List<Entity>` instead of a `NativeList<Entity>`.

**Consequences:**
- Burst fallback to managed: simulation runs 10-100x slower with no visible error (just a Burst compilation warning in the console that's easy to miss).
- Hard compile error if Burst strict mode is enabled.
- Allocated managed memory from jobs causes GC pressure, creating frame-time spikes.

**Prevention:**
- Enable Burst "strict mode" in project settings so failures are errors, not silent fallbacks.
- Use only `NativeArray`, `NativeList`, `NativeHashMap` for collections in jobs.
- No `string`, `class`, `UnityEngine.Object`, or `delegate` in any job struct or ISystem.
- Use `Unity.Logging` (structured logging) instead of `Debug.Log` for Burst-compatible logging, or guard debug code with `#if !UNITY_BURST_COMPILATION`.

**Detection:**
- Burst Inspector (Window > Burst > Burst Inspector) showing "Compilation Failed" or "Not Compiled" for expected jobs.
- Console warnings about Burst compilation failures during domain reload.
- Profiler showing "Managed" execution for systems that should be Burst-compiled.

**Phase relevance:** All runtime system implementation. Should be enforced from the first system written.

**Confidence:** HIGH.

---

### Pitfall 7: Forgetting deltaTime frame-rate independence in decay calculations

**What goes wrong:** Developers use a fixed decay factor per frame (`amplitude *= 0.99f`) instead of using the frame-rate-independent `ExponentialDecay` already implemented in `ResonanceMath`. The simulation runs correctly at the developer's frame rate but produces different physics at other frame rates.

**Why it happens:** The per-frame multiplication "looks right" during testing. The developer tests at 60fps, the thesis reviewer runs at 30fps or 144fps, and the decay times are completely different. The existing `ResonanceMath.ExponentialDecay` and `DrivenOscillatorStep` are already frame-rate independent (they use `exp(-dt/tau)` and `math.lerp` with time-derived alpha), but a developer implementing a new system might not use them.

**Consequences:**
- Physics simulation produces different results at different frame rates.
- Thesis results are not reproducible.
- Objects decay twice as fast at 120fps vs 60fps, or half as fast at 30fps.

**Prevention:**
- ALL amplitude updates MUST go through `ResonanceMath.DrivenOscillatorStep` (for driven oscillation) or `ResonanceMath.ExponentialDecay` (for free decay). No direct amplitude manipulation.
- Pass `SystemAPI.Time.DeltaTime` (or `UnityEngine.Time.deltaTime` in MonoBehaviour) to all math functions. Never use a constant factor.
- Test at multiple frame rates (30, 60, 144) and verify that decay times are consistent.

**Detection:**
- Any `*= constant` applied to amplitude outside of ResonanceMath.
- Different audio behavior when VSync is toggled or the editor is under load.
- `ResonanceMath` functions not being called from propagation/decay systems.

**Phase relevance:** ECS runtime systems phase. Easy to prevent if established as a rule upfront.

**Confidence:** HIGH -- the project already has the correct math; the risk is not using it.

---

### Pitfall 8: Audio sample rate mismatch between ECS simulation and OnAudioFilterRead

**What goes wrong:** ECS systems run at frame rate (e.g., 60 Hz). `OnAudioFilterRead` runs at audio sample rate (e.g., 48000 Hz). Developers pass the ECS-computed amplitude directly to the audio buffer, producing 60 amplitude changes per second instead of 48000. The result is a stepped, buzzy waveform rather than a smooth sine wave.

**Why it happens:** The mental model is "ECS computes the sound, audio callback plays it." But the audio callback must generate smooth waveforms at 48000 Hz from parameters that update at 60 Hz. This requires interpolation.

**Consequences:**
- Audio sounds like a 60 Hz square wave modulated by the desired frequency.
- Severe aliasing artifacts.
- Amplitude "zipper noise" (audible stepping when amplitude changes).

**Prevention:**
- The ECS system provides: frequency (Hz), amplitude (0-1), and active/inactive state. Updated at frame rate.
- The audio callback generates the waveform sample-by-sample:
  ```
  for each sample:
      phase += 2 * PI * frequency / sampleRate
      output[i] = amplitude * sin(phase)
  ```
- Interpolate amplitude changes across the audio buffer (linear ramp from previous amplitude to current amplitude over the buffer length) to avoid zipper noise.
- The phase accumulator MUST live in the MonoBehaviour (audio-thread-local), not in ECS. ECS tracks "is this entity vibrating and at what amplitude." The audio thread tracks "where am I in the waveform."

**Detection:**
- Audio output sounds buzzy/aliased even at low frequencies.
- Waveform visualization shows staircase patterns.
- Audio quality changes when frame rate changes.

**Phase relevance:** Audio Synthesis phase. Architecture-level decision about what lives in ECS vs what lives in the audio callback.

**Confidence:** HIGH.

---

### Pitfall 9: Entity-to-AudioSource mapping becomes a lifecycle nightmare

**What goes wrong:** Each emitting entity needs an AudioSource for output. Developers create one AudioSource per entity at scene start, or dynamically instantiate them per-strike. Both approaches have problems: pre-allocation wastes resources when most entities are silent, dynamic instantiation causes GC spikes and latency at strike time.

**Why it happens:** Unity's audio system is MonoBehaviour-based. There is no ECS-native audio output. Bridging between "entities that are vibrating" and "AudioSources that produce sound" requires explicit mapping and lifecycle management.

**Consequences:**
- Pre-allocation: 200 AudioSources exist for 200 entities even if only 3 are active. Each AudioSource has overhead.
- Dynamic allocation: GC spike and 1+ frame latency when a new entity starts emitting. AudioSource instantiation triggers managed allocations.
- Orphaned AudioSources if entities are destroyed without cleanup.
- AudioSource pool management becomes the most complex code in the project.

**Prevention:**
- **Use an AudioSource pool** with a small fixed size (e.g., 8-16 voices, matching the maximum expected simultaneous emitters). When an entity starts emitting, claim a pooled AudioSource. When it stops, release it.
- **Priority system:** If all pool slots are full, steal the quietest (lowest amplitude) AudioSource for the new emitter. This is standard voice-stealing in audio engines.
- **Single AudioSource alternative:** For a thesis demo with modest entity counts, consider a SINGLE AudioSource with `OnAudioFilterRead` that mixes ALL active emitters into one buffer. This eliminates pool management entirely. Sum: `output[i] = sum(amplitude_k * sin(phase_k))` for all active entities k.
- The single-AudioSource mixer approach is strongly recommended for this project given the scope.

**Detection:**
- AudioSource count growing unbounded in the Hierarchy.
- GC allocations in the Profiler during strike events.
- Audio dropouts when many entities activate simultaneously.

**Phase relevance:** Hybrid Bridge design phase. The pool vs mixer decision affects all downstream audio code.

**Confidence:** HIGH.

---

## Minor Pitfalls

Mistakes that cause annoyance or minor debt but are fixable without rework.

---

### Pitfall 10: EnableableComponent query semantics are confusing

**What goes wrong:** `SystemAPI.Query<ResonantObjectData, EmitterTag>()` returns entities where EmitterTag EXISTS on the entity, regardless of whether it is enabled or disabled. To filter to only enabled entities, you must include `EnabledRefRO<EmitterTag>` or use `.WithAll<EmitterTag>()` which does filter by enabled state when the component is IEnableableComponent.

**Why it happens:** The Entities API behavior here is subtle. `WithAll<T>` filters by enabled state for IEnableableComponent. But accessing the component directly through the query tuple does not filter. The documentation is not immediately clear on this distinction.

**Prevention:**
- For the propagation system (only process active emitters): use `SystemAPI.Query<...>().WithAll<EmitterTag>()` -- this correctly filters to enabled-only.
- For the activation system (find entities with enabled StrikeEvent): use `.WithAll<StrikeEvent>()`.
- For the deactivation system (find active emitters below threshold): query `.WithAll<EmitterTag>()`, check amplitude, then get `EnabledRefRW<EmitterTag>` to disable.
- Write a unit test that creates an entity with disabled EmitterTag and verifies it does NOT appear in propagation queries.

**Detection:**
- Systems processing all entities instead of just active ones (silent performance bug).
- Deactivation system never running because it filters out disabled entities it needs to process (logic inversion).

**Phase relevance:** ECS runtime systems -- first system implementation.

**Confidence:** MEDIUM -- behavior may vary between Entities versions; verify with the 1.3.9 API.

---

### Pitfall 11: Floating-point phase accumulator drift over long sessions

**What goes wrong:** The phase accumulator `phase += increment; phase %= 2*PI;` gradually loses precision as the float value grows. After minutes of continuous playback, the phase accumulator's precision degrades because single-precision floats have fewer significant digits at larger values.

**Why it happens:** A float32 has ~7 significant digits. `2 * PI` is ~6.28. After enough additions, the accumulator value grows, and the modulo brings it back, but accumulated rounding errors produce frequency drift or waveform distortion.

**Prevention:**
- Apply `phase %= (2f * math.PI)` or `phase -= 2f * math.PI * math.floor(phase / (2f * math.PI))` every frame to keep phase in [0, 2*PI). This bounds the maximum value and maintains precision.
- The audio callback's local phase accumulator needs the same treatment.
- For this thesis project (likely demos of minutes, not hours), this is low severity but trivially preventable.

**Detection:**
- Audio pitch drifting after extended playback.
- Phase values growing to large numbers in the debugger.

**Phase relevance:** Both ECS systems and audio synthesis. Trivial to add during implementation.

**Confidence:** HIGH -- fundamental floating-point behavior.

---

### Pitfall 12: Testing ECS systems in PlayMode requires SubScene setup

**What goes wrong:** PlayMode tests for ECS systems fail or behave unexpectedly because the test does not properly set up the World, EntityManager, and system groups. Developers try to test systems by creating entities manually but forget to schedule system updates or process command buffers.

**Why it happens:** ECS test infrastructure is different from MonoBehaviour testing. You cannot just call `system.OnUpdate()` -- you need a properly configured World with system groups.

**Prevention:**
- Use `Unity.Entities.Tests` test fixtures (e.g., `ECSTestsFixture`) that provide a pre-configured `World` and `EntityManager`.
- For integration tests, create test SubScenes with known entity configurations.
- Call `World.Update()` to advance the full system group pipeline, not individual systems.
- For the audio bridge, test the data flow (ECS to shared buffer) separately from the audio callback. Mock the shared buffer for audio callback tests.

**Detection:**
- Tests that pass in isolation but fail when run together (World leaking between tests).
- Tests that require specific frame counts to produce results (timing-dependent).

**Phase relevance:** Integration testing phase. Plan test infrastructure before writing tests.

**Confidence:** MEDIUM.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Severity | Mitigation |
|---|---|---|---|
| ECS Runtime Systems (EmitterActivation) | Sync points from `EntityManager.SetComponentEnabled` (#3) | Critical | Use `EnabledRefRW` in jobs/queries, not EntityManager |
| ECS Runtime Systems (Propagation) | N x N quadratic explosion (#2) | Critical | Distance + frequency culling, iterate only active emitters |
| ECS Runtime Systems (Propagation) | EnableableComponent query confusion (#10) | Moderate | Use `.WithAll<EmitterTag>()` for enabled-only filtering |
| ECS Runtime Systems (Decay) | Frame-rate dependent decay (#7) | Moderate | Use only `ResonanceMath.ExponentialDecay`, always pass deltaTime |
| Audio Synthesis | Audio thread safety (#1) | Critical | Shared buffer pattern, never access ECS from audio thread |
| Audio Synthesis | Phase discontinuity clicks (#4) | Critical | Never reset phase, accumulate continuously |
| Audio Synthesis | Sample rate mismatch (#8) | Moderate | Audio callback generates waveform at 48kHz, ECS provides parameters at frame rate |
| Audio Synthesis | Phase float drift (#11) | Minor | Modulo phase into [0, 2*PI) every update |
| Hybrid Bridge | MonoBehaviour reading incomplete jobs (#5) | Moderate | Dedicated ISystem in PresentationSystemGroup copies to shared buffer |
| Hybrid Bridge | Entity-AudioSource lifecycle (#9) | Moderate | Single-AudioSource mixer pattern for thesis scope |
| All Runtime Systems | Burst managed reference violations (#6) | Moderate | Enable Burst strict mode, no managed types in jobs |
| Integration Testing | ECS test World setup (#12) | Minor | Use ECSTestsFixture, call World.Update() |

---

## Summary of Prevention Architecture

The majority of critical pitfalls (1, 4, 5, 8, 9) are resolved by one architectural decision: **the shared-buffer single-mixer pattern.**

```
ECS World (frame rate)              Audio Thread (48kHz)
========================            =====================
ResonancePropagationSystem          OnAudioFilterRead
  - updates CurrentAmplitude          - reads shared buffer
  - uses only ResonanceMath           - generates sine per sample
         |                            - local phase accumulator
         v                            - linear amplitude ramp
PresentationSystemGroup                     ^
  AudioDataCopySystem                       |
  - copies amplitude + freq -----> SharedAudioBuffer (NativeArray or float[])
    to shared buffer                  [freq, amplitude, active] per voice
```

This pattern provides:
- Thread safety (no ECS access from audio thread)
- Correct sample-rate synthesis (audio thread generates waveform)
- Clean lifecycle (fixed-size buffer, no dynamic allocation)
- Single sync point (copy system runs after all simulation)

---

## Sources

- Unity Entities 1.x architecture: IEnableableComponent, sync points, EntityCommandBuffer patterns -- based on Entities 1.3.x API (training data, MEDIUM confidence; verify specific query behavior with Entities 1.3.9 docs)
- OnAudioFilterRead threading model: Unity AudioSource documentation (HIGH confidence -- stable behavior since Unity 5)
- DSP fundamentals (phase accumulation, sample-rate synthesis, discontinuity artifacts): standard digital signal processing (HIGH confidence)
- N-body interaction optimization patterns: standard computational physics (HIGH confidence)
- Burst compilation restrictions: Unity Burst 1.8.x documentation (HIGH confidence -- stable restrictions since Burst 1.0)
