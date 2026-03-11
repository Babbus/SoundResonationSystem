# Roadmap: Sound Resonation System

## Overview

This roadmap delivers a complete real-time sympathetic resonance simulation: from striking an object and watching it decay, through N-body sympathetic propagation, to audible procedural audio output, and finally a polished thesis demonstration. Each phase produces the data the next phase consumes, enforcing a strict dependency chain that keeps each layer independently testable.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3, 4): Planned milestone work
- Decimal phases (e.g., 2.1): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Single-Object ECS Pipeline** - Strike, activate, decay, deactivate a single resonant object
- [ ] **Phase 2: Sympathetic Propagation** - N-body frequency matching drives nearby objects sympathetically
- [ ] **Phase 3: Hybrid Bridge and Audio Synthesis** - Thread-safe ECS-to-audio transfer and procedural sine output
- [ ] **Phase 4: Polish and Validation** - Visual feedback, debug HUD, integration tests, performance validation

## Phase Details

### Phase 1: Single-Object ECS Pipeline
**Goal**: A single resonant object can be struck, vibrate with physically accurate decay, and automatically deactivate -- the complete single-entity lifecycle verifiable without audio
**Depends on**: Nothing (first phase)
**Requirements**: ECS-01, ECS-02, ECS-03, INP-01
**Success Criteria** (what must be TRUE):
  1. User can click a resonant object in PlayMode and its CurrentAmplitude immediately rises from zero
  2. After being struck, the object's CurrentAmplitude decays exponentially over time at a rate determined by its material's loss factor
  3. When CurrentAmplitude drops below the threshold, the EmitterTag is automatically disabled and the object is no longer processed as active
  4. Striking the same object again while it is still ringing re-excites it without causing errors or state corruption
**Plans**: 3 plans

Plans:
- [x] 01-01-PLAN.md -- ECS systems and per-material threshold (component mods + 3 Burst-compiled systems)
- [x] 01-02-PLAN.md -- Input bridge and test scene (StrikeInputManager + steel/glass/wood scene)
- [x] 01-03-PLAN.md -- PlayMode integration tests (6 lifecycle tests)

### Phase 2: Sympathetic Propagation
**Goal**: Striking one object causes nearby objects at matching natural frequencies to begin vibrating sympathetically -- the thesis headline feature
**Depends on**: Phase 1 (requires working activation, decay, and deactivation systems)
**Requirements**: ECS-04
**Success Criteria** (what must be TRUE):
  1. When an object is struck, a nearby object tuned to the same natural frequency begins vibrating without being directly struck
  2. A nearby object tuned to a significantly different frequency does not respond to the struck emitter
  3. Sympathetic response attenuates with distance -- objects farther from the emitter receive less energy
  4. Multiple emitters active simultaneously each independently drive nearby receivers
**Plans**: TBD

Plans:
- [ ] 02-01: TBD
- [ ] 02-02: TBD

### Phase 3: Hybrid Bridge and Audio Synthesis
**Goal**: The ECS simulation produces audible output -- strike an object and hear a sine tone at its natural frequency that decays in sync with the physics
**Depends on**: Phase 1 (requires CurrentAmplitude and NaturalFrequency being written by ECS systems); Phase 2 recommended but not blocking
**Requirements**: AUD-01, AUD-02
**Success Criteria** (what must be TRUE):
  1. Striking an object produces an audible sine tone at its computed natural frequency
  2. The tone's volume decays in sync with the ECS CurrentAmplitude -- when the object deactivates, the tone stops
  3. Multiple struck objects produce simultaneous tones that mix additively without clicks or pops
  4. No audio artifacts (clicks, pops, dropouts) occur during normal operation including re-strikes
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD
- [ ] 03-03: TBD

### Phase 4: Polish and Validation
**Goal**: The thesis demonstration is visually informative, debuggable, and validated with automated tests and performance measurements
**Depends on**: Phase 3 (full end-to-end pipeline must be working)
**Requirements**: POL-01, POL-02, TST-01, TST-02
**Success Criteria** (what must be TRUE):
  1. Resonant objects visually respond to their CurrentAmplitude via scale pulse or color change -- visible amplitude feedback without audio
  2. A debug HUD overlay displays real-time frequency, amplitude, and active emitter count during PlayMode
  3. PlayMode integration tests automatically validate the full strike-activate-decay-deactivate lifecycle in ECS
  4. Performance validation demonstrates that the system maintains 60fps with the target entity count, with frame time measurements logged
**Plans**: TBD

Plans:
- [ ] 04-01: TBD
- [ ] 04-02: TBD
- [ ] 04-03: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Single-Object ECS Pipeline | 3/3 | ✓ Complete | 2026-03-11 |
| 2. Sympathetic Propagation | 0/2 | Not started | - |
| 3. Hybrid Bridge and Audio Synthesis | 0/3 | Not started | - |
| 4. Polish and Validation | 0/3 | Not started | - |
