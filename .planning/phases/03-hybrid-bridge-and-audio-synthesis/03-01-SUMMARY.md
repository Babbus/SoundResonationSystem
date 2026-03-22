---
phase: 03-hybrid-bridge-and-audio-synthesis
plan: 01
subsystem: audio
tags: [ecs, audio-bridge, double-buffer, voice-pool, harmonic-profile, native-array]

requires:
  - phase: 01-ecs-resonance-pipeline
    provides: ResonantObjectData, EmitterTag, ShapeType, ResonanceMath
  - phase: 02-sympathetic-propagation
    provides: SympatheticPropagationSystem (enables sympathetic activation detection)
provides:
  - VoiceData blittable struct for audio thread consumption
  - HarmonicProfile shape-to-partial-ratio lookups
  - VoicePool slot assignment with amplitude-based stealing
  - ResonanceAudioBridge double-buffered ECS-to-audio data bridge
affects: [03-02-voice-synthesizer, audio-synthesis]

tech-stack:
  added: [NativeArray double-buffer, volatile int thread sync]
  patterns: [lock-free double-buffer, MonoBehaviour-to-ECS bridge via LateUpdate, int-keyed entity pool for testability]

key-files:
  created:
    - Assets/SoundResonance/Runtime/Hybrid/VoiceData.cs
    - Assets/SoundResonance/Runtime/Audio/HarmonicProfile.cs
    - Assets/SoundResonance/Runtime/Hybrid/VoicePool.cs
    - Assets/SoundResonance/Runtime/Hybrid/ResonanceAudioBridge.cs
    - Assets/SoundResonance/Tests/EditMode/VoicePoolTests.cs
    - Assets/SoundResonance/Tests/EditMode/HarmonicProfileTests.cs
  modified: []

key-decisions:
  - "int-keyed VoicePool instead of Entity-keyed: enables EditMode testing without EntityManager"
  - "volatile int readIndex for lock-free double-buffer: no locks/mutexes needed for thread safety"
  - "IsNewStrike detection via frame-over-frame entity set comparison plus EmitterTag.StrikeAmplitude threshold"

patterns-established:
  - "Lock-free double-buffer pattern: main thread writes buffer opposite to readIndex, audio thread reads readIndex buffer, volatile swap provides happens-before guarantee"
  - "MonoBehaviour LateUpdate bridge pattern: collect ECS data after all systems complete, expose via NativeArray for audio thread"

requirements-completed: [AUD-01]

duration: 3min
completed: 2026-03-22
---

# Phase 3 Plan 1: ECS-to-Audio Bridge Summary

**Lock-free double-buffered bridge from ECS emitter data to audio thread via VoiceData NativeArray, with shape-specific harmonic profiles and amplitude-based voice pool stealing**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-22T12:18:59Z
- **Completed:** 2026-03-22T12:22:25Z
- **Tasks:** 2
- **Files created:** 6

## Accomplishments
- VoiceData blittable struct capturing frequency, amplitude, position, shape, and strike/sympathetic flags for each voice slot
- HarmonicProfile providing physics-derived partial ratios (Euler-Bernoulli, Kirchhoff, Donnell) and amplitude weights per shape type
- VoicePool managing 16 voice slots with persistent entity tracking and lowest-amplitude stealing when full
- ResonanceAudioBridge MonoBehaviour collecting ECS data in LateUpdate into a lock-free double-buffered NativeArray

## Task Commits

Each task was committed atomically:

1. **Task 1: Create VoiceData, HarmonicProfile, and VoicePool data layer** - `3c8b663` (feat)
2. **Task 2: Create ResonanceAudioBridge with double-buffer ECS data collection** - `14a9994` (feat)

## Files Created/Modified
- `Assets/SoundResonance/Runtime/Hybrid/VoiceData.cs` - Blittable struct for double-buffer entries (frequency, amplitude, position, shape, flags)
- `Assets/SoundResonance/Runtime/Audio/HarmonicProfile.cs` - Shape-to-harmonic-ratio and amplitude-weight static lookups
- `Assets/SoundResonance/Runtime/Hybrid/VoicePool.cs` - Voice slot assignment, stealing, release with int entity IDs
- `Assets/SoundResonance/Runtime/Hybrid/ResonanceAudioBridge.cs` - MonoBehaviour bridge collecting ECS data into double-buffered NativeArray
- `Assets/SoundResonance/Tests/EditMode/VoicePoolTests.cs` - 8 EditMode tests for pool logic
- `Assets/SoundResonance/Tests/EditMode/HarmonicProfileTests.cs` - 9 EditMode tests for harmonic lookups

## Decisions Made
- Used int entity IDs (Entity.Index) instead of Entity structs for VoicePool dictionary key -- enables EditMode testing without EntityManager dependency
- Lock-free double-buffer with volatile int readIndex -- no locks/mutexes needed, audio thread reads one buffer while main thread writes the other
- IsNewStrike detection via frame-over-frame HashSet comparison of active entity IDs plus EmitterTag.StrikeAmplitude threshold (>0.001) to distinguish direct strikes from sympathetic activation

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Bridge infrastructure complete, ready for Plan 02 (VoiceSynthesizer) to consume GetReadBuffer() on the audio thread
- HarmonicProfile ratios and weights ready for additive synthesis partial generation
- VoiceData struct contains all fields needed for per-partial frequency/amplitude calculation

## Self-Check: PASSED

All 6 created files verified on disk. Both task commits (3c8b663, 14a9994) verified in git log.

---
*Phase: 03-hybrid-bridge-and-audio-synthesis*
*Completed: 2026-03-22*
