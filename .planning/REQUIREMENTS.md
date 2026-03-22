# Requirements: Sound Resonation System

**Defined:** 2026-03-11
**Core Value:** Physically accurate real-time resonance simulation where acoustic behavior emerges entirely from material properties and geometry.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### ECS Runtime

- [ ] **ECS-01**: Emitter activation system processes StrikeEvents, enables EmitterTag, and sets initial amplitude from strike force
- [ ] **ECS-02**: Exponential decay system reduces CurrentAmplitude per-frame using material loss factor and Q-factor
- [ ] **ECS-03**: Emitter deactivation system disables EmitterTag when CurrentAmplitude drops below configurable threshold
- [x] **ECS-04**: Sympathetic propagation system computes Lorentzian frequency response between emitter-receiver pairs with distance and frequency culling

### Input

- [ ] **INP-01**: User can click on a resonant object to strike it via mouse raycast, enabling StrikeEvent with normalized force

### Audio

- [x] **AUD-01**: Hybrid bridge copies ECS amplitude/frequency data to NativeArray shared-buffer in LateUpdate for audio thread consumption
- [x] **AUD-02**: OnAudioFilterRead generates sine waves from shared-buffer amplitude and frequency data at audio sample rate

### Testing

- [ ] **TST-01**: PlayMode integration tests validate the full activation → decay → deactivation lifecycle in ECS
- [ ] **TST-02**: Basic performance validation measures frame time with increasing entity counts

### Polish

- [ ] **POL-01**: Resonant objects visually respond to CurrentAmplitude via scale or color changes
- [ ] **POL-02**: Debug HUD overlay displays runtime frequency, amplitude, and active emitter count

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Input

- **INP-02**: Configurable strike force multiplier for varying impact intensity

### Audio

- **AUD-03**: Multi-harmonic overtones add 2nd and 3rd harmonics for richer timbre
- **AUD-04**: Single AudioSource mixer consolidates all voices into one output
- **AUD-05**: FMOD integration as alternative professional audio backend

## Out of Scope

| Feature | Reason |
|---------|--------|
| Artistic/designer parameters | Contradicts thesis premise — all behavior from material science |
| Full modal analysis | Thesis limited to fundamental frequency mode |
| DSPGraph audio | Experimental/deprecated — OnAudioFilterRead is stable |
| Networked multiplayer | Single-user simulation only |
| VR/AR support | Desktop thesis demo only |
| Mobile optimization | Desktop-only target |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| ECS-01 | Phase 1 | Complete |
| ECS-02 | Phase 1 | Complete |
| ECS-03 | Phase 1 | Complete |
| ECS-04 | Phase 2 | Complete |
| INP-01 | Phase 1 | Complete |
| AUD-01 | Phase 3 | Complete |
| AUD-02 | Phase 3 | Complete |
| TST-01 | Phase 4 | Pending |
| TST-02 | Phase 4 | Pending |
| POL-01 | Phase 4 | Pending |
| POL-02 | Phase 4 | Pending |

**Coverage:**
- v1 requirements: 11 total
- Mapped to phases: 11
- Unmapped: 0

---
*Requirements defined: 2026-03-11*
*Last updated: 2026-03-11 after Phase 1 completion*
