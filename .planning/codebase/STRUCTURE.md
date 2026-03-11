# Codebase Structure

**Analysis Date:** 2026-03-11

## Directory Layout

```
Assets/SoundResonance/
├── Runtime/                                 # All gameplay + framework code
│   ├── Authoring/
│   │   └── ResonantObjectAuthoring.cs      # MonoBehaviour for designer configuration; contains Baker
│   ├── Components/
│   │   ├── ResonantObjectData.cs           # Core ECS component: physics state (frequency, Q, amplitude)
│   │   ├── EmitterTag.cs                   # Enableable tag: marks currently-vibrating objects
│   │   └── StrikeEvent.cs                  # Enableable event: signals a strike with force amplitude
│   ├── Physics/
│   │   ├── FrequencyCalculator.cs          # Analytical formulas for bar/plate/shell frequencies
│   │   ├── ShapeClassifier.cs              # Bounding-box shape classification into 3 canonical types
│   │   ├── ResonanceMath.cs                # Driven damped oscillator equations; Burst-compiled
│   │   └── NoteNameHelper.cs               # Frequency → musical note name converter
│   ├── ScriptableObjects/
│   │   ├── MaterialProfileSO.cs            # Single material's physical properties (E, ρ, η, ν)
│   │   ├── MaterialDatabase.cs             # Registry of all material presets; provides lookup
│   │   ├── BlittableMaterialData.cs        # Burst-safe struct copy of material data
│   │   └── Presets/                        # (Directory for preset material assets, not populated yet)
│   ├── Hybrid/                             # (Empty; reserved for future MonoBehaviour ↔ ECS bridges)
│   ├── Audio/                              # (Empty; reserved for future audio synthesis layer)
│   └── Systems/                            # (Empty; reserved for future ISystem implementations)
├── Editor/                                  # Editor-only tools and inspectors
│   ├── Inspectors/
│   │   ├── ResonantObjectAuthoringEditor.cs  # Custom inspector: displays computed f0, note name, Q
│   │   └── MaterialPresetGenerator.cs        # Tool for batch-creating MaterialProfileSO assets
│   └── FrequencyAnalyzer/                    # (Empty; reserved for future frequency analysis tools)
└── Tests/
    ├── EditMode/
    │   ├── FrequencyCalculatorTests.cs      # Unit tests for frequency formulas
    │   ├── ShapeClassifierTests.cs          # Unit tests for shape classification logic
    │   └── ResonanceMathTests.cs            # Unit tests for oscillator equations
    └── PlayMode/                             # (Empty; reserved for future integration tests)
```

## Directory Purposes

**Runtime/Authoring/:**
- Purpose: Designer-facing component and Baker that drives the ECS population
- Contains: Single MonoBehaviour (ResonantObjectAuthoring) that acts as a data declaration
- Key files: `ResonantObjectAuthoring.cs` (contains both the component and its nested Baker class)
- Design intent: Keep minimal — just holds a reference to MaterialProfileSO; all computation logic lives in Baker or math library

**Runtime/Components/:**
- Purpose: ECS component definitions for runtime state and events
- Contains: Three struct components (IComponentData)
- Key files:
  - `ResonantObjectData.cs`: Holds all baked physics constants (frequency, Q-factor, shape) + runtime state (amplitude, phase)
  - `EmitterTag.cs`: IEnableableComponent for toggleable vibration state without structural changes
  - `StrikeEvent.cs`: IEnableableComponent for one-shot event mechanism
- Design pattern: All components are blittable structs; no nested references

**Runtime/Physics/:**
- Purpose: Pure mathematics library for resonance simulation — completely stateless, Burst-compatible
- Contains: Four static utility classes with mathematical functions
- Key files:
  - `FrequencyCalculator.cs`: Static methods for bar/plate/shell frequency formulas with comprehensive documentation
  - `ShapeClassifier.cs`: Static method to classify bounding box extents into vibration shape types + thresholds
  - `ResonanceMath.cs`: Driven damped harmonic oscillator: Lorentzian response, exponential decay, discrete time steps
  - `NoteNameHelper.cs`: Frequency to MIDI note conversion using equal temperament (A4=440Hz)
- Design pattern: All marked [BurstCompile]; no MonoBehaviour, no state, no allocations
- Reusability: Can be used in Baker (edit-time), future systems (runtime), tests, and inspectors

**Runtime/ScriptableObjects/:**
- Purpose: Configuration and material property storage
- Contains: Three classes — one ScriptableObject type and two structs
- Key files:
  - `MaterialProfileSO.cs`: Editable ScriptableObject with 4 physical properties (E, ρ, η, ν) + 2 read-only derived fields (Q, speed of sound)
  - `BlittableMaterialData.cs`: Burst-safe struct copy — contains same data as MaterialProfileSO but suitable for job parameters and struct copying
  - `MaterialDatabase.cs`: Singleton registry with FindByName() lookup; also contains static GetPresetData() for editor tools
- Design pattern: Material data flows from ScriptableObject (editor) → BlittableMaterialData (runtime) → all calculators
- Coupling: Minimal — MaterialProfileSO only depends on UnityEngine; BlittableMaterialData has no dependencies

**Runtime/Hybrid/ & Runtime/Audio/:**
- Purpose: Reserved for future expansion
- Hybrid: MonoBehaviour ↔ ECS bridges if needed for input handling or animation systems
- Audio: Audio synthesis and DSP layer to convert vibration data into audio samples

**Runtime/Systems/:**
- Purpose: Reserved for future ECS runtime systems
- Expected implementations:
  - EmitterActivationSystem: Consumes StrikeEvent, enables EmitterTag, updates amplitude
  - ResonanceSystem: Updates amplitude each frame via ResonanceMath.ExponentialDecay() and DrivenOscillatorStep()
  - EmitterDeactivationSystem: Disables EmitterTag when amplitude falls below threshold
  - AudioBridge: Reads vibration state and synthesizes audio output

**Editor/Inspectors/:**
- Purpose: Editor UI and developer tools
- Key files:
  - `ResonantObjectAuthoringEditor.cs`: Draws computed resonance properties in Inspector; re-calculates on mesh/material change
  - `MaterialPresetGenerator.cs`: Command to batch-create material preset assets from hardcoded preset array
- Design: Read-only displays; all computation via public static methods from Physics library

**Editor/FrequencyAnalyzer/:**
- Purpose: Reserved for future editor tools
- Expected: Spectrum visualization, frequency response plots, bulk material property editing

**Tests/EditMode/:**
- Purpose: Unit tests for physics calculations
- Key files:
  - `ResonanceMathTests.cs`: Validates Lorentzian response, decay, oscillator step
  - `FrequencyCalculatorTests.cs`: Tests frequency formulas for known shapes
  - `ShapeClassifierTests.cs`: Tests classification thresholds against edge cases
- Framework: NUnit via Unity Test Framework
- Coverage: Physics library only; no ECS or Baker logic (those require baking system)

**Tests/PlayMode/:**
- Purpose: Reserved for future integration tests
- Expected: Full baking → runtime → audio output validation

## Key File Locations

**Entry Points:**

- `Assets/SoundResonance/Runtime/Authoring/ResonantObjectAuthoring.cs`: The component that triggers baking; designers add this to GameObjects in SubScenes
- `Assets/SoundResonance/Runtime/Authoring/ResonantObjectAuthoring.cs` (nested class): ResonantObjectBaker — the baking implementation
- `Assets/SoundResonance/Editor/Inspectors/ResonantObjectAuthoringEditor.cs`: Custom Inspector for authoring component

**Configuration:**

- `Assets/SoundResonance/Runtime/ScriptableObjects/MaterialProfileSO.cs`: Create instances for each material (Steel, Glass, Wood, etc.) via Editor menu "Sound Resonance/Material Profile"
- `Assets/SoundResonance/Runtime/ScriptableObjects/MaterialDatabase.cs`: Singleton database asset (create via "Sound Resonance/Material Database")
- (No .json, .xml, or .yaml config files — all configuration is via ScriptableObjects)

**Core Logic:**

- Physics calculations: `Assets/SoundResonance/Runtime/Physics/ResonanceMath.cs`, `FrequencyCalculator.cs`, `ShapeClassifier.cs`
- Shape/material data flow: Baker in `ResonantObjectAuthoring.cs` → calls ShapeClassifier + FrequencyCalculator → creates ECS components
- Material properties: `MaterialProfileSO.cs` (editor) → `BlittableMaterialData.cs` (runtime)

**Testing:**

- Unit tests: `Assets/SoundResonance/Tests/EditMode/*.cs`
- Test entry: Run via Unity Test Runner or `dotnet test`

## Naming Conventions

**Files:**

- **Class/Component files:** PascalCase.cs (e.g., `ResonantObjectData.cs`, `FrequencyCalculator.cs`)
- **Editor-specific files:** Placed under `Editor/` folder; file name includes context (e.g., `ResonantObjectAuthoringEditor.cs`)
- **Test files:** Named after the class they test with suffix `Tests` (e.g., `ResonanceMathTests.cs`)
- **ScriptableObject files:** Typically custom menu items are in `ScriptableObjects/` folder with `SO` or `Database` suffix (e.g., `MaterialProfileSO.cs`)

**Directories:**

- **Logical grouping:** By architectural layer (Authoring, Components, Physics, ScriptableObjects)
- **Editor separation:** All Editor-only code in separate `Editor/` folder; runtime code never references Editor folder
- **Reserved empty folders:** `Hybrid/`, `Audio/`, `Systems/`, `FrequencyAnalyzer/`, `PlayMode/` — clearly signal expansion points

**C# Naming:**

- **Classes:** PascalCase (e.g., `ResonantObjectAuthoring`, `ShapeClassifier`)
- **Methods:** PascalCase (e.g., `CalculateNaturalFrequency`, `Classify`, `LorentzianResponse`)
- **Constants:** UPPERCASE_WITH_UNDERSCORES (e.g., `AmplitudeThreshold`, `BarAspectThreshold`, `FreeFreeBarCoefficient`)
- **Private fields:** camelCase with underscore prefix optional (e.g., `lossFactor` in MaterialProfileSO)
- **Enums:** PascalCase for enum type, PascalCase for values (e.g., `ShapeType.Bar`, `ShapeType.Plate`, `ShapeType.Shell`)
- **Structs:** PascalCase (e.g., `ResonantObjectData`, `BlittableMaterialData`, `ShapeClassification`)

## Where to Add New Code

**New Feature (Physics Behavior):**
- **Primary code:** `Assets/SoundResonance/Runtime/Physics/` — add new static class or method to existing utility
- **Tests:** `Assets/SoundResonance/Tests/EditMode/` — add test class following NUnit pattern
- **Example:** Adding air damping simulation → new static method in `ResonanceMath.cs` + corresponding test

**New Component/Module (ECS Workflow):**
- **Component definition:** `Assets/SoundResonance/Runtime/Components/` — create new struct inheriting `IComponentData`
- **Baker integration:** Update `ResonantObjectBaker` in `ResonantObjectAuthoring.cs` to instantiate the component
- **System (if needed):** `Assets/SoundResonance/Runtime/Systems/` — create new partial struct inheriting `ISystem`
- **Example:** Adding dampening override per-object → new struct `DampingOverride : IComponentData` in Components/

**New Material Property:**
- **ScriptableObject:** Update `MaterialProfileSO.cs` to add field (public float with [Range] and tooltip)
- **Blittable copy:** Update `BlittableMaterialData` struct with new field
- **Preset data:** Update `MaterialDatabase.GetPresetData()` array with new values for each preset
- **Validation:** Update `MaterialProfileSO.OnValidate()` if the new property has dependencies
- **Example:** Adding temperature coefficient → add float to both ProfileSO and BlittableData; update all presets

**New Editor Tool:**
- **Implementation:** `Assets/SoundResonance/Editor/Inspectors/` or `FrequencyAnalyzer/` depending on scope
- **Dependency:** Can call any public static method from Physics library
- **Coupling:** Editor-only code; never referenced from Runtime
- **Example:** Frequency sweep visualization → new window in Editor/FrequencyAnalyzer/

**Utilities / Helpers:**
- **Standalone calculations:** `Assets/SoundResonance/Runtime/Physics/` — add as static class (ensure [BurstCompile] compatible)
- **UI/Display:** `Assets/SoundResonance/Runtime/Physics/` if pure calculation (like NoteNameHelper); `Editor/` if display-specific
- **Example:** Decibel converter (Hz ↔ dB) → static method in `NoteNameHelper.cs` or new `AudioUnitConverter.cs`

## Special Directories

**Assets/SoundResonance/Runtime/ScriptableObjects/Presets/:**
- Purpose: Location for preset material ScriptableObject instances
- Generated: No — manually created via Editor menu or MaterialPresetGenerator (not yet implemented)
- Committed: Yes — presets are configuration assets, should be versioned

**Assets/SoundResonance/Tests/:**
- Purpose: Test code only; not included in runtime builds
- Generated: No — test files are written by developers
- Committed: Yes — tests are part of the codebase

**Assets/SoundResonance/Runtime/Systems/:**
- Purpose: ECS runtime systems (ISystem implementations)
- Generated: No — manually written
- Committed: Yes — core gameplay code
- Current state: Empty; ready for implementation of EmitterActivationSystem, ResonanceSystem, etc.

**Assets/SoundResonance/Runtime/Audio/:**
- Purpose: Audio synthesis and DSP layer
- Generated: No
- Committed: Yes (when implemented)
- Current state: Empty; reserved for audio callback handler, waveform synthesis, etc.

**Assets/SoundResonance/Runtime/Hybrid/:**
- Purpose: MonoBehaviour ↔ ECS integration (if needed)
- Generated: No
- Committed: Yes (when implemented)
- Current state: Empty; may not be needed if input is handled separately

## Import Organization and Dependencies

**Public API Surface:**

The system is organized so external code (future gameplay systems) only needs to import:
- `SoundResonance.ResonantObjectData` — the core component
- `SoundResonance.EmitterTag`, `SoundResonance.StrikeEvent` — event components
- `SoundResonance.ResonanceMath` — physics functions (for runtime systems or audio bridge)
- Material setup is via Editor (no runtime dependency on ScriptableObjects in the data path)

**Internal Dependencies (within SoundResonance):**

- Authoring layer imports: Physics, ScriptableObjects, Components
- Physics layer imports: Unity.Mathematics only
- Components import: Nothing (pure data)
- ScriptableObjects import: UnityEngine only
- Tests import: Physics, Components, respective test class

**No Circular Dependencies:** Baker (authoring) reads from Physics and Materials but isn't read by them.

## File Location Summary Table

| Purpose | Location | Typical File Count |
|---------|----------|-------------------|
| Authoring (Designer Interface) | `Runtime/Authoring/` | 1 |
| ECS Components | `Runtime/Components/` | 3 |
| Physics Calculations | `Runtime/Physics/` | 4 |
| Material Configuration | `Runtime/ScriptableObjects/` | 3+ |
| Editor Tools | `Editor/Inspectors/` | 2+ |
| Unit Tests | `Tests/EditMode/` | 3+ |
| Runtime Systems (Future) | `Runtime/Systems/` | 0 (empty) |
| Audio Synthesis (Future) | `Runtime/Audio/` | 0 (empty) |
| MonoBehaviour Bridges (Future) | `Runtime/Hybrid/` | 0 (empty) |
