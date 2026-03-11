# Phase 1: Single-Object ECS Pipeline - Context

**Gathered:** 2026-03-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Strike a single resonant object, vibrate with physically accurate exponential decay driven by material properties, and automatically deactivate when amplitude drops below threshold. The complete single-entity lifecycle, verifiable without audio. Sympathetic propagation, audio synthesis, and visual feedback are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Strike input
- Fixed normalized force per click — no variable strike intensity
- Object-level detection only — click location on surface does not matter
- Every click registers as a separate strike — no debouncing
- No visual feedback on strike in this phase (Phase 4 handles polish)
- Raycast targeting method: Claude's discretion (physics colliders vs alternatives in DOTS)

### Re-excitation behavior
- Additive energy: each strike adds energy to current amplitude
- No amplitude cap — amplitude can grow unbounded from rapid strikes
- No decay reset or timer — exponential decay operates continuously on current amplitude value
- Physics-accurate: each strike introduces new energy, decay is a continuous physical process on total amplitude

### Material data authoring
- Per-entity ECS components store material properties directly (no ScriptableObject presets)
- Natural frequency computed from physics (Young's modulus, density, geometry) — not authored directly
- Geometric model for frequency computation: needs research (researcher should investigate feasible models given Unity DOTS capabilities)
- Test scene should include 2-3 different materials (e.g., steel, glass, wood) to verify different decay behaviors

### Deactivation threshold
- Per-material threshold value — each material defines its own amplitude cutoff
- Immediate ECS deactivation when amplitude drops below threshold
- ADSR envelope with fast release deferred to Phase 3 audio layer — ECS handles physics cutoff, audio handles smooth release
- Re-activation after deactivation: same path as first strike (enable EmitterTag, add energy)

### Claude's Discretion
- Raycast implementation approach for DOTS/ECS
- Geometric model for natural frequency computation (pending research)
- ECS component layout and system scheduling
- Exact exponential decay formula parameterization

</decisions>

<specifics>
## Specific Ideas

- "Think about real physics" — the user expects physically grounded behavior throughout, not game-like approximations
- ADSR concept mentioned for audio smoothing — captures the user's expectation that the system should sound natural when audio is added in Phase 3
- Multiple materials in test scene to demonstrate that different materials produce visibly different decay characteristics

</specifics>

<deferred>
## Deferred Ideas

- ADSR envelope for audio release — Phase 3 (audio synthesis)
- Variable strike force (INP-02) — v2 requirement
- Visual amplitude feedback — Phase 4

</deferred>

---

*Phase: 01-single-object-ecs-pipeline*
*Context gathered: 2026-03-11*
