---
phase: 02-sympathetic-propagation
plan: 01
subsystem: physics
tags: [ecs, burst, lorentzian, inverse-square, sympathetic-resonance, ijobentity]

requires:
  - phase: 01-single-object-ecs-pipeline
    provides: "EmitterActivationSystem, ExponentialDecaySystem, EmitterDeactivationSystem, ResonanceMath utilities"
provides:
  - "SympatheticPropagationSystem: two-pass emitter-to-receiver propagation with Lorentzian response and inverse-square attenuation"
  - "EmitterSnapshot struct for main-thread emitter data collection"
  - "PlayMode integration tests for sympathetic propagation behavior"
affects: [03-hybrid-bridge-audio, 04-polish-validation]

tech-stack:
  added: []
  patterns:
    - "Two-pass snapshot architecture: main-thread collection + parallel job processing"
    - "IgnoreComponentEnabledState + manual enabled check for mixed active/inactive iteration"
    - "Visibility floor constant for thesis demo convenience"

key-files:
  created:
    - Assets/SoundResonance/Runtime/Systems/SympatheticPropagationSystem.cs
    - Assets/SoundResonance/Tests/PlayMode/SympatheticPropagationTests.cs
  modified:
    - Assets/SoundResonance/Tests/PlayMode/EmitterLifecycleTests.cs

key-decisions:
  - "Two-pass snapshot architecture chosen over ComponentLookup for Burst safety and parallel scheduling"
  - "Direct-only propagation: active emitters skip receiver processing to prevent cascade chains"
  - "Visibility floor (0.001f) applied after driving force > 0 check for immediate thesis demo feedback"
  - "Frequency culling at 2:1 ratio and distance culling at 10m for performance"

patterns-established:
  - "Two-pass snapshot + parallel job: collect data on main thread, process in parallel IJobEntity"
  - "EmitterSnapshot struct pattern for decoupling query results from job execution"

requirements-completed: [ECS-04]

duration: 2min
completed: 2026-03-22
---

# Phase 2 Plan 1: Sympathetic Propagation System Summary

**Two-pass Burst-compiled SympatheticPropagationSystem with Lorentzian frequency response and inverse-square distance attenuation, plus 3 PlayMode integration tests proving frequency selectivity and distance-dependent energy transfer**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-22T10:43:21Z
- **Completed:** 2026-03-22T10:45:49Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- SympatheticPropagationSystem with two-pass architecture: main-thread emitter snapshot collection + parallel Burst-compiled receiver propagation job
- Lorentzian frequency response for physically accurate frequency selectivity
- Inverse-square distance attenuation with frequency and distance culling for performance
- 3 PlayMode integration tests validating matched-frequency response, mismatched rejection, and distance attenuation
- Existing 6 EmitterLifecycleTests updated with LocalTransform for compatibility

## Task Commits

Each task was committed atomically:

1. **Task 1: Create failing SympatheticPropagationTests and update test helpers** - `1c7a739` (test - TDD RED phase)
2. **Task 2: Create SympatheticPropagationSystem making tests pass** - `ad54b50` (feat - TDD GREEN phase)

## Files Created/Modified
- `Assets/SoundResonance/Runtime/Systems/SympatheticPropagationSystem.cs` - Two-pass sympathetic propagation system with EmitterSnapshot struct, Burst-compiled PropagationJob, Lorentzian + inverse-square physics
- `Assets/SoundResonance/Tests/PlayMode/SympatheticPropagationTests.cs` - 3 PlayMode integration tests for sympathetic propagation behavior
- `Assets/SoundResonance/Tests/PlayMode/EmitterLifecycleTests.cs` - Added position overload to CreateResonantEntity, added LocalTransform to all entities

## Decisions Made
- Two-pass snapshot architecture chosen over ComponentLookup for Burst safety and parallel scheduling
- Direct-only propagation: active emitters are skipped in the receiver job to prevent cascade chains
- Visibility floor (0.001f) applied after driving force > 0 check to ensure immediate feedback in thesis demo
- Frequency culling at 2:1 ratio and distance culling at 10m for early-out performance optimization

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Sympathetic propagation system complete and tested
- Ready for 02-02: Extended test scene with tuning fork pair demo and human verification
- All existing Phase 1 tests remain compatible (LocalTransform added to all test entities)

## Self-Check: PASSED

All created files verified on disk. All commit hashes found in git log.

---
*Phase: 02-sympathetic-propagation*
*Completed: 2026-03-22*
