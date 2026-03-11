# Feature Landscape

**Domain:** Real-time physics-based sympathetic resonance simulation with procedural audio
**Researched:** 2026-03-11
**Overall confidence:** MEDIUM (based on domain knowledge of audio DSP, ECS patterns, and resonance physics; no external sources verified due to tool constraints)

## Table Stakes

Features the thesis demonstration requires to function. Missing any of these means the system cannot demonstrate its core claim: "real-time sympathetic resonance from material properties."

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Strike Input (Raycast Click)** | Without input, nothing vibrates. Thesis reviewer must be able to click objects to excite them. | Low | Raycast from camera, enable StrikeEvent on hit entity. MonoBehaviour in Hybrid/ that talks to ECS via EntityManager. |
| **Emitter Activation System** | Consumes StrikeEvent, enables EmitterTag, sets initial amplitude. The first link in the runtime chain. | Low | ISystem that queries `StrikeEvent` enabled entities, copies NormalizedForce to EmitterTag.StrikeAmplitude, enables EmitterTag, disables StrikeEvent. Straightforward ECS pattern. |
| **Exponential Decay System** | Objects must ring down after being struck. Without decay, amplitudes never change -- no audible or visual behavior. | Low | ISystem per frame: apply `ResonanceMath.ExponentialDecay()` to CurrentAmplitude. Disable EmitterTag when amplitude falls below AmplitudeThreshold. Math already exists. |
| **Emitter Deactivation** | Must stop processing objects that have decayed to silence. Without this, performance degrades as entities accumulate active state. | Low | Can be part of decay system: if CurrentAmplitude < AmplitudeThreshold, disable EmitterTag. Single conditional per entity. |
| **Procedural Audio Synthesis (Single Sine)** | The thesis claims audio output from physics. Must generate audible tones at computed natural frequencies. | Medium | MonoBehaviour with AudioSource using OnAudioFilterRead. Read frequency + amplitude from ECS, write sine wave samples. Phase continuity from ResonantObjectData.Phase is critical for glitch-free output. |
| **ECS-to-MonoBehaviour Audio Bridge** | Unity audio callbacks (OnAudioFilterRead) run on MonoBehaviours, not ECS systems. Must bridge the gap. | Medium | Hybrid pattern: MonoBehaviour reads ECS data in LateUpdate or via EntityQuery, caches values for audio thread. Must handle threading (audio callback is not main thread). Use lock-free double buffer or volatile fields. |
| **Phase Accumulation** | Without continuous phase tracking, sine wave output clicks/pops at every frame boundary. | Low | Already has Phase field in ResonantObjectData. System must increment phase by `2 * PI * frequency * deltaTime` each frame, wrapping at 2*PI. |
| **Distance-Based Attenuation** | Sympathetic resonance is distance-dependent. Closer objects resonate more strongly. Thesis must demonstrate this spatial behavior. | Low | ResonanceMath.InverseSquareAttenuation() already exists. Apply during propagation. |
| **Frame-Rate Independent Simulation** | Thesis must demonstrate consistent physics regardless of frame rate. ResonanceMath already uses frame-rate-independent formulas. | Low | Already designed into ResonanceMath.DrivenOscillatorStep() and ExponentialDecay(). Systems just need to pass SystemAPI.Time.DeltaTime. |
| **Multiple Simultaneous Emitters** | Must demonstrate more than one object vibrating at once. Core thesis scenario: strike object A, watch object B resonate sympathetically. | Medium | Audio mixing of multiple sine waves. Each active emitter contributes to the output buffer. Must limit voice count to avoid saturation. |

## Differentiators

Features that elevate the thesis from "working demo" to "impressive demonstration." Not strictly required, but significantly strengthen the academic contribution.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Sympathetic Resonance Propagation** | The headline feature: nearby objects at matching frequencies begin vibrating when a neighbor is struck. This IS the thesis. | High | Each active emitter drives all nearby entities via Lorentzian frequency matching. O(N*M) where N=emitters, M=receivers. Use DrivenOscillatorStep() with LorentzianResponse() as the coupling coefficient. Needs spatial queries (distance checks). |
| **Multi-Harmonic Audio (Overtone Series)** | Real objects produce harmonics (2x, 3x, 4x fundamental). Single sine sounds artificial and "electronic." Adding 2-3 harmonics sounds vastly more realistic. | Medium | Generate overtones at integer multiples of f0 with decreasing amplitude (1/n or 1/n^2 rolloff). Each harmonic is an additional sine in OnAudioFilterRead. 3-4 harmonics per voice is the sweet spot. |
| **Visual Amplitude Feedback** | Thesis reviewers need to SEE resonance, not just hear it. Scale, color, or shader effect driven by CurrentAmplitude. | Low | Simple: read amplitude, scale transform or tint material. Could use ECS-driven shader property. Dramatic visual impact for minimal code. |
| **Configurable Strike Force** | Different strike strengths produce different initial amplitudes and thus different volumes and decay behaviors. Shows the system responds proportionally. | Low | Already designed: StrikeEvent.NormalizedForce exists. Map mouse click duration or velocity to force value. |
| **Real-Time Parameter Display (HUD)** | Show frequency, amplitude, Q-factor, note name overlay on objects during simulation. Proves the physics are running, not pre-baked audio clips. | Low | OnGUI or UI Toolkit overlay. Read ResonantObjectData from EntityManager. NoteNameHelper already converts frequency to note names. |
| **Resonance Chain Reactions** | Object A excites Object B, which excites Object C. Cascade propagation demonstrating emergent behavior across a network of resonant objects. | High | Falls out of correct propagation implementation: if B becomes an emitter (amplitude above threshold), it naturally drives C. Must handle re-entrancy carefully (avoid infinite feedback loops -- need energy conservation or max amplitude clamping). |
| **Material Comparison Scene** | Pre-built scene with identical geometry in different materials (Steel, Glass, Wood, Rubber). Strike one, compare resonance behavior. Powerful thesis demonstration. | Low | Scene design, not code. Place same-shape objects with different MaterialProfileSO assignments. Reviewer immediately sees high-Q steel ringing vs. damped rubber thud. |
| **Performance Profiling Dashboard** | Show entity count, active emitter count, frame time. Proves DOTS/Burst performance claims. | Low | Unity Profiler markers via ProfilerMarker. Display via simple UI. Already have com.unity.profiling.core in packages. |

## Anti-Features

Features to deliberately NOT build. Common traps in audio/resonance systems that would waste time, add complexity, or undermine the thesis's physics-first approach.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Full Modal Analysis from Mesh** | Requires finite element analysis (FEA) -- computationally expensive, well beyond thesis scope, and a separate research domain entirely. Would consume months for marginal accuracy improvement over bounding box approximation. | Keep bounding box classification. Document it as a known approximation. The thesis contribution is the real-time ECS pipeline, not FEA accuracy. |
| **FMOD/Wwise Integration** | Professional audio middleware adds massive dependency, configuration complexity, and obscures the procedural synthesis that IS the thesis contribution. Reviewers cannot inspect FMOD internals. | Use Unity AudioSource with OnAudioFilterRead. The simplicity IS the point -- audio emerges from physics, not middleware presets. Mention FMOD as "future work." |
| **Spatial Audio (HRTF/Ambisonics)** | 3D audio spatialization is orthogonal to resonance physics. Adds DSP complexity without strengthening the resonance simulation claim. | Use basic Unity AudioSource 3D settings if spatial cues are needed. Do not implement custom HRTF. |
| **FFT-Based Spectral Analysis at Runtime** | Tempting for visualization, but FFT adds computational overhead, complexity, and is not needed for synthesis (we already know the frequencies analytically). | Display computed frequencies directly. If a spectrum view is desired, compute it analytically from known harmonics -- do not FFT the audio output. |
| **Designer-Tunable Artistic Parameters** | The thesis explicitly states all behavior derives from material science. Adding "brightness," "warmth," or "attack" knobs contradicts the core claim. | All acoustic behavior from E, rho, eta, nu. If something sounds wrong, fix the physics or material data, not add a fudge factor. |
| **Continuous Driving Forces (Bowing/Wind)** | Modeling sustained excitation (violin bow, wind on bridge cables) requires different physics (stick-slip, vortex shedding). Scope creep that would require new math models. | Stick to impulse excitation (strike). Mention continuous driving as future work. The DrivenOscillatorStep() math supports it, but the input and physics validation are separate research. |
| **Runtime Material Creation** | Adding new materials at runtime (user-defined E, rho, eta) seems flexible but introduces validation nightmares and impossible-to-debug physics behavior. | Materials are baked at edit time via ScriptableObjects. 10 presets cover the thesis needs. |
| **Networked/Multiplayer Resonance** | Explicitly out of scope per PROJECT.md. Would require deterministic simulation synchronization. | Single-player only. |
| **Audio Recording/Export** | Writing audio to disk is a nice-to-have but adds file I/O, format handling, and testing burden with no thesis value. | If needed for thesis appendix, use external screen/audio capture software. |

## Feature Dependencies

```
Strike Input (Raycast)
  └──> Emitter Activation System
         └──> Exponential Decay System + Phase Accumulation
                ├──> Emitter Deactivation (part of decay)
                ├──> ECS-to-MonoBehaviour Audio Bridge
                │      └──> Procedural Audio Synthesis (OnAudioFilterRead)
                │             └──> Multiple Simultaneous Emitters (voice mixing)
                │                    └──> Multi-Harmonic Audio (overtones) [DIFFERENTIATOR]
                └──> Sympathetic Resonance Propagation [DIFFERENTIATOR]
                       └──> Distance-Based Attenuation
                              └──> Resonance Chain Reactions [DIFFERENTIATOR]

Visual Amplitude Feedback ←── reads CurrentAmplitude (independent of audio path)
Real-Time Parameter Display ←── reads ResonantObjectData (independent of audio path)
Material Comparison Scene ←── requires all table stakes working (scene design task)
```

**Critical path:** Strike Input -> Activation -> Decay -> Audio Bridge -> Synthesis. This is the minimum chain for audible output.

**Parallel work:** Visual feedback and parameter display can be built independently once decay system updates CurrentAmplitude.

**Propagation is independent of audio:** Sympathetic resonance propagation updates amplitudes in ECS. Audio synthesis reads those amplitudes. They do not need to be built together -- propagation can be validated visually before audio is connected.

## MVP Recommendation

For the minimum viable thesis demonstration, prioritize in this order:

1. **Emitter Activation System** -- Consumes StrikeEvent, enables EmitterTag (table stakes, Low complexity)
2. **Exponential Decay System with Phase Accumulation** -- Objects ring and decay (table stakes, Low complexity)
3. **Strike Input via Raycast** -- User can click to excite objects (table stakes, Low complexity)
4. **ECS-to-MonoBehaviour Audio Bridge** -- Thread-safe data transfer (table stakes, Medium complexity)
5. **Procedural Audio Synthesis** -- Audible sine wave output (table stakes, Medium complexity)
6. **Sympathetic Resonance Propagation** -- The thesis headline feature (differentiator, High complexity)
7. **Visual Amplitude Feedback** -- Reviewers can see resonance (differentiator, Low complexity)

Defer to post-MVP:
- **Multi-Harmonic Audio:** Enhances realism but single sine demonstrates the principle. Add after core loop works.
- **Resonance Chain Reactions:** Emerges from correct propagation but needs energy conservation safeguards. Validate two-body resonance first.
- **Performance Profiling Dashboard:** Nice for thesis chapter on performance, but build it when writing that chapter.
- **Material Comparison Scene:** Scene design task, not engineering. Build when preparing thesis demonstration.

## Complexity Budget

| Category | Feature Count | Estimated Effort |
|----------|--------------|-----------------|
| Table Stakes (must build) | 10 features | ~3-4 days of focused work |
| High-Value Differentiators | 3-4 features | ~2-3 days additional |
| Scene/Content Work | 2 features | ~1 day |
| **Total for strong thesis demo** | ~15 features | ~6-8 days |

The table stakes are mostly Low complexity because the hard work (physics math, component design, baking pipeline) is already done. The remaining work is plumbing: connecting existing math to ECS systems and routing data to audio output.

## Sources

- Project source code analysis (ResonantObjectData.cs, EmitterTag.cs, StrikeEvent.cs, ResonanceMath.cs)
- Existing project documentation (.planning/PROJECT.md, .planning/codebase/ARCHITECTURE.md, CONCERNS.md)
- Domain knowledge of audio DSP, ECS patterns, and driven harmonic oscillator physics (MEDIUM confidence -- based on training data, not externally verified for this specific session)

---

*Feature landscape: 2026-03-11*
