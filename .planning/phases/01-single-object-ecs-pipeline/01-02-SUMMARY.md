---
phase: 01-single-object-ecs-pipeline
plan: 02
subsystem: input-bridge
tags: [unity-input-system, physics-raycast, screen-space-picking, subscene, monobehaviour]

# Dependency graph
requires:
  - phase: 01-01
    provides: ECS systems (activation, decay, deactivation) and DeactivationThreshold pipeline
provides:
  - StrikeInputManager MonoBehaviour for mouse-click-to-ECS bridging
  - Screen-space entity picking (works with SubScene-baked entities)
  - TestSceneSetup editor tool for creating test scenes
  - Test scene with steel/glass/wood resonant objects
affects: [02-propagation, 03-audio-bridge, 04-polish]

# Tech tracking
tech-stack:
  added: []
  patterns: [screen-space entity picking via WorldToScreenPoint, IgnoreComponentEnabledState for enableable queries, SubScene baking workflow]

key-files:
  created:
    - Assets/SoundResonance/Runtime/Input/StrikeInputManager.cs
    - Assets/SoundResonance/Editor/TestSceneSetup.cs
  modified: []

key-decisions:
  - "Screen-space picking instead of Physics.Raycast — SubScene strips colliders during baking"
  - "IgnoreComponentEnabledState required for queries involving disabled enableable components"
  - "EmitterActivationSystem needs IgnoreComponentEnabledState + manual StrikeEvent check because EmitterTag starts disabled"

patterns-established:
  - "EntityQueryOptions.IgnoreComponentEnabledState for queries that must include disabled enableable components"
  - "Screen-space picking pattern: project entity LocalToWorld to screen via Camera.WorldToScreenPoint, find closest to mouse"

# Metrics
duration: 15min
completed: 2026-03-11
---

# Phase 1 Plan 2: Input Bridge and Test Scene Summary

**Screen-space entity picking input bridge with SubScene-baked test scene (steel/glass/wood)**

## Performance

- **Duration:** ~15 min (including checkpoint debugging)
- **Started:** 2026-03-11T16:40:00Z
- **Completed:** 2026-03-11T16:55:00Z
- **Tasks:** 3 (2 auto + 1 checkpoint)
- **Files modified:** 4

## Accomplishments
- StrikeInputManager bridges mouse clicks to ECS StrikeEvent via screen-space entity picking
- TestSceneSetup editor tool creates test scenes with steel/glass/wood objects
- Full pipeline verified in PlayMode: click → activation → decay (steel slow, glass medium, wood instant)
- Additive re-excitation confirmed working on steel and glass

## Task Commits

Each task was committed atomically:

1. **Task 1: Create StrikeInputManager** - `61807f9` (feat)
2. **Task 2: Create test scene editor tool** - `4509295` (feat)
3. **Task 3: PlayMode verification** - `3214b02` (fix — SubScene + enableable component fixes)

## Files Created/Modified
- `Assets/SoundResonance/Runtime/Input/StrikeInputManager.cs` - Screen-space entity picking input bridge
- `Assets/SoundResonance/Editor/TestSceneSetup.cs` - Editor tool for creating test scenes
- `Assets/SoundResonance/Runtime/Systems/EmitterActivationSystem.cs` - Added IgnoreComponentEnabledState

## Decisions Made
- Switched from Physics.Raycast to screen-space entity picking (SubScene strips colliders)
- Added IgnoreComponentEnabledState to both StrikeInputManager query and ActivateJob (enableable components start disabled)
- Manual StrikeEvent enabled check in ActivateJob since query ignores all enabled states

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] SubScene collider stripping broke Physics.Raycast**
- **Found during:** Task 3 (PlayMode verification)
- **Issue:** Objects in SubScene lose colliders during baking, Physics.Raycast returns no hits
- **Fix:** Replaced raycast with screen-space entity picking using Camera.WorldToScreenPoint
- **Files modified:** Assets/SoundResonance/Runtime/Input/StrikeInputManager.cs
- **Verification:** Clicking objects now registers in console log
- **Committed in:** 3214b02

**2. [Rule 3 - Blocking] EntityQuery filtered out entities with disabled enableable components**
- **Found during:** Task 3 (PlayMode verification)
- **Issue:** StrikeEvent starts disabled, query returned zero entities
- **Fix:** Added EntityQueryOptions.IgnoreComponentEnabledState to StrikeInputManager query
- **Files modified:** Assets/SoundResonance/Runtime/Input/StrikeInputManager.cs
- **Committed in:** 3214b02

**3. [Rule 1 - Bug] ActivateJob never matched on first strike**
- **Found during:** Task 3 (PlayMode verification)
- **Issue:** EmitterTag starts disabled, job query filtered out all entities on first strike
- **Fix:** Added IgnoreComponentEnabledState to ActivateJob + manual strikeEnabled check
- **Files modified:** Assets/SoundResonance/Runtime/Systems/EmitterActivationSystem.cs
- **Committed in:** 3214b02

---

**Total deviations:** 3 auto-fixed (3 blocking)
**Impact on plan:** All fixes necessary for correct SubScene + enableable component behavior. No scope creep.

## Issues Encountered
- `in StrikeEvent` + `EnabledRefRW<StrikeEvent>` in same job caused aliasing error — changed to `ref StrikeEvent`

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Full single-entity lifecycle verified in PlayMode
- Ready for Phase 2 (sympathetic propagation) — input bridge works, ECS systems process correctly
- Key learning: all queries involving disabled enableable components need IgnoreComponentEnabledState

---
*Phase: 01-single-object-ecs-pipeline*
*Completed: 2026-03-11*
