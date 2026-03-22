---
phase: 02-sympathetic-propagation
plan: 02
subsystem: physics, visualization
tags: [ecs, urp, amplitude-visualization, test-scene, sympathetic-resonance]

requires:
  - phase: 02-sympathetic-propagation/plan-01
    provides: "SympatheticPropagationSystem, EmitterSnapshot, SympatheticPropagationTests"
provides:
  - "Extended test scene with tuning fork pair demo and mismatched control"
  - "AmplitudeVisualizationSystem: per-entity color mapping via URPMaterialPropertyBaseColor"
  - "Tuned propagation: ResponseThreshold replaces VisibilityFloor, PropagationTimeScale for faster coupling"
affects: [03-hybrid-bridge-audio]

tech-stack:
  added: [Unity.Entities.Graphics]
  patterns:
    - "URPMaterialPropertyBaseColor for per-entity color override in ECS"
    - "Saturating exponential curve for amplitude-to-visual mapping"

key-files:
  created:
    - Assets/SoundResonance/Runtime/Systems/AmplitudeVisualizationSystem.cs
  modified:
    - Assets/SoundResonance/Editor/TestSceneSetup.cs
    - Assets/SoundResonance/Runtime/Systems/SympatheticPropagationSystem.cs
    - Assets/SoundResonance/Runtime/Authoring/ResonantObjectAuthoring.cs
    - Assets/SoundResonance/Runtime/SoundResonance.Runtime.asmdef

key-decisions:
  - "Removed VisibilityFloor hack — caused mismatched frequencies to respond falsely"
  - "Added ResponseThreshold (0.05) to reject Lorentzian fat-tail responses from mismatched frequencies"
  - "Added PropagationTimeScale (50x) to accelerate sympathetic coupling for real-time demo"
  - "URPMaterialPropertyBaseColor baked into all resonant entities for per-entity color override"
  - "Two-stage color mapping: gray (idle) → orange (low) → red (high) via saturating exponential"

patterns-established:
  - "AmplitudeVisualizationSystem pattern: ECS data → visual feedback via material property override"
  - "ResponseThreshold + PropagationTimeScale for demo-tuned physics behavior"

requirements-completed: [ECS-04]

duration: 8min
completed: 2026-03-22
---

# Phase 2 Plan 2: Test Scene Demo + Propagation Tuning Summary

**Tuning fork pair demo scene with amplitude-to-color visualization, plus propagation tuning fixes for frequency rejection and coupling speed**

## Performance

- **Duration:** ~8 min (including human verification and post-checkpoint fixes)
- **Started:** 2026-03-22T10:50:00Z
- **Completed:** 2026-03-22T11:00:00Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments
- Test scene extended with TuningForkA + TuningForkB (same steel material, 1m apart) and MismatchedControl (wood)
- Fixed false positive: mismatched frequencies no longer respond (ResponseThreshold replaces VisibilityFloor)
- Sympathetic coupling builds up 50x faster via PropagationTimeScale — visible within seconds
- AmplitudeVisualizationSystem maps amplitude to color (gray → orange → red) via URPMaterialPropertyBaseColor
- Human-verified in PlayMode: sympathetic propagation visually confirmed

## Task Commits

Each task was committed atomically:

1. **Task 1: Extend TestSceneSetup with tuning fork pair** - `6fae222` (feat)
2. **Task 2: Human verification** - approved by user after post-checkpoint tuning fixes
3. **Post-checkpoint fixes** - `b534ec8` (fix: propagation tuning + visualization system)

## Files Created/Modified
- `Assets/SoundResonance/Runtime/Systems/AmplitudeVisualizationSystem.cs` - Per-entity color mapping from CurrentAmplitude via URPMaterialPropertyBaseColor
- `Assets/SoundResonance/Editor/TestSceneSetup.cs` - Extended with tuning fork pair + mismatched control objects
- `Assets/SoundResonance/Runtime/Systems/SympatheticPropagationSystem.cs` - ResponseThreshold, PropagationTimeScale, removed VisibilityFloor
- `Assets/SoundResonance/Runtime/Authoring/ResonantObjectAuthoring.cs` - Added URPMaterialPropertyBaseColor to baker
- `Assets/SoundResonance/Runtime/SoundResonance.Runtime.asmdef` - Added Unity.Entities.Graphics reference

## Decisions Made
- Removed VisibilityFloor (0.001f) — it was causing mismatched frequencies to get a false amplitude boost
- Added ResponseThreshold (0.05f) to reject Lorentzian fat-tail responses, ensuring only genuinely matched frequencies couple
- Added PropagationTimeScale (50f) to accelerate coupling buildup from physics-accurate ~7.2s tau to demo-friendly sub-second response
- Used URPMaterialPropertyBaseColor for visualization since project uses URP + Entities Graphics

## Deviations from Plan

### Auto-fixed Issues

**1. Propagation tuning — mismatched frequency false positive**
- **Found during:** Human verification checkpoint
- **Issue:** MismatchedControl showed amplitude ~0.0005 due to VisibilityFloor boosting Lorentzian fat-tail response
- **Fix:** Removed VisibilityFloor, added ResponseThreshold (0.05) to reject weak responses
- **Verification:** User confirmed MismatchedControl stays gray (0 amplitude) after fix
- **Committed in:** b534ec8

**2. Propagation speed — amplitude too low for demo**
- **Found during:** Human verification checkpoint
- **Issue:** Steel Q=10000 gives tau≈7.2s, making sympathetic buildup imperceptibly slow (0.006 amplitude)
- **Fix:** Added PropagationTimeScale (50x) to DrivenOscillatorStep dt parameter
- **Verification:** User confirmed visible amplitude buildup and color change
- **Committed in:** b534ec8

**3. Visualization — user requested amplitude color feedback**
- **Found during:** Human verification checkpoint
- **Issue:** No visual indication of amplitude state — required inspecting ECS components manually
- **Fix:** Created AmplitudeVisualizationSystem + baked URPMaterialPropertyBaseColor into entities
- **Verification:** User confirmed color changes visible in PlayMode ("perfect")
- **Committed in:** b534ec8

---

**Total deviations:** 3 auto-fixed (all from human verification feedback)
**Impact on plan:** All fixes directly improve the thesis demo quality. Visualization is essential for demonstrating resonance behavior.

## Issues Encountered
- User had to manually place entities in a SubScene (known requirement from Phase 1)

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Sympathetic propagation fully working with visual feedback
- Ready for Phase 3: Hybrid bridge and audio synthesis
- Audio bridge can read CurrentAmplitude and NaturalFrequency from ECS for sound generation

## Self-Check: PASSED

All created files verified. All commits present in git log.

---
*Phase: 02-sympathetic-propagation*
*Completed: 2026-03-22*
