---
phase: 01-single-object-ecs-pipeline
plan: 01
subsystem: ecs-pipeline
tags: [unity-ecs, burst, ijobentity, isystem, exponential-decay, enableable-component]

# Dependency graph
requires:
  - phase: none
    provides: initial project structure with components and authoring
provides:
  - EmitterActivationSystem consuming StrikeEvent and enabling EmitterTag
  - ExponentialDecaySystem applying per-frame amplitude decay
  - EmitterDeactivationSystem disabling emitters below per-material threshold
  - DeactivationThreshold field through full component pipeline
affects: [01-02-edit-mode-tests, 01-03-strike-authoring, 02-propagation, 03-audio-bridge]

# Tech tracking
tech-stack:
  added: []
  patterns: [BurstCompile ISystem + IJobEntity, EnabledRefRW toggle, ScheduleParallel, per-material threshold pipeline]

key-files:
  created:
    - Assets/SoundResonance/Runtime/Systems/EmitterActivationSystem.cs
    - Assets/SoundResonance/Runtime/Systems/ExponentialDecaySystem.cs
    - Assets/SoundResonance/Runtime/Systems/EmitterDeactivationSystem.cs
  modified:
    - Assets/SoundResonance/Runtime/Components/ResonantObjectData.cs
    - Assets/SoundResonance/Runtime/ScriptableObjects/MaterialProfileSO.cs
    - Assets/SoundResonance/Runtime/ScriptableObjects/BlittableMaterialData.cs
    - Assets/SoundResonance/Runtime/Authoring/ResonantObjectAuthoring.cs

key-decisions:
  - "Per-material DeactivationThreshold assigned after Validate() in GetBlittableData() to prevent 0.0f default"
  - "Decay formula kept unsimplified (2*PI*f0 / 2*Q) for readability matching ResonanceMath reference"
  - "DeactivateJob resets Phase to 0 alongside CurrentAmplitude for clean re-activation state"

patterns-established:
  - "ISystem + nested IJobEntity with ScheduleParallel for all resonance systems"
  - "UpdateAfter chain for system ordering within SimulationSystemGroup"
  - "EnabledRefRW<T> for zero-cost component enable/disable toggling"

# Metrics
duration: 2min
completed: 2026-03-11
---

# Phase 1 Plan 1: Core ECS Systems Summary

**Three Burst-compiled ECS systems (activation, decay, deactivation) with per-material DeactivationThreshold piped through MaterialProfileSO to baker**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-11T16:32:53Z
- **Completed:** 2026-03-11T16:34:25Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- Added DeactivationThreshold field through the full data pipeline (MaterialProfileSO -> BlittableMaterialData -> Baker -> ResonantObjectData)
- Created EmitterActivationSystem: consumes StrikeEvent, writes EmitterTag.StrikeAmplitude, enables EmitterTag, adds energy additively
- Created ExponentialDecaySystem: per-frame exponential decay using material Q-factor and natural frequency
- Created EmitterDeactivationSystem: disables EmitterTag below per-material threshold with clean state reset

## Task Commits

Each task was committed atomically:

1. **Task 1: Add DeactivationThreshold to component pipeline** - `635443c` (feat)
2. **Task 2: Create three ECS runtime systems** - `fef39b4` (feat)

## Files Created/Modified
- `Assets/SoundResonance/Runtime/Components/ResonantObjectData.cs` - Added DeactivationThreshold field
- `Assets/SoundResonance/Runtime/ScriptableObjects/MaterialProfileSO.cs` - Added deactivationThreshold serialized field with Range, assigned in GetBlittableData()
- `Assets/SoundResonance/Runtime/ScriptableObjects/BlittableMaterialData.cs` - Added DeactivationThreshold field
- `Assets/SoundResonance/Runtime/Authoring/ResonantObjectAuthoring.cs` - Baker pipes DeactivationThreshold to ECS component
- `Assets/SoundResonance/Runtime/Systems/EmitterActivationSystem.cs` - Consumes StrikeEvent, enables EmitterTag, additive energy
- `Assets/SoundResonance/Runtime/Systems/ExponentialDecaySystem.cs` - Per-frame exponential amplitude decay
- `Assets/SoundResonance/Runtime/Systems/EmitterDeactivationSystem.cs` - Disables EmitterTag below per-material threshold

## Decisions Made
- DeactivationThreshold assigned after Validate() in GetBlittableData() to ensure it is not overwritten or zeroed by Validate()
- Kept decay formula in unsimplified form `(2*PI*f0) / (2*Q)` for direct correspondence with ResonanceMath.ExponentialDecay documentation
- DeactivateJob resets both CurrentAmplitude and Phase to 0 for clean re-activation state (prevents phase glitches on re-strike)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All three systems ready for edit-mode testing in Plan 01-02
- StrikeAuthoring (Plan 01-03) can enable StrikeEvent to trigger the full pipeline
- DeactivationThreshold pipeline ready for per-material tuning in inspector

---
*Phase: 01-single-object-ecs-pipeline*
*Completed: 2026-03-11*
