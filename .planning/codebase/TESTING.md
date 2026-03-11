# Testing Patterns

**Analysis Date:** 2026-03-11

## Test Framework

**Runner:**
- Unity Test Framework (UTF) with NUnit backend
- Location: `Assets/SoundResonance/Tests/EditMode/` and `Assets/SoundResonance/Tests/PlayMode/`
- Uses `using NUnit.Framework;` imports

**Assertion Library:**
- NUnit assertions: `Assert.AreEqual()`, `Assert.Greater()`, `Assert.Less()`, `Assert.That()` with range constraints

**Run Commands:**
```bash
# Unity Editor: Window > General > Test Runner
# Or command line (if set up):
Unity.exe -runTests -testPlatform editmode -testResults test-results.xml

# Within Editor: Run All (EditMode) or Run All (PlayMode)
```

## Test File Organization

**Location:**
- `Assets/SoundResonance/Tests/EditMode/` — Edit-time tests, no runtime physics
- `Assets/SoundResonance/Tests/PlayMode/` — PlayMode tests (currently empty)
- Tests are **co-located by domain** in the Tests folder, not scattered alongside source code

**Naming:**
- Pattern: `[Component/Module]Tests.cs`
- Examples:
  - `FrequencyCalculatorTests.cs` (testing `FrequencyCalculator`)
  - `ResonanceMathTests.cs` (testing `ResonanceMath`)
  - `ShapeClassifierTests.cs` (testing `ShapeClassifier`)

**Structure:**
```
Assets/SoundResonance/Tests/
├── EditMode/
│   ├── FrequencyCalculatorTests.cs
│   ├── ResonanceMathTests.cs
│   ├── ShapeClassifierTests.cs
│   └── SoundResonance.Tests.EditMode.asmdef
└── PlayMode/
    └── SoundResonance.Tests.PlayMode.asmdef
```

## Test Structure

**Suite Organization:**
```csharp
namespace SoundResonance.Tests
{
    /// <summary>
    /// Validates that FrequencyCalculator produces physically meaningful results.
    /// [Detailed explanation of what's being validated and why...]
    /// </summary>
    public class FrequencyCalculatorTests
    {
        private BlittableMaterialData steel;

        [SetUp]
        public void Setup()
        {
            // Initialize test data once per test
            steel = new BlittableMaterialData
            {
                YoungsModulus = 200e9f,
                Density = 7800f,
                LossFactor = 0.0001f,
                PoissonRatio = 0.30f
            };
            steel.Validate();
        }

        [Test]
        public void LongerBar_HasLowerFrequency()
        {
            // Arrange: set up test data
            float f1 = FrequencyCalculator.CalculateBarFrequency(0.1f, 0.005f, steel.YoungsModulus, steel.Density);
            float f2 = FrequencyCalculator.CalculateBarFrequency(0.2f, 0.005f, steel.YoungsModulus, steel.Density);

            // Act: (implicitly done in Arrange above — math is pure)

            // Assert: verify results
            Assert.Less(f2, f1, "Longer bar should have lower frequency");
            float ratio = f1 / f2;
            Assert.That(ratio, Is.InRange(3.5f, 4.5f),
                $"Frequency ratio should be ~4.0 for doubled length, got {ratio}");
        }
    }
}
```

**Patterns:**
- Class-level `[SetUp]` initializes shared test data
- Single `[Test]` method per test case (one behavior per method)
- Descriptive test names as sentences: `LongerBar_HasLowerFrequency()`, `LorentzianResponse_PeaksAtNaturalFrequency()`
- Comments explain test logic and expected behavior
- Assertions include error messages with actual values: `$"Got {f0}Hz"`

## Mocking

**Framework:** No mocking framework used (Moq, NSubstitute, etc. not present).

**Patterns:**
- Direct instantiation of data structs: `new BlittableMaterialData { ... }`
- No interfaces to mock — all code is direct method calls on static classes
- Test data initialized with realistic values (e.g., steel properties from material science)

**What to Mock:**
- **Nothing.** The codebase is math-heavy with pure functions. No dependencies to mock.
- All tested code (`FrequencyCalculator`, `ResonanceMath`, `ShapeClassifier`) are static utility functions with no side effects

**What NOT to Mock:**
- Don't mock the physics math — it needs to be tested against real formulas
- Don't mock material data — use real values to validate physical correctness

## Fixtures and Factories

**Test Data:**
```csharp
[SetUp]
public void Setup()
{
    steel = new BlittableMaterialData
    {
        YoungsModulus = 200e9f,  // Pa
        Density = 7800f,          // kg/m^3
        LossFactor = 0.0001f,     // dimensionless
        PoissonRatio = 0.30f      // dimensionless
    };
    steel.Validate();
}
```

**Location:**
- Inline in `[SetUp]` method — no factory classes
- Material fixtures created on-demand:
  ```csharp
  var glass = new BlittableMaterialData
  {
      YoungsModulus = 70e9f,
      Density = 2500f,
      LossFactor = 0.001f,
      PoissonRatio = 0.22f
  };
  glass.Validate();
  ```

- Shape fixtures created as inline `float3` or `ShapeClassification` structs

## Coverage

**Requirements:** Not enforced. No visible coverage thresholds in project settings.

**View Coverage:**
- Unity Test Runner window can be configured to show coverage (if OpenCover is installed)
- No explicit coverage configuration in the project

## Test Types

**Unit Tests:**
- **Scope:** Individual math functions in isolation
- **Approach:**
  - Test correctness of formula (Lorentzian, exponential decay, inverse-square law)
  - Test edge cases (zero frequency, zero density, values at boundaries)
  - Test scaling relationships (doubling length quarters frequency for bars)
  - Test physical correctness (frequency decreases with increased length, increases with thickness)

- **Examples:**
  - `LorentzianResponse_PeaksAtNaturalFrequency()` — verifies the formula's peak is normalized to 1.0
  - `LongerBar_HasLowerFrequency()` — verifies the f ~ 1/L^2 relationship
  - `ThickerBar_HasHigherFrequency()` — verifies linear thickness dependence

**Integration Tests:**
- **Scope:** Multiple components working together
- **Approach:**
  - `CalculateNaturalFrequency_DispatchesCorrectly()` — tests that shape classification feeds into the right frequency formula
  - `DrivenOscillatorStep_ApproachesTarget()` — tests time-stepping convergence over multiple frames

- **Location:** Mixed with unit tests in `FrequencyCalculatorTests`, `ResonanceMathTests`

**E2E Tests:**
- **Framework:** Not currently used
- **Future approach:** PlayMode tests would run the full resonance simulation in-game (if needed)

## Common Patterns

**Async Testing:**
- Not used. All tested code is synchronous math.

**Error Testing:**
```csharp
[Test]
public void ZeroLength_ReturnsZero()
{
    float f0 = FrequencyCalculator.CalculateBarFrequency(0f, 0.005f, steel.YoungsModulus, steel.Density);
    Assert.AreEqual(0f, f0, "Zero length should return zero frequency");
}

[Test]
public void ZeroDensity_ReturnsZero()
{
    float f0 = FrequencyCalculator.CalculateBarFrequency(0.1f, 0.005f, steel.YoungsModulus, 0f);
    Assert.AreEqual(0f, f0, "Zero density should return zero frequency");
}
```

**Range Assertions:**
```csharp
// Tolerance for floating-point rounding/approximation
Assert.That(f0, Is.InRange(200f, 800f),
    $"Steel bar (20cm x 4mm) should produce frequency in audible range, got {f0}Hz");

// Ratio validation with tolerance
Assert.That(ratio, Is.InRange(3.5f, 4.5f),
    $"Frequency ratio should be ~4.0 for doubled length, got {ratio}");
```

**Behavioral Testing (Scaling Laws):**
```csharp
[Test]
public void StifferMaterial_HasHigherFrequency()
{
    // f ~ sqrt(E), so 4x stiffness should give 2x frequency
    float f1 = FrequencyCalculator.CalculateBarFrequency(0.1f, 0.005f, 50e9f, steel.Density);
    float f2 = FrequencyCalculator.CalculateBarFrequency(0.1f, 0.005f, 200e9f, steel.Density);

    Assert.Greater(f2, f1, "Stiffer material should have higher frequency");
    float ratio = f2 / f1;
    Assert.That(ratio, Is.InRange(1.8f, 2.2f),
        $"Frequency ratio should be ~2.0 for 4x stiffness, got {ratio}");
}
```

**Time-Step Convergence (for iterative/temporal code):**
```csharp
[Test]
public void DrivenOscillatorStep_ApproachesTarget()
{
    float current = 0f;
    float target = 1f;
    float f0 = 440f;
    float Q = 100f; // Low Q for fast convergence in test

    // Simulate 1 second at 60fps
    for (int i = 0; i < 60; i++)
    {
        current = ResonanceMath.DrivenOscillatorStep(current, target, 1f / 60f, f0, Q);
    }

    // After 1 second with tau ≈ 0.072s, should be very close to target
    Assert.That(current, Is.InRange(0.9f, 1.1f),
        $"After 1s with low-Q drive, amplitude should be near target, got {current}");
}
```

## Assembly Definition Constraints

**EditMode Tests (`SoundResonance.Tests.EditMode.asmdef`):**
```json
{
    "name": "SoundResonance.Tests.EditMode",
    "rootNamespace": "SoundResonance.Tests",
    "references": [
        "SoundResonance.Runtime",
        "Unity.Mathematics",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [ "Editor" ],
    "precompiledReferences": [ "nunit.framework.dll" ],
    "allowUnsafeCode": false,
    "defineConstraints": [ "UNITY_INCLUDE_TESTS" ]
}
```

- Tests run only in the Editor (`includePlatforms: ["Editor"]`)
- Depend on `SoundResonance.Runtime` but can reference math/physics code
- NUnit framework included as precompiled reference
- No unsafe code allowed in tests

---

*Testing analysis: 2026-03-11*
