# Phase 3: Hybrid Bridge and Audio Synthesis - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

The ECS simulation produces audible output — strike an object and hear a synthesized tone at its natural frequency that decays in sync with the physics. Multiple objects produce simultaneous spatialized tones that mix additively. Tone timbre emerges from shape-based harmonic ratios, not pre-recorded samples. Visual feedback and debug HUD are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Voice architecture
- Per-entity AudioSource: each active emitter gets its own AudioSource with Unity 3D spatial blend for stereo spatialization
- Voice pool of 16 AudioSources, pre-allocated and reused
- Voice stealing: when pool is full, steal the voice with lowest CurrentAmplitude
- Release: short fade (50-100ms) when EmitterTag deactivates — prevents clicks without being perceptible
- Each voice runs OnAudioFilterRead on its own AudioSource for PCM synthesis

### Harmonic timbre design
- 4 partials per voice: fundamental + 3 overtones (64 total sine generators at max polyphony)
- Shape-specific harmonic ratios:
  - Bar: 1, 2.76, 5.40, 8.93 (Euler-Bernoulli beam modes)
  - Plate: 1, 1.59, 2.14, 2.65 (Kirchhoff plate modes)
  - Shell: 1, 1.51, 1.93, 2.29 (Donnell shell modes)
- Shape-specific amplitude weights: bars emphasize fundamental (metallic ring), plates distribute more evenly (shimmery), shells are mid-heavy (bell-like)
- Higher partials decay faster: partial N decays proportionally N× faster than fundamental — attack is bright, sustain mellows
- Sympathetic voices produce purer tone: emphasize fundamental, suppress upper partials — physically accurate since only the resonant frequency couples

### Strike transient
- Band-limited noise burst filtered around the fundamental frequency — material-specific character (steel = bright crack, wood = dull thud)
- Material-dependent duration: steel ~5ms (sharp), glass ~3ms (crisp), wood ~10-15ms (softer)
- Intensity scales with strike force (NormalizedForce) — ready for variable force in v2
- No transient for sympathetically activated voices — they fade in smoothly, no physical impact occurred

### Thread-safe bridge (Claude's discretion)
- NativeArray shared-buffer from ECS to audio thread (decided in PROJECT.md)
- Bridge copies active emitter data (amplitude, frequency, position, shape) in LateUpdate
- Claude handles: buffer layout, synchronization strategy, voice activation/deactivation signaling

### Claude's Discretion
- Thread-safe bridge implementation details (buffer format, lock-free vs. double-buffer)
- Exact harmonic amplitude weights per shape (researcher should investigate physical values)
- Noise burst generation algorithm for strike transient
- AudioSource configuration (spatial blend curve, rolloff settings)
- Voice pool management implementation
- Fade-out implementation for release

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### ECS integration points
- `Assets/SoundResonance/Runtime/Systems/SympatheticPropagationSystem.cs` — EmitterSnapshot struct, active emitter data collection pattern (reusable for audio bridge)
- `Assets/SoundResonance/Runtime/Systems/EmitterActivationSystem.cs` — Strike activation flow, where StrikeEvent is consumed
- `Assets/SoundResonance/Runtime/Systems/EmitterDeactivationSystem.cs` — Where EmitterTag gets disabled (triggers voice release)
- `Assets/SoundResonance/Runtime/Systems/ExponentialDecaySystem.cs` — Amplitude decay, execution order reference

### Component data
- `Assets/SoundResonance/Runtime/Components/ResonantObjectData.cs` — CurrentAmplitude, NaturalFrequency, QFactor, Shape fields
- `Assets/SoundResonance/Runtime/Components/EmitterTag.cs` — Enableable component marking active voices
- `Assets/SoundResonance/Runtime/Physics/ResonanceMath.cs` — DriveTimeConstant, ExponentialDecay for partial decay calculations

### Shape classification
- `Assets/SoundResonance/Runtime/Physics/ShapeClassifier.cs` — ShapeType enum (Bar, Plate, Shell) used for harmonic ratio lookup

### Prior phase decisions
- `.planning/phases/01-single-object-ecs-pipeline/01-CONTEXT.md` — ADSR deferred to Phase 3, additive re-excitation model
- `.planning/phases/02-sympathetic-propagation/02-CONTEXT.md` — Direct-only propagation (but active emitters can receive energy from others)

### Requirements
- `.planning/REQUIREMENTS.md` — AUD-01 (hybrid bridge), AUD-02 (OnAudioFilterRead synthesis)

### Project architecture
- `.planning/PROJECT.md` — Three execution contexts (ECS ~60Hz, MonoBehaviour main thread, audio ~48kHz), NativeArray bridge pattern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `EmitterSnapshot` struct in SympatheticPropagationSystem: pattern for collecting active emitter data on main thread — audio bridge can follow same approach
- `ResonanceMath.ExponentialDecay()`: can compute per-partial decay rates for the faster-decaying upper partials
- `ResonanceMath.DriveTimeConstant()`: useful for computing partial-specific time constants
- `ShapeType` enum (Bar/Plate/Shell): already exists, used to index harmonic ratio tables
- `AmplitudeVisualizationSystem`: pattern reference for reading ResonantObjectData across all entities

### Established Patterns
- ISystem + IJobEntity with ScheduleParallel for ECS systems
- IgnoreComponentEnabledState for iterating all entities regardless of EmitterTag state
- Two-pass architecture: main thread data collection + parallel job processing
- Enableable component transitions for activation/deactivation events

### Integration Points
- Audio bridge system runs AFTER ExponentialDecaySystem and EmitterDeactivationSystem (needs final amplitude values for the frame)
- Voice activation triggered by EmitterTag being enabled (StrikeEvent already consumed by EmitterActivationSystem)
- Voice deactivation triggered by EmitterTag being disabled (by EmitterDeactivationSystem)
- Per-entity AudioSource position must track LocalTransform (managed system or MonoBehaviour)
- Empty `Assets/SoundResonance/Runtime/Audio/` and `Assets/SoundResonance/Runtime/Hybrid/` directories exist for new code

</code_context>

<specifics>
## Specific Ideas

- "We need to make the sounds realistic at all cost" — timbre quality is the top priority for the thesis demo
- Physics-driven synthesis: all audio parameters (frequency, amplitude, decay, timbre) derive from the ECS simulation, not authored
- Sympathetic resonance should be AUDIBLE — hearing TuningForkB start singing without being struck proves the physics works
- The audio must demonstrate that material properties + geometry → realistic sound, which is the thesis argument

</specifics>

<deferred>
## Deferred Ideas

- FMOD integration (AUD-05) — v2 requirement, Unity AudioSource sufficient for thesis
- Variable strike force (INP-02) — v2, but transient intensity scaling is ready for it
- ADSR envelope — simplified to short fade-out release, full ADSR not needed for thesis scope
- Multi-harmonic overtones as v2 requirement (AUD-03) — actually implementing 4 partials now, exceeds original v1 scope

</deferred>

---

*Phase: 03-hybrid-bridge-and-audio-synthesis*
*Context gathered: 2026-03-22*
