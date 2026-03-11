# Sound Resonation System

## What This Is

A physics-based sympathetic resonance simulation system built in Unity 6 DOTS/ECS for an academic thesis. It models how vibrating objects transfer energy to nearby objects at matching natural frequencies — using real material science constants (Young's modulus, density, loss factor) rather than artistic parameters. Designed to run in real-time with Burst-compiled performance.

## Core Value

Physically accurate resonance behavior that **emerges from material properties and geometry** — strike a steel bar and hear it ring, watch a nearby matching-frequency object vibrate sympathetically, all computed from real physics equations.

## Requirements

### Validated

- ✓ Shape classification from mesh bounding box (Bar/Plate/Shell) — existing
- ✓ Natural frequency calculation using Euler-Bernoulli, Kirchhoff, and Donnell theories — existing
- ✓ 10 material presets with real physical properties (Steel, Aluminum, Glass, Brass, Copper, Oak, Spruce, Concrete, Rubber, Ceramic) — existing
- ✓ Lorentzian frequency response, exponential decay, inverse-square attenuation, driven oscillator step — existing
- ✓ ECS components: ResonantObjectData, EmitterTag (enableable), StrikeEvent (enableable) — existing
- ✓ Authoring + Baker pipeline: designer assigns material, baker computes frequency/shape/Q at edit-time — existing
- ✓ Custom inspector with live frequency, musical note, Q-factor display — existing
- ✓ Edit-mode unit tests for ShapeClassifier, FrequencyCalculator, ResonanceMath — existing

### Active

- [ ] ECS runtime systems: EmitterActivation, ResonancePropagation, EmitterDeactivation
- [ ] Raycast-based strike input system (click to strike objects with configurable force)
- [ ] Procedural audio synthesis via Unity AudioSource (OnAudioFilterRead sine wave generation)
- [ ] Hybrid ECS-to-MonoBehaviour bridge for audio output
- [ ] PlayMode integration tests for runtime simulation
- [ ] Basic performance validation (entity count scaling)
- [ ] Optional FMOD integration as alternative audio backend

### Out of Scope

- Artistic/designer-tunable parameters — all behavior must derive from material science
- Networked multiplayer resonance — single-player simulation only
- Complex mesh-based modal analysis — using bounding box approximation is sufficient for thesis
- Mobile platform optimization — desktop-only target

## Context

- Academic thesis project: "Real-Time Sympathetic Resonance Simulation Using Unity DOTS"
- Documentation in Turkish (system-architecture-tr.md, thesis-abstract-tr.md)
- 5-layer architecture already established: Data → Physics → ECS Components → Authoring → Editor Tools
- All physics math is Burst-compiled and frame-rate independent
- Edit-time baking means expensive calculations happen once, not per-frame
- Empty folders exist for planned work: Runtime/Systems/, Runtime/Audio/, Runtime/Hybrid/

## Constraints

- **Engine**: Unity 6.0.3.9f1 with Entities 1.3.9 — locked for thesis consistency
- **Burst compatibility**: All runtime code must use blittable structs, no managed types in jobs
- **Frame-rate independence**: Simulation must produce consistent results regardless of frame rate
- **Real physics**: Material properties from ASM International and Kinsler & Frey reference data
- **Performance**: Must handle tens of resonant objects at 60fps minimum

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| DOTS/ECS over MonoBehaviour | Cache-friendly iteration over many resonant objects, Burst compilation | ✓ Good |
| Bounding box shape classification | Sufficient approximation for thesis scope, avoids complex modal analysis | ✓ Good |
| Edit-time baking of physics | Frequency/shape don't change at runtime, compute once | ✓ Good |
| IEnableableComponent for events | Zero-cost state transitions without structural chunk changes | ✓ Good |
| Unity AudioSource first, FMOD later | Start simple, add professional audio backend as optional phase | — Pending |
| Raycast click for strike input | Simple, deterministic input for thesis demonstration | — Pending |

---
*Last updated: 2026-03-11 after initialization*
