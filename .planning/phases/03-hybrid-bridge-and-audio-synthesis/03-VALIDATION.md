---
phase: 3
slug: hybrid-bridge-and-audio-synthesis
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Unity Test Framework (NUnit) — EditMode + PlayMode |
| **Config file** | `Assets/SoundResonance/Tests/PlayMode/SoundResonance.Tests.PlayMode.asmdef` |
| **Quick run command** | Unity Editor > Test Runner > PlayMode > Run Selected |
| **Full suite command** | Unity Editor > Test Runner > Run All |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run EditMode tests for VoicePool/HarmonicProfile logic
- **After every plan wave:** Run full suite (EditMode + PlayMode)
- **Before `/gsd:verify-work`:** Full suite must be green + manual audio verification
- **Max feedback latency:** 10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 1 | AUD-01 | unit | EditMode: verify buffer population from mock data | ❌ W0 | ⬜ pending |
| 03-01-02 | 01 | 1 | AUD-01 | unit | EditMode: verify voice assignment/stealing logic | ❌ W0 | ⬜ pending |
| 03-02-01 | 02 | 1 | AUD-02 | manual-only | Listen for tone on strike, verify no clicks | N/A | ⬜ pending |
| 03-02-02 | 02 | 1 | AUD-02 | manual-only | Strike 3+ objects, verify clean additive mix | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Assets/SoundResonance/Tests/EditMode/VoicePoolTests.cs` — covers voice assignment/stealing logic
- [ ] `Assets/SoundResonance/Tests/EditMode/HarmonicProfileTests.cs` — covers ratio/weight lookups
- [ ] Assembly reference: Tests.EditMode.asmdef needs reference to SoundResonance.Runtime

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Sine tone at natural frequency on strike | AUD-02 | Audio quality is subjective | Strike object in play mode, verify audible tone at correct pitch |
| Volume decay syncs with ECS CurrentAmplitude | AUD-02 | Real-time audio-visual sync | Strike object, observe amplitude visualization fading in sync with audio |
| Multiple simultaneous tones mix cleanly | AUD-02 | Artifact detection requires listening | Strike 3+ objects rapidly, listen for clean additive mixing |
| No clicks/pops on re-strike | AUD-02 | Audio artifacts require listening | Re-strike an already-ringing object, verify smooth transition |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
