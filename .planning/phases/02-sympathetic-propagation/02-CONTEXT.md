# Phase 2: Sympathetic Propagation - Context

**Gathered:** 2026-03-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Striking one object causes nearby objects at matching natural frequencies to begin vibrating sympathetically. This is the thesis headline feature: N-body frequency matching with distance attenuation. Audio synthesis, visual feedback, and debug HUD are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Physical fidelity
- Use real physics (Lorentzian response + inverse-square attenuation) with a visibility floor
- Visibility floor ensures even weak sympathetic coupling produces an observable response during demos
- The floor prevents near-zero responses from being completely invisible before Phase 4 visual feedback exists

### Visibility floor design
- Claude's discretion on whether the floor is a fixed constant or material-derived
- Goal: ensure weak but real sympathetic responses are detectable in amplitude values

### Multi-emitter behavior
- Additive (superposition principle): sum all driving forces linearly from multiple emitters
- Two emitters at the same frequency double the driving strength on a receiver
- No amplitude cap — matches physics superposition

### Chain propagation
- Direct only: only directly struck objects emit and drive receivers
- Sympathetically vibrating objects respond but do NOT become emitters themselves
- No cascade risk, sufficient for thesis scope

### Demo scene layout
- Extend existing Phase 1 test scene (reuse TestSceneSetup.cs infrastructure)
- Tuning fork pair arrangement: two identical-material objects at same frequency placed near each other
- Plus a mismatched-frequency object as control (strike one, match vibrates, mismatch stays still)
- Keep it minimal — distance attenuation verified in tests only, not visual scene layout
- Object count: Claude's discretion (minimum needed to cover success criteria)

### Verification approach
- PlayMode tests only — no debug logs or temporary gizmos
- Follows Phase 1 test infrastructure pattern (CreateResonantEntity + SimulateFrames helpers)
- Monotonic/relative assertion strategy (no exact values, only comparisons)
- Three key test cases:
  1. Matched frequency responds: strike emitter, verify same-frequency receiver gains amplitude > 0
  2. Mismatched frequency rejected: strike emitter, verify different-frequency receiver stays at amplitude near 0
  3. Distance attenuation: same frequency at different distances, verify closer receiver gets more amplitude

### Claude's Discretion
- Propagation system job structure (ComponentLookup vs. two-pass with NativeArray)
- Distance and frequency culling thresholds for performance
- Visibility floor implementation approach (fixed constant vs. material-derived)
- Exact object count and placement in extended test scene
- Multiple simultaneous emitter test (deselected by user — implement if naturally covered by architecture)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### ECS architecture and patterns
- `.planning/codebase/ARCHITECTURE.md` — Layer overview, data flow, enableable component event pattern
- `.planning/codebase/CONVENTIONS.md` — Naming, Burst compatibility rules, ECS patterns

### Phase 1 context and decisions
- `.planning/phases/01-single-object-ecs-pipeline/01-CONTEXT.md` — Prior decisions on strike input, re-excitation, deactivation threshold

### Physics math library
- `Assets/SoundResonance/Runtime/Physics/ResonanceMath.cs` — LorentzianResponse, InverseSquareAttenuation, DrivenOscillatorStep (all needed for propagation)

### Existing systems (pattern reference)
- `Assets/SoundResonance/Runtime/Systems/EmitterActivationSystem.cs` — ISystem + IJobEntity + IgnoreComponentEnabledState pattern to follow

### Requirements
- `.planning/REQUIREMENTS.md` — ECS-04: Sympathetic propagation system specification

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ResonanceMath.LorentzianResponse()`: Computes frequency selectivity between emitter-receiver pairs — directly used by propagation system
- `ResonanceMath.InverseSquareAttenuation()`: Distance falloff — directly used for spatial attenuation
- `ResonanceMath.DrivenOscillatorStep()`: Discrete-time driven oscillator update — applies driving force to receiver amplitude per frame
- `ResonantObjectData`: Already stores NaturalFrequency, QFactor, CurrentAmplitude needed for both emitter and receiver roles
- `EmitterTag`: Enableable component marking active vibrating objects — used to identify emitters

### Established Patterns
- ISystem + nested IJobEntity with ScheduleParallel — standard for all resonance systems
- IgnoreComponentEnabledState + manual enabled check — required for enableable component queries
- Additive energy model — strikes add to CurrentAmplitude, same principle applies to sympathetic driving
- Monotonic/relative test assertions — Phase 1 established this for physics tests with variable DeltaTime

### Integration Points
- PropagationSystem runs AFTER EmitterActivationSystem (needs updated emitter amplitudes) and BEFORE ExponentialDecaySystem (so decay applies to sympathetically-driven amplitude too)
- Extended test scene integrates into existing SubScene baking pipeline
- PlayMode tests extend existing test infrastructure (CreateResonantEntity + SimulateFrames)

</code_context>

<specifics>
## Specific Ideas

- "Tuning fork pair" arrangement for demo — classic physics demonstration setup the user explicitly chose
- Visibility floor concept: user wants physics accuracy but acknowledges that near-zero responses need to be observable for thesis
- Direct-only propagation: user confirmed no cascade, keeping the system simple and controllable for thesis scope
- "Keep it minimal" — user preference for scene simplicity, let tests cover edge cases

</specifics>

<deferred>
## Deferred Ideas

- Chain/cascade propagation (receivers becoming emitters) — could be a future enhancement but out of thesis scope
- Debug gizmo visualization of emitter-receiver connections — Phase 4 (polish)
- Multiple simultaneous emitter test case — not a priority but may be naturally covered

</deferred>

---

*Phase: 02-sympathetic-propagation*
*Context gathered: 2026-03-22*
