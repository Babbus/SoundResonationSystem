# Codebase Concerns

**Analysis Date:** 2026-03-11

## Missing Core ECS Systems

**Critical Gap: No Runtime Resonance Systems**
- Issue: The codebase defines all data structures and physics math, but no ECS systems exist to consume them at runtime.
- Files affected: `Assets/SoundResonance/Runtime/Systems/` (directory exists but is empty)
- Impact: Resonant objects cannot be struck, driven, or produce output. The entire resonance simulation pipeline is incomplete.
- Fix approach: Must implement:
  - `EmitterActivationSystem`: Responds to StrikeEvent by enabling EmitterTag and setting initial amplitude
  - `ResonanceSystem`: Reads ResonantObjectData and applies DrivenOscillatorStep math per frame
  - `DecaySystem`: Applies ExponentialDecay when not being driven, disables EmitterTag below threshold
  - `ResonanceAudioBridge`: Reads CurrentAmplitude/Phase and synthesizes audio output

## Physics Model Limitations

**Approximation: Bounding Box Shape Classification**
- Issue: `ShapeClassifier.cs` reduces arbitrary 3D meshes to three canonical shapes (Bar/Plate/Shell) using only bounding box aspect ratios.
- Files: `Assets/SoundResonance/Runtime/Physics/ShapeClassifier.cs` (lines 89-129)
- Impact:
  - Irregular or complex geometries produce inaccurate frequency estimates
  - A tuning fork prong (assumed uniform bar) actually has tapered geometry, reducing stiffness and lowering frequency significantly compared to formula
  - Test acknowledges this: expects 20cm uniform bar to produce ~520Hz, but real A440 tuning fork uses tapered ~9.5cm geometry (test comments, lines 37-46)
  - Objects that don't map cleanly to Bar/Plate/Shell categories default to Plate, which may be wrong
- Scaling limits: Only works for objects with simple aspect ratios. Complex sculptural shapes will have incorrect natural frequencies
- Fix approach:
  - For critical audio elements, use measured frequency data or more precise analytical models
  - Document expected accuracy limits (±30-50% for arbitrary meshes)
  - Consider adding a manual frequency override field to ResonantObjectAuthoring for high-precision objects

**Thresholds Are Heuristic-Driven**
- Issue: ShapeClassifier thresholds hardcoded for common objects, not mathematically justified.
- Files: `Assets/SoundResonance/Runtime/Physics/ShapeClassifier.cs` (lines 70-82)
  - BarAspectThreshold = 3.0 (bar vs. plate distinction)
  - ShellThinThreshold = 3.0 (thin vs. thick)
  - ShellSymmetryMin/Max = 1.2 to 2.0 (curvature detection)
- Impact: Edge cases near thresholds misclassify (e.g., 2.95:1 aspect ratio vs 3.05:1)
- Fix approach: Add test cases for boundary conditions, document rationale for each threshold, consider exposing as inspector fields for testing

## Unsafe Material Properties

**No Validation of Loss Factor Bounds**
- Issue: `MaterialProfileSO.cs` (line 32) clamps LossFactor to [0.00001, 1.0], but unity clamp is not enforced at runtime.
- Files: `Assets/SoundResonance/Runtime/ScriptableObjects/MaterialProfileSO.cs` and `BlittableMaterialData.cs`
- Impact:
  - If LossFactor = 0 (division by zero), QFactor computation in `BlittableMaterialData.Validate()` (line 59) handles this with fallback to 10000, but silent fallback may hide user errors
  - If LossFactor > 1.0 (physically meaningless), Q < 1 produces degenerate damping behavior
- Fix approach: Add explicit validation assertions in BlittableMaterialData.Validate() with descriptive error messages

**Young's Modulus and Density Defaults at 1.0 Pa / 1.0 kg/m^3**
- Issue: `MaterialProfileSO.OnValidate()` (lines 64-66) sets minimums to prevent divide-by-zero, but 1.0 is unrealistic.
- Impact: Silently creates physically nonsensical materials. A density of 1.0 kg/m³ is lighter than air (1.2 kg/m³). Young's Modulus of 1 Pa is impossibly soft.
- Fix approach: Use realistic minimum values (E_min ≈ 1e6 Pa for rubber, density_min ≈ 100 kg/m³ for aerogel) or reject invalid input with warnings

## Frequency Calculation Precision Issues

**Poisson's Ratio Ignored for Bar Calculations**
- Issue: `FrequencyCalculator.CalculateBarFrequency()` (lines 92-98) uses 1D Euler-Bernoulli beam theory, which ignores Poisson's ratio.
- Files: `Assets/SoundResonance/Runtime/Physics/FrequencyCalculator.cs`
- Impact: For very thick bars (where Poisson effects are significant), frequency estimates underestimate damping effects in perpendicular directions. Minimal for thin bars (tuning forks) but visible for stubby bars.
- Fix approach: This is a known limitation of the simple model. Document it. Consider using 3D plate theory for bars with aspect ratio < 5:1.

**Formula Assumes Homogeneous, Isotropic Materials**
- Issue: All frequency calculators treat material as uniform and same in all directions.
- Files: All of `Assets/SoundResonance/Runtime/Physics/FrequencyCalculator.cs`
- Impact: Cannot model:
  - Composite materials (carbon fiber, laminated wood)
  - Anisotropic materials (directional grain, crystal structure)
  - Layered structures (bimetal strips)
- Fix approach: Out of scope for current phase. Document as limitation. If needed in future, requires per-layer material data.

## Test Coverage Gaps

**No PlayMode Tests for ECS Systems**
- Issue: All tests are EditMode (unit math tests). No integration tests for actual ECS behavior.
- Files: `Assets/SoundResonance/Tests/EditMode/` (FrequencyCalculatorTests.cs, ResonanceMathTests.cs, ShapeClassifierTests.cs)
- Impact: Cannot verify:
  - Component baking works correctly
  - ResonanceSystem reads/writes components properly
  - Amplitude buildup/decay simulation matches math
  - Audio output synthesis is correct
  - Frame-rate independence of exponential smoothing
- Fix approach: When systems are implemented, add PlayMode tests in `Assets/SoundResonance/Tests/PlayMode/` covering:
  - Strike event flow (StrikeEvent enabled → EmitterTag enabled → amplitude rises)
  - Decay behavior after driving stops
  - Distance attenuation (multiple emitters heard at different volumes)

**No Tests for Material Database**
- Issue: `MaterialDatabase.cs` has no tests.
- Files: `Assets/SoundResonance/Runtime/ScriptableObjects/MaterialDatabase.cs`
- Impact: FindByName() method untested; could fail with null returns on typos or case sensitivity issues
- Fix approach: Add test covering:
  - FindByName() with exact match
  - FindByName() with case mismatch
  - FindByName() with nonexistent material
  - GetPresetData() returns correct count and non-null materials

**No Tests for NoteNameHelper Edge Cases**
- Issue: `NoteNameHelper.cs` handles negative frequencies and extreme ranges, but tests are missing.
- Files: `Assets/SoundResonance/Runtime/Physics/NoteNameHelper.cs` (lines 30-50)
- Impact: Could fail on:
  - Extremely low frequencies (< 1 Hz, very negative MIDI notes)
  - NaN or infinity
  - Negative frequencies (should not happen, but not tested)
- Fix approach: Add tests for boundary conditions and invalid inputs

## Scaling and Performance Concerns

**No LOD (Level-of-Detail) Strategy for Many Objects**
- Issue: ResonanceSystem will iterate over all entities with ResonantObjectData every frame.
- Files: Will be in future `Assets/SoundResonance/Runtime/Systems/ResonanceSystem.cs` (not yet written)
- Impact: With 1000+ resonant objects, the per-frame math becomes expensive:
  - Each object: 1 LorentzianResponse call, 1 DrivenOscillatorStep call, 1 ExponentialDecay call
  - All using sqrt(), exp(), log2() — expensive operations
  - Even Burst-compiled, will bottleneck as scale increases
- Scaling limits: Expected to handle ~100 simultaneous emitters at 60fps. Beyond that, frame rate will drop.
- Fix approach:
  - Implement emission-based culling: Disable objects with amplitude < AmplitudeThreshold (0.0001) to skip processing
  - Consider distance-based LOD: Objects far from listener don't need per-frame updates (could use OnDemandEvaluation)
  - Profile actual frame cost once systems are written

**No Burst Compilation Path for NoteNameHelper**
- Issue: `NoteNameHelper.FrequencyToNoteName()` uses string formatting and cannot be Burst-compiled.
- Files: `Assets/SoundResonance/Runtime/Physics/NoteNameHelper.cs`
- Impact: This is called in `ResonantObjectAuthoringEditor.OnInspectorGUI()` (line 63), which is Editor-only, so no runtime cost. However, if ever used in a runtime UI system, it will cause Burst compilation failures or unmanaged code calls.
- Fix approach: Keep in non-Burst namespace. If runtime note display is needed, create a Burst-compatible variant that returns MIDI note number + cents deviation as integers.

## Dependency and Integration Issues

**Incomplete Material Property Ecosystem**
- Issue: Material properties are defined, but no way to add custom materials at runtime or verify material validity across the codebase.
- Files: `Assets/SoundResonance/Runtime/ScriptableObjects/MaterialDatabase.cs` and `MaterialProfileSO.cs`
- Impact: Designers must create MaterialProfileSO assets manually; typos or unrealistic values go unchecked until runtime when physics looks wrong
- Fix approach: Add MaterialValidator utility that checks:
  - All material properties in expected ranges
  - Young's Modulus > stiffness of rubber (0.001e9 Pa)
  - Density > density of aerogel (1 kg/m³)
  - Loss factor in [0.00001, 1.0]
  - Poisson ratio in [0, 0.5]

**No Audio Bridge Contract Defined**
- Issue: Components and systems expect to pass data to audio synthesis, but `ResonanceAudioBridge` does not exist yet.
- Files: Referenced in `ResonantObjectData.cs` (line 33), implied in documentation throughout
- Impact: Unclear what the audio output format should be:
  - Single sine wave per emitter, or full harmonic series?
  - Fixed sample rate (44.1kHz, 48kHz)?
  - How many voices can play simultaneously?
  - Does phase continuity matter (for glitch-free synthesis)?
- Fix approach: Define interface contract:
  ```csharp
  public interface IResonanceAudioBridge
  {
      void SetEmitterAmplitude(Entity emitterEntity, float amplitude, float phase);
      void EnableEmitter(Entity emitterEntity);
      void DisableEmitter(Entity emitterEntity);
  }
  ```

## Minor Code Quality Concerns

**Magic Numbers in ShapeClassifier**
- Issue: Sort in `SortDescending()` (lines 134-136) uses hardcoded comparison order for 3-element array.
- Files: `Assets/SoundResonance/Runtime/Physics/ShapeClassifier.cs`
- Impact: Works correctly but is not obvious without understanding bubble sort. Could be replaced with `math.sort()` if available in newer Unity.Mathematics versions.
- Fix approach: Add comment explaining bubble sort for fixed 3 elements, or refactor to use math library function

**Negative MIDI Note Handling Complexity**
- Issue: `NoteNameHelper.FrequencyToNoteName()` (lines 44-45) has special case logic for negative MIDI notes.
- Files: `Assets/SoundResonance/Runtime/Physics/NoteNameHelper.cs`
- Impact: Code is correct but fragile. If formula changes, this could break silently.
- Fix approach: Add test cases specifically for very low frequencies (< 1 Hz) that produce negative MIDI notes

**Editor Tool Doesn't Validate Asset Paths**
- Issue: `MaterialPresetGenerator.GeneratePresets()` (line 28) assumes `PresetPath` directory exists or can be created.
- Files: `Assets/SoundResonance/Editor/Inspectors/MaterialPresetGenerator.cs`
- Impact: If PresetPath is not writable (permissions, immutable directory), tool fails with unhelpful error.
- Fix approach: Add try-catch with descriptive error message, validate directory before writing

## Physics Model Validation

**Frequency Validation Test Is Generous but Not Grounded in Theory**
- Issue: `FrequencyCalculatorTests.SteelBar_TuningForkDimensions_ProducesReasonableFrequency()` (lines 37-56) allows 200-800Hz range for a bar that should produce ~520Hz.
- Files: `Assets/SoundResonance/Tests/EditMode/FrequencyCalculatorTests.cs`
- Impact: A frequency error of ±53% is large. Test would pass for a formula that's off by factor of 2. This is acknowledged in comments but means the test is more of a sanity check than a validation.
- Fix approach: Either:
  - Tighten tolerance to ±20% if formula is well-validated against real measurements
  - Or expand comments to explain why ±53% tolerance is acceptable for this use case

---

*Concerns audit: 2026-03-11*
