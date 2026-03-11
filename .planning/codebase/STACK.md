# Technology Stack

**Analysis Date:** 2026-03-11

## Languages

**Primary:**
- C# (9.0+) - All runtime and editor code for physics simulation and ECS systems

**Secondary:**
- YAML - Asset serialization and configuration (ProjectSettings)

## Runtime

**Engine:**
- Unity 6 (6000.3.9f1)

**Package Manager:**
- NuGet (via Unity Package Manager)
- Lockfile: `Packages/manifest.json` and `Packages/packages-lock.json`

## Frameworks

**Core ECS & Physics:**
- Unity Entities 1.3.9 - DOTS ECS framework for data-driven design
- Unity Entities Graphics 1.4.18 - Rendering pipeline for ECS entities
- Unity Burst 1.8.0 (via dependencies) - JIT compilation for high-performance C# code
- Unity Transforms - Transform component system for ECS
- Unity Collections 1.4.x - High-performance data structures (NativeArray, NativeList)
- Unity Mathematics 1.3.x - SIMD-optimized math library
- Unity Physics 2.0.x (as part of Entities) - Physics engine for DOTS

**Graphics:**
- Universal Render Pipeline (URP) 17.3.0 - Default rendering pipeline
- Unity.Rendering (via entities-graphics)

**Testing:**
- Unity Test Framework 1.6.0 - Edit-mode and Play-mode tests

**Input & Audio:**
- Input System 1.18.0 - New input handling system
- Audio Module (built-in) - For audio synthesis integration

**Development Tools:**
- Rider IDE integration 3.0.39
- Visual Studio IDE integration 2.0.26
- Timeline 1.8.10 - Animation timeline
- Visual Scripting 1.9.9 (included, not used in core)

## Key Dependencies

**Critical for Physics Simulation:**
- com.unity.entities - ECS framework enabling data-oriented physics calculations
- com.unity.burst - Runtime JIT for Burst jobs, essential for performance-critical frequency/shape calculations
- com.unity.mathematics - SIMD-optimized math for transcendental functions (sqrt, sin)
- com.unity.collections - Memory-efficient collections for entity data

**Infrastructure:**
- com.unity.transforms - Spatial transform hierarchy
- com.unity.render-pipelines.universal - Rendering substrate

**Editor & Development:**
- com.unity.test-framework - Unit/integration testing
- com.unity.ide.rider, com.unity.ide.visualstudio - IDE support

## Configuration

**Environment:**
- Platform-agnostic via ProjectSettings
- Target platforms: Standalone, WebGL (via URP support)
- Build target configured in ProjectSettings/ProjectSettings.asset

**Build:**
- Project type: Slnx solution (C# workspace format)
- Assembly definitions: `SoundResonance.Runtime.asmdef`, `SoundResonance.Editor.asmdef`, `SoundResonance.Tests.*.asmdef`
- Unsafe code enabled in Runtime assembly for Burst job optimization

**Project Layout:**
- Entry point: Standard Unity scene-based with SubScene baking
- Build pipeline: Assets/SoundResonance subdirectory follows module structure

## Platform Requirements

**Development:**
- .NET 8.0 (implicit via Unity 6)
- Visual Studio 2022 or Rider 2024.x
- Hardware: Modern CPU with AVX support for Burst

**Production:**
- Deployment: Standalone executable, WebGL, or as embedded Unity module
- Runtime requirements: OpenGL 4.1+ for graphics

---

*Stack analysis: 2026-03-11*
