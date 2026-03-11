# Coding Conventions

**Analysis Date:** 2026-03-11

## Naming Patterns

**Files:**
- PascalCase for all C# files: `FrequencyCalculator.cs`, `ResonantObjectData.cs`
- Suffixes indicate purpose: `*Data.cs` for structs, `*SO.cs` for ScriptableObjects, `*Tests.cs` for test classes, `*Helper.cs` for utility functions, `*Editor.cs` for editor-only code
- Tests use full name: `FrequencyCalculatorTests.cs`, `ResonanceMathTests.cs`

**Functions:**
- PascalCase for all public methods and static methods: `CalculateBarFrequency()`, `LorentzianResponse()`, `FrequencyToNoteName()`, `GetBlittableData()`
- Private methods also PascalCase: `SortDescending()`, `Swap()`
- Very few private methods — most code is static, public, and Burst-compatible

**Variables:**
- PascalCase for fields (both public and private): `YoungsModulus`, `Density`, `CurrentAmplitude`, `NaturalFrequency`, `LossFactor`
- camelCase for local variables: `f0`, `f1`, `f2`, `ratio`, `extents`, `classification`, `alpha`, `tau`, `response`
- Parameter names: camelCase with descriptive intent: `frequencyHz`, `naturalFrequency`, `drivingFrequency`, `deltaTime`, `characteristicLength`, `youngsModulus`
- const fields: camelCase with `Coefficient` or `Threshold` suffix: `FreeFreeBarCoefficient`, `PlateCoefficient`, `BarAspectThreshold`

**Types:**
- PascalCase for all types: `ResonantObjectData`, `BlittableMaterialData`, `ShapeClassification`, `ShapeType`, `EmitterTag`, `StrikeEvent`
- Enums have clean descriptive names: `ShapeType` with values `Bar`, `Plate`, `Shell`
- Interfaces use standard Unity naming: `IComponentData`, `IEnableableComponent`

## Code Style

**Formatting:**
- Standard .NET conventions: 4-space indentation
- Brace style: Allman (opening brace on new line) is NOT used; K&R style (brace on same line) for methods, but classes use opening brace on new line
- Line length: No visible hard limit, but typical lines stay under 100 characters
- Whitespace: Blank lines separate logical sections within methods

**Linting:**
- No explicit linting framework detected (no .eslintrc, StyleCop, or Analyzer files)
- Code follows implicit conventions from manual review and editor defaults
- Focus on clarity and documentation over strict rule enforcement

## Import Organization

**Order:**
1. Using directives (system namespaces first, then Unity namespaces, then project namespaces)
   - `using Unity.Burst;`
   - `using Unity.Entities;`
   - `using Unity.Mathematics;`
   - `using UnityEditor;` (if editor code)
   - `using UnityEngine;`
   - No project-specific imports in most files (single namespace `SoundResonance`)

2. Namespace declaration
   - `namespace SoundResonance { }`
   - Or `namespace SoundResonance.Tests { }` for test code
   - Or `namespace SoundResonance.Editor { }` for editor code

**Path Aliases:**
- Not used. All imports are explicit Unity packages or the `SoundResonance` namespace

## Error Handling

**Patterns:**
- Guard clauses at function entry: validate parameters immediately, return safe default if invalid
  ```csharp
  if (length <= 0f || density <= 0f) return 0f;
  if (naturalFrequency <= 0f || qFactor <= 0f) return 0f;
  if (frequencyHz <= 0f || float.IsNaN(frequencyHz) || float.IsInfinity(frequencyHz))
      return "\u2014"; // em dash
  ```

- No exceptions thrown in math/physics code. Returns zero or a sentinel value instead.
- Baker code uses `Debug.LogWarning()` for missing/invalid components:
  ```csharp
  Debug.LogWarning($"ResonantObjectAuthoring on '{authoring.name}' has no material profile assigned. Skipping bake.", authoring);
  ```

- Editor validation uses `OnValidate()` to clamp values:
  ```csharp
  private void OnValidate()
  {
      if (lossFactor <= 0f) lossFactor = 0.00001f;
      if (density <= 0f) density = 1f;
  }
  ```

## Logging

**Framework:** Direct `Debug.Log*()` calls or none at all in release code.

**Patterns:**
- Used only in authoring/baking: `Debug.LogWarning()` for configuration issues
- Physics/math code is silent — no logging
- No production logging framework (no Serilog, Log4Net, etc.)

## Comments

**When to Comment:**
- Extensive use of XML documentation comments (`/// <summary>`) on all public types, methods, and fields
- Complex physics formulas explained with multi-line comments including:
  - The governing equation (e.g., harmonic oscillator differential equation)
  - Physical intuition (why frequency increases with thickness, decreases with length)
  - References to academic sources (Blevins, Leissa, etc.)
  - Derivation of constants and coefficients

**JSDoc/TSDoc:**
- Fully uses XML doc comments (C# equivalent of JSDoc)
- Example from `FrequencyCalculator.cs`:
  ```csharp
  /// <summary>
  /// Computes the natural (resonant) frequency of an object from its material properties
  /// and geometry using analytical formulas from structural vibration theory.
  ///
  /// Why natural frequency matters:
  /// Every physical object has frequencies at which it "wants" to vibrate...
  /// </summary>
  public static float CalculateBarFrequency(float length, float thickness,
      float youngsModulus, float density)
  ```

- Parameters documented: `/// <param name="length">Bar length in meters.</param>`
- Returns documented: `/// <returns>Fundamental frequency in Hz.</returns>`
- Complex behavior explained in detail with links to underlying theory

## Function Design

**Size:**
- Small, focused functions: most are 10-30 lines
- Single responsibility: one formula per function
- Examples:
  - `CalculateBarFrequency()`: 7 lines
  - `LorentzianResponse()`: 12 lines
  - `DrivenOscillatorStep()`: 8 lines

**Parameters:**
- Explicit parameter names that include units: `frequencyHz`, `timeSinceStop`, `referenceDistance`
- No ambiguity: `youngsModulus` not just `E`
- Default parameters used sparingly (example: `referenceDistance = 1f` in `InverseSquareAttenuation()`)

**Return Values:**
- Always return a value (no `void` methods except `Bake()` in Bakers)
- Safe defaults on error: return `0f` for frequencies, `0f` for ratios, `"\u2014"` for invalid notes
- Struct returns used (not out parameters): `ShapeClassification` returned from `Classify()`

## Module Design

**Exports:**
- Static classes export all public methods (no private static classes or sealed modifiers)
- Examples:
  - `ResonanceMath`: all 6 methods public, no instance fields
  - `FrequencyCalculator`: all methods public, constants private
  - `ShapeClassifier`: public `Classify()`, private helper `SortDescending()`

- ScriptableObjects (MonoBehaviour-derived) expose field editors with tooltips

**Barrel Files:**
- Not used. Each file has one main type (single-responsibility)
- No `index.cs` or `_all.cs` files

## Burst Compatibility

**Key Constraint:**
- Math code must be Burst-compilable (`[BurstCompile]` attribute applied to `ResonanceMath` class)
- All math functions use `unity.mathematics` types: `float3`, `math.sqrt()`, `math.log2()`, etc.
- No managed allocations, no `List<T>`, no strings in hot paths
- Structs are unmanaged/blittable (no class fields, all primitive/math types)

**Example:**
```csharp
[BurstCompile]
public static float LorentzianResponse(float drivingFrequency, float naturalFrequency, float qFactor)
{
    if (naturalFrequency <= 0f || qFactor <= 0f) return 0f;
    // ... pure math
}
```

## ECS Patterns

**Component Data:**
- Implemented as `struct` with `IComponentData` interface
- Examples: `ResonantObjectData`, `StrikeEvent`, `EmitterTag`
- Small, blittable structs — no references, all primitive fields
- Public fields (no properties) for direct access by ECS jobs

**Enableable Components:**
- Use `IEnableableComponent` for one-shot events or toggleable state
- Example: `StrikeEvent : IComponentData, IEnableableComponent` — enabled on input, consumed, disabled
- Cheaper than adding/removing components (no structural changes)

---

*Convention analysis: 2026-03-11*
