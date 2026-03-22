---
phase: 2
slug: sympathetic-propagation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-22
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | NUnit 3.x via Unity Test Runner |
| **Config file** | `Assets/SoundResonance/Tests/PlayMode/SoundResonance.Tests.PlayMode.asmdef` |
| **Quick run command** | Unity Test Runner > PlayMode > SoundResonance.Tests namespace |
| **Full suite command** | Unity Test Runner > Run All (EditMode + PlayMode) |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run propagation-specific PlayMode tests
- **After every plan wave:** Run full PlayMode + EditMode suite
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 0 | ECS-04 | PlayMode integration | Unity Test Runner > PlayMode | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | ECS-04a | PlayMode integration | Unity Test Runner > PlayMode | ❌ W0 | ⬜ pending |
| 02-01-03 | 01 | 1 | ECS-04b | PlayMode integration | Unity Test Runner > PlayMode | ❌ W0 | ⬜ pending |
| 02-01-04 | 01 | 1 | ECS-04c | PlayMode integration | Unity Test Runner > PlayMode | ❌ W0 | ⬜ pending |
| 02-01-05 | 01 | 2 | ECS-04d | PlayMode integration | Unity Test Runner > PlayMode | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Assets/SoundResonance/Tests/PlayMode/SympatheticPropagationTests.cs` — stubs for ECS-04a/b/c/d
- [ ] Update `CreateResonantEntity` helper to accept `float3 position` parameter (or create overload)
- [ ] Existing `EmitterLifecycleTests.cs` must still pass (regression)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Visual sympathetic vibration in scene | ECS-04 | Requires visual inspection in Unity Editor | Strike one object, observe nearby matched-frequency object begins vibrating |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
