# Architecture

**Analysis Date:** 2026-03-11

## Pattern Overview

**Overall:** Physics-based ECS (Entity Component System) simulation with baker-driven authoring. The system uses Unity Entities 1.x with Burst compilation for real-time resonance physics simulation, moving expensive computations to edit-time baking where possible.

**Key Characteristics:**
- **Edit-Time Baking:** Shape classification and frequency calculation happen once in the Editor via Baker components, not at runtime
- **ECS with Enableable Components:** Uses `IEnableableComponent` as a zero-cost event mechanism for state transitions without structural changes
- **Pure Math Library:** Physics calculations isolated in Burst-compilable static utility classes
- **Authoring Layer:** MonoBehaviour-based configuration (ResonantObjectAuthoring) that describes designer intent, converted to ECS data at bake time
- **Material-Based Physics:** All acoustic behavior derives from real material science constants (Young's Modulus, density, loss factor), not artistic tweaks

## Layers

**Authoring Layer:**
- Purpose: Designer-facing configuration in the Editor. GameObjects with ResonantObjectAuthoring components describe which physical material should drive the resonance simulation
- Location: `Assets/SoundResonance/Runtime/Authoring/`
- Contains: ResonantObjectAuthoring (MonoBehaviour wrapper)
- Depends on: Unity Transform, MeshFilter components; MaterialProfileSO database
- Used by: Baker process; ResonantObjectAuthoringEditor custom inspector

**Baker / Baking Layer:**
- Purpose: Converts authoring data into baked ECS components at edit-time. Runs once when SubScene is modified
- Location: Nested within ResonantObjectAuthoring.cs as ResonantObjectBaker class
- Contains: Baker<ResonantObjectAuthoring> implementation
- Depends on: ShapeClassifier, FrequencyCalculator, MaterialProfileSO
- Used by: Unity's baking system (transparent to gameplay code)
- Process:
  1. Read MeshFilter bounds and GameObject scale
  2. Classify shape (Bar/Plate/Shell) from bounding box aspect ratios
  3. Look up material properties from assigned MaterialProfileSO
  4. Compute natural frequency using shape-specific formula
  5. Bake ResonantObjectData, EmitterTag (disabled), and StrikeEvent (disabled) components to entity

**Component / Data Layer:**
- Purpose: ECS data structures and tags used at runtime
- Location: `Assets/SoundResonance/Runtime/Components/`
- Contains:
  - `ResonantObjectData`: Core physics state (natural frequency, Q-factor, shape, current amplitude, phase)
  - `EmitterTag`: Enableable tag marking currently-vibrating objects. Toggled by systems to avoid structural changes
  - `StrikeEvent`: Enableable event component. Enabled by input/collision detection to signal a strike. Consumed by activation system
- Depends on: Nothing (pure data)
- Used by: Runtime systems (would be implemented in future), physics calculations

**Physics/Math Library:**
- Purpose: Pure, stateless mathematical functions for resonance simulation physics
- Location: `Assets/SoundResonance/Runtime/Physics/`
- Contains:
  - `ResonanceMath`: Driven damped oscillator formulas (Lorentzian response, exponential decay, discrete time steps)
  - `FrequencyCalculator`: Analytical frequency formulas for bar/plate/shell geometries
  - `ShapeClassifier`: Bounding-box-based shape classification into canonical vibration modes
  - `NoteNameHelper`: Frequency-to-musical-note conversion utility
- Depends on: Unity.Mathematics (Burst-compatible math)
- Used by: Baker (edit-time), potential runtime systems, tests, custom inspector

**Material Database Layer:**
- Purpose: Centralized storage of physical material properties and presets
- Location: `Assets/SoundResonance/Runtime/ScriptableObjects/`
- Contains:
  - `MaterialProfileSO`: ScriptableObject holding a single material's properties (Young's Modulus, density, loss factor, Poisson's ratio)
  - `BlittableMaterialData`: Burst-safe struct copy of material data (used at runtime/in jobs)
  - `MaterialDatabase`: Singleton registry of all MaterialProfileSO assets with lookup methods
- Depends on: ScriptableObject serialization, material science reference data
- Used by: Authoring (designer selects material); Baker (reads material data); Custom inspector (displays properties)

**Editor / Tooling Layer:**
- Purpose: Development-time visualization and data generation
- Location: `Assets/SoundResonance/Editor/`
- Contains:
  - `ResonantObjectAuthoringEditor`: Custom inspector that displays computed resonance properties (frequency, note name, Q-factor) in real-time as designer adjusts mesh or material
  - `MaterialPresetGenerator`: Editor tool for creating MaterialProfileSO assets from preset data
- Depends on: All other layers for reading state; doesn't affect runtime
- Used by: Editor UI only; transparent to gameplay

## Data Flow

**Edit-Time Design Flow (Baker / Baking System):**

1. Designer places a GameObject in a SubScene with MeshFilter and ResonantObjectAuthoring component
2. Designer assigns a MaterialProfileSO (e.g., "Steel") to the ResonantObjectAuthoring
3. Unity's baking system detects the component and calls ResonantObjectBaker.Bake()
4. Baker reads:
   - MeshFilter.sharedMesh.bounds for geometry
   - Transform.lossyScale for world-space dimensions
   - MaterialProfileSO.GetBlittableData() for physical constants
5. Baker calls ShapeClassifier.Classify(extents) → classifies mesh into Bar/Plate/Shell
6. Baker calls FrequencyCalculator.CalculateNaturalFrequency(shape, material) → computes f0 in Hz
7. Baker creates entity with ResonantObjectData (all baked values), EmitterTag (disabled), StrikeEvent (disabled)
8. Baked data serialized to disk with SubScene

**Runtime Activation Flow (Intended, Not Yet Implemented):**

1. Input system or collision detection enables StrikeEvent on entity with normalized force amplitude
2. Runtime system (future) reads StrikeEvent.NormalizedForce
3. System enables EmitterTag, sets EmitterTag.StrikeAmplitude = NormalizedForce, disables StrikeEvent
4. Each frame, resonance system (future) updates amplitude via ResonanceMath.ExponentialDecay()
5. When CurrentAmplitude drops below AmplitudeThreshold, system disables EmitterTag
6. Audio system (future) reads CurrentAmplitude and Phase to generate waveform via ResonanceMath.LorentzianResponse()

**State Management:**

- **Baked State:** All computed physics properties (frequency, Q-factor, shape) baked into ResonantObjectData at edit-time. Immutable at runtime.
- **Runtime State:** Current vibration state (amplitude, phase) stored in ResonantObjectData and updated per-frame by systems (not yet implemented).
- **Event State:** StrikeEvent and EmitterTag use `IEnableableComponent` to signal state transitions without causing structural chunk fragmentation.

## Key Abstractions

**Driven Damped Harmonic Oscillator:**
- Purpose: Models how any resonant physical object behaves when struck or driven — the fundamental equation underlying all resonance phenomena
- Examples: `ResonanceMath.LorentzianResponse()`, `ResonanceMath.ExponentialDecay()`, `ResonanceMath.DrivenOscillatorStep()`
- Pattern: Closed-form analytical solutions (not numerical integration). Lorentzian frequency response formula A(f) = 1/sqrt((1-r²)² + (r/Q)²) where r = f/f0

**Shape-Based Frequency Calculation:**
- Purpose: Maps 3D mesh geometry to one of three canonical vibration shapes, each with its own analytical frequency formula
- Examples: Bar (tuning fork) f0 ∝ t/L²; Plate (cymbal) f0 ∝ t/D²; Shell (bell) f0 ∝ t/R² (t=thickness, L/D/R=characteristic lengths)
- Pattern: Classification heuristic (bounding box aspect ratios) → dispatch to formula based on shape type
- Implementation: `ShapeClassifier.Classify()` returns ShapeType + dimensions; `FrequencyCalculator.CalculateNaturalFrequency()` dispatches to appropriate formula

**Material Property Inheritance:**
- Purpose: All acoustic behavior (frequency, Q-factor, decay rate) derives from four fundamental material constants: E (stiffness), ρ (density), η (loss factor), ν (Poisson's ratio)
- Examples: Q = 1/η; f0 ∝ sqrt(E/ρ); decay_rate ∝ η
- Pattern: Material data stored once in ScriptableObject; read by Baker and passed to all calculators; never modified at runtime

**Enableable Component Events:**
- Purpose: Signal state changes (strike, activation, deactivation) without structural chunk changes, which would cause expensive sync points
- Examples: StrikeEvent enabled by input → consumed by (future) EmitterActivationSystem → disables itself; EmitterTag toggled when amplitude rises/falls below threshold
- Pattern: Zero-cost bit flip in chunk header via SetComponentEnabled<T>() instead of AddComponent/RemoveComponent

## Entry Points

**Edit-Time Entry Point:**
- Location: `Assets/SoundResonance/Runtime/Authoring/ResonantObjectAuthoring.cs` (the component itself)
- Triggers: Designer places the component on a GameObject in a SubScene; Editor detects it during baking
- Responsibilities: Acts as a declaration of intent (which material), triggers Baker via implicit system discovery

**Baker Entry Point:**
- Location: Nested class `ResonantObjectBaker` inside ResonantObjectAuthoring.cs
- Triggers: Unity's baking system when SubScene is baked
- Responsibilities:
  1. Validate inputs (material assigned, mesh exists)
  2. Calculate shape and frequency
  3. Create baked ECS components
  4. Establish dependencies so re-baking occurs when material asset changes

**Custom Inspector Entry Point:**
- Location: `Assets/SoundResonance/Editor/Inspectors/ResonantObjectAuthoringEditor.cs`
- Triggers: Inspector GUI rendered when ResonantObjectAuthoring component selected in Editor
- Responsibilities: Display computed resonance properties (shape, f0, note name, Q-factor) with live updates

**Preset Generator Entry Point:**
- Location: `Assets/SoundResonance/Editor/Inspectors/MaterialPresetGenerator.cs`
- Triggers: Editor menu command (future, not yet implemented)
- Responsibilities: Create MaterialProfileSO assets from preset data array

## Error Handling

**Strategy:** Defensive null checks and numerical bounds checks; log warnings when configuration is incomplete; provide sensible fallbacks (return 0 frequency, skip baking, use default material values).

**Patterns:**
- **Missing Material:** Baker logs warning "has no material profile assigned" and skips baking the entity
- **Missing Mesh:** Baker logs warning "has no MeshFilter or mesh" and skips baking
- **Invalid Dimensions:** Frequency calculators check for non-positive values before computing (prevents NaN/Infinity). Return 0 if invalid.
- **Numerical Edge Cases:** NoteNameHelper checks for NaN/Infinity frequency; returns "—" (em dash) for invalid values
- **Division by Zero:** All frequency formulas guard against zero/negative characteristic lengths and densities

## Cross-Cutting Concerns

**Burst Compatibility:**
- All physics math in ResonanceMath, FrequencyCalculator, ShapeClassifier marked `[BurstCompile]` or statically compatible
- Uses only `Unity.Mathematics` (no LINQ, no managed allocations, no virtual methods)
- Enables future compilation of runtime physics jobs at high performance

**Validation and Debugging:**
- MaterialProfileSO.OnValidate() recomputes Q-factor and speed-of-sound when properties change in Inspector
- BlittableMaterialData.Validate() recomputes derived values when needed
- All computed values displayed in ResonantObjectAuthoringEditor for immediate designer feedback

**Coupling:**
- Components are independent and composable — ResonantObjectData doesn't know about EmitterTag or materials
- Math library is pure (no state, no dependencies beyond Unity.Mathematics)
- Baker is the only place that ties authoring intent to ECS data structure

**Performance Assumptions:**
- Expensive calculations (frequency, shape classification) happen once at bake time, not per-frame
- Runtime systems (future) will use Burst-compiled jobs on enableable components to avoid structural changes
- Material properties cached in BlittableMaterialData struct for zero-overhead access in tight loops
