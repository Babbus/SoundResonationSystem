# External Integrations

**Analysis Date:** 2026-03-11

## APIs & External Services

**Not detected:** This project contains no external API integrations, cloud services, or third-party SDKs.

The system is entirely self-contained for physics simulation and audio synthesis. All acoustic material data is embedded as ScriptableObject presets in the codebase.

## Data Storage

**Databases:**
- Not used - Project uses only local ScriptableObject assets for material data

**File Storage:**
- Local filesystem only
  - ScriptableObject assets: `Assets/SoundResonance/Runtime/ScriptableObjects/`
  - Material presets: `Assets/SoundResonance/Runtime/ScriptableObjects/Presets/`
  - No file I/O in runtime code; only Editor-time operations via AssetDatabase

**Caching:**
- Not applicable - All data is baked to ECS at edit time; no runtime caching layers

## Authentication & Identity

**Not applicable** - No authentication system. Project is standalone simulation with no multi-user or networked features.

## Monitoring & Observability

**Error Tracking:**
- Not integrated - Uses standard Unity Debug.LogWarning/LogError

**Logs:**
- Console logging via `Debug.Log()`, `Debug.LogWarning()`
- Example: `Assets/SoundResonance/Editor/Inspectors/MaterialPresetGenerator.cs` logs material generation status

## CI/CD & Deployment

**Hosting:**
- None - Standalone application or embedded module

**CI Pipeline:**
- Not configured - This is a development codebase, not production-deployed service

## Environment Configuration

**Required env vars:**
- None - All configuration is Editor-based or hardcoded in ScriptableObjects

**Secrets location:**
- Not applicable - No API keys, credentials, or secrets in use

**Configuration approach:**
- ScriptableObject assets in `Assets/SoundResonance/Runtime/ScriptableObjects/`
- Material database: `MaterialDatabase.asset` contains all 11 preset materials
- No external configuration files required

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- None

## Material Data Source

**Embedded Reference Data:**
- All physical constants sourced from printed references
- Materials defined in `Assets/SoundResonance/Runtime/ScriptableObjects/MaterialDatabase.cs`:
  - Steel, Aluminum, Glass, Brass, Copper
  - Wood (Oak, Spruce), Concrete, Rubber, Ceramic
- Data sources (comments in code):
  - ASM International Materials Data
  - Kinsler & Frey "Fundamentals of Acoustics"
  - Blevins "Formulas for Natural Frequency and Mode Shape"
  - CES EduPack material property database

## Audio Synthesis

**Audio Integration:**
- Unity Audio Module (built-in) - No external audio engine
- Amplitude output from `ResonantObjectData.CurrentAmplitude` drives audio callbacks
- Reference to `ResonanceAudioBridge` (not present in explored code) would handle real-time synthesis in LateUpdate

---

*Integration audit: 2026-03-11*
