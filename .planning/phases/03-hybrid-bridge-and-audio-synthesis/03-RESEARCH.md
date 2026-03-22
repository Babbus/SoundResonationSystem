# Phase 3: Hybrid Bridge and Audio Synthesis - Research

**Researched:** 2026-03-22
**Domain:** Unity audio synthesis (OnAudioFilterRead), ECS-to-audio thread bridge, additive sine synthesis with harmonics
**Confidence:** HIGH

## Summary

This phase bridges the ECS simulation (running at ~60Hz on worker threads) to the audio callback (running at ~48kHz on the audio thread) and synthesizes realistic tones from physics-derived parameters. The core challenge is thread-safe data transfer: the main thread copies ECS component data into a shared buffer during LateUpdate, and each voice's OnAudioFilterRead reads from that buffer on the audio thread.

The architecture uses a voice pool of 16 pre-allocated AudioSource GameObjects, each with a MonoBehaviour implementing OnAudioFilterRead for PCM sine synthesis. Each voice produces 4 partials (fundamental + 3 overtones) with shape-specific harmonic ratios derived from Euler-Bernoulli beam theory, Kirchhoff plate theory, and Donnell shell theory. Unity's built-in AudioSource spatial blend handles 3D spatialization automatically -- OnAudioFilterRead generates mono samples, and the AudioSource applies distance attenuation and stereo panning based on its transform position relative to the AudioListener.

**Primary recommendation:** Use a double-buffer (ping-pong) pattern for the ECS-to-audio bridge. The main thread writes to buffer A while the audio thread reads from buffer B, then they swap atomically. This avoids locks entirely and prevents the audio thread from ever blocking on the main thread.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Per-entity AudioSource: each active emitter gets its own AudioSource with Unity 3D spatial blend for stereo spatialization
- Voice pool of 16 AudioSources, pre-allocated and reused
- Voice stealing: when pool is full, steal the voice with lowest CurrentAmplitude
- Release: short fade (50-100ms) when EmitterTag deactivates -- prevents clicks without being perceptible
- Each voice runs OnAudioFilterRead on its own AudioSource for PCM synthesis
- 4 partials per voice: fundamental + 3 overtones (64 total sine generators at max polyphony)
- Shape-specific harmonic ratios: Bar (1, 2.76, 5.40, 8.93), Plate (1, 1.59, 2.14, 2.65), Shell (1, 1.51, 1.93, 2.29)
- Shape-specific amplitude weights per shape type
- Higher partials decay faster: partial N decays proportionally N times faster than fundamental
- Sympathetic voices produce purer tone: emphasize fundamental, suppress upper partials
- Band-limited noise burst strike transient filtered around fundamental frequency
- Material-dependent transient duration: steel ~5ms, glass ~3ms, wood ~10-15ms
- Intensity scales with strike force (NormalizedForce)
- No transient for sympathetically activated voices
- NativeArray shared-buffer from ECS to audio thread

### Claude's Discretion
- Thread-safe bridge implementation details (buffer format, lock-free vs. double-buffer)
- Exact harmonic amplitude weights per shape (researcher should investigate physical values)
- Noise burst generation algorithm for strike transient
- AudioSource configuration (spatial blend curve, rolloff settings)
- Voice pool management implementation
- Fade-out implementation for release

### Deferred Ideas (OUT OF SCOPE)
- FMOD integration (AUD-05) -- v2 requirement
- Variable strike force (INP-02) -- v2, but transient intensity scaling is ready for it
- Full ADSR envelope -- simplified to short fade-out release
- Multi-harmonic overtones as v2 requirement (AUD-03) -- actually implementing 4 partials now
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| AUD-01 | Hybrid bridge copies ECS amplitude/frequency data to NativeArray shared-buffer in LateUpdate for audio thread consumption | Double-buffer pattern, VoiceData struct layout, bridge MonoBehaviour collecting from EntityManager |
| AUD-02 | OnAudioFilterRead generates sine waves from shared-buffer amplitude and frequency data at audio sample rate | Per-voice MonoBehaviour with phase accumulation, 4-partial additive synthesis, shape-based harmonic ratios |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Unity AudioSource | Unity 6.0.3.9f1 | Per-voice 3D audio playback with spatial blend | Built-in, handles spatialization, no external dependencies |
| OnAudioFilterRead | Unity MonoBehaviour | PCM sample generation on audio thread | Only stable real-time synthesis API in Unity (DSPGraph deprecated) |
| NativeArray | Unity.Collections | Thread-safe shared buffer between main and audio thread | Burst-compatible, no GC allocation, explicit lifetime control |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Unity.Mathematics | Entities 1.3.9 | math.sin, math.exp for synthesis | Already in project, Burst-compatible |
| Unity.Entities | 1.3.9 | EntityManager queries in bridge LateUpdate | Reading ResonantObjectData/EmitterTag from ECS world |
| System.Threading.Interlocked | .NET | Atomic buffer swap for double-buffer | Lock-free synchronization between main and audio threads |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Per-voice AudioSource | Single AudioSource mixer | Single mixer is simpler but loses free 3D spatialization per voice -- user decided per-voice |
| Double-buffer | Volatile field + copy | Simpler but risks torn reads if struct is larger than pointer width |
| NativeArray | Managed array | Would work but allocates on GC heap, not Burst-compatible for future optimization |

## Architecture Patterns

### Recommended Project Structure
```
Assets/SoundResonance/Runtime/
  Audio/
    VoiceSynthesizer.cs         # MonoBehaviour on each pooled AudioSource -- OnAudioFilterRead
    HarmonicProfile.cs          # Static shape-to-harmonic-ratio lookup (Bar/Plate/Shell)
  Hybrid/
    ResonanceAudioBridge.cs     # MonoBehaviour -- LateUpdate reads ECS, writes shared buffer
    VoicePool.cs                # Manages 16 AudioSource pool, voice assignment/stealing/release
    VoiceData.cs                # Blittable struct for shared buffer entries
```

### Pattern 1: Double-Buffer Bridge (AUD-01)
**What:** Two NativeArrays of VoiceData. Main thread writes to one while audio thread reads from the other. An atomic int index tracks which is "read" vs "write."
**When to use:** Always -- this is the load-bearing thread-safety pattern.
**Example:**
```csharp
// VoiceData -- blittable struct for one voice slot
public struct VoiceData
{
    public float Frequency;        // NaturalFrequency from ECS
    public float Amplitude;        // CurrentAmplitude from ECS
    public float StrikeAmplitude;  // For transient intensity scaling
    public ShapeType Shape;        // For harmonic ratio lookup
    public float3 Position;        // For AudioSource transform sync
    public byte Active;            // 1 = active, 0 = inactive
    public byte IsNewStrike;       // 1 = just struck this frame (trigger transient)
    public byte IsSympathetic;     // 1 = sympathetically activated (no transient)
}

// In ResonanceAudioBridge (MonoBehaviour)
private NativeArray<VoiceData> _bufferA;
private NativeArray<VoiceData> _bufferB;
private volatile int _readIndex; // 0 = read A, 1 = read B

void LateUpdate()
{
    // Write to the buffer the audio thread is NOT reading
    var writeBuffer = _readIndex == 0 ? _bufferB : _bufferA;

    // Collect active emitter data from ECS EntityManager
    // ... populate writeBuffer ...

    // Atomic swap: audio thread now reads the freshly written buffer
    _readIndex = _readIndex == 0 ? 1 : 0;
}
```

### Pattern 2: Per-Voice Phase-Accumulating Synthesis (AUD-02)
**What:** Each VoiceSynthesizer maintains per-partial phase accumulators. OnAudioFilterRead reads its assigned VoiceData slot and generates additive sine samples.
**When to use:** Every voice.
**Example:**
```csharp
// In VoiceSynthesizer (MonoBehaviour on pooled AudioSource)
private float[] _partialPhases = new float[4]; // one per partial
private int _voiceIndex; // which slot in the shared buffer this voice reads

void OnAudioFilterRead(float[] data, int channels)
{
    var voiceData = _bridge.GetReadBuffer()[_voiceIndex];
    if (voiceData.Active == 0) { /* fade out and zero */ return; }

    float[] ratios = HarmonicProfile.GetRatios(voiceData.Shape);
    float[] weights = HarmonicProfile.GetWeights(voiceData.Shape);

    int sampleRate = AudioSettings.outputSampleRate;
    float increment = 2f * Mathf.PI / sampleRate;

    for (int i = 0; i < data.Length; i += channels)
    {
        float sample = 0f;
        for (int p = 0; p < 4; p++)
        {
            float freq = voiceData.Frequency * ratios[p];
            float partialAmp = voiceData.Amplitude * weights[p];
            // Higher partials decay faster: scale amplitude by 1/(p+1)
            // (actual per-partial decay driven by ECS amplitude * partial weight)
            sample += Mathf.Sin(_partialPhases[p]) * partialAmp;
            _partialPhases[p] += freq * increment;
            if (_partialPhases[p] > 2f * Mathf.PI)
                _partialPhases[p] -= 2f * Mathf.PI;
        }

        for (int ch = 0; ch < channels; ch++)
            data[i + ch] = sample;
    }
}
```

### Pattern 3: Voice Pool with Stealing
**What:** 16 pre-instantiated AudioSource GameObjects. VoicePool assigns voices to entities and steals the quietest when full.
**When to use:** Voice management in bridge LateUpdate.
**Example:**
```csharp
public class VoicePool
{
    private VoiceSynthesizer[] _voices; // 16 pre-allocated
    private int[] _assignedEntity;      // entity index or -1

    public int AssignVoice(int entityIndex, float amplitude)
    {
        // 1. Find free voice
        for (int i = 0; i < _voices.Length; i++)
            if (_assignedEntity[i] == -1) { _assignedEntity[i] = entityIndex; return i; }

        // 2. Steal quietest voice
        int quietest = 0;
        float minAmp = float.MaxValue;
        for (int i = 0; i < _voices.Length; i++)
        {
            float amp = GetVoiceAmplitude(i);
            if (amp < minAmp) { minAmp = amp; quietest = i; }
        }
        // Trigger fade-out on stolen voice before reassignment
        _voices[quietest].BeginRelease();
        _assignedEntity[quietest] = entityIndex;
        return quietest;
    }
}
```

### Anti-Patterns to Avoid
- **Locking in OnAudioFilterRead:** Never use lock/mutex in the audio callback. Even brief contention causes audio dropouts. Use lock-free double-buffer instead.
- **Allocating in OnAudioFilterRead:** No `new`, no LINQ, no string operations. The audio thread runs at ~48kHz sample rate and must never trigger GC.
- **Reading ECS components directly from audio thread:** EntityManager is main-thread only. Always copy to a shared buffer on the main thread first.
- **Resetting phase on frequency change:** If frequency changes between frames (e.g., due to Doppler or re-strike), do NOT reset phase to 0. Continue from current phase with new increment to avoid discontinuity clicks.
- **Writing mono samples to only channel 0:** Always write to ALL channels in the interleaved buffer, otherwise you get silence in one ear.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 3D spatialization | Manual stereo panning/attenuation math | AudioSource.spatialBlend = 1 | Unity handles HRTF, distance curves, rolloff -- complex to replicate |
| Sample rate detection | Hardcoded 44100 or 48000 | AudioSettings.outputSampleRate | Varies by platform and user settings |
| Audio thread scheduling | Manual thread/timer for audio | OnAudioFilterRead | Unity handles buffer sizing, callback timing, device management |
| Phase wrapping | Manual modulo | Subtract 2*PI when > 2*PI | Modulo of floats accumulates error; subtraction is exact for single-wrap |

**Key insight:** Unity's AudioSource handles the hardest parts (device management, buffer sizing, 3D spatialization, mixer routing). OnAudioFilterRead only needs to fill a float array with PCM samples.

## Common Pitfalls

### Pitfall 1: Audio Clicks on Voice Activation/Deactivation
**What goes wrong:** Abruptly starting or stopping a sine wave creates a discontinuity in the waveform, heard as a click or pop.
**Why it happens:** A sine wave at an arbitrary phase has non-zero amplitude. Starting from phase=0 is fine (starts at zero crossing), but stopping at arbitrary phase creates a step discontinuity.
**How to avoid:** Implement attack/release envelopes in the synthesis:
- **Attack:** New voices ramp from 0 to target amplitude over ~2ms (96 samples at 48kHz). This is fast enough to be imperceptible but smooth enough to prevent clicks.
- **Release:** When voice deactivates, ramp amplitude to 0 over 50-100ms (2400-4800 samples). Do NOT immediately zero the buffer.
- **Re-strike:** If an already-active voice is re-struck, do NOT reset phase. Just update amplitude target.
**Warning signs:** Audible pops when clicking objects rapidly, or when objects deactivate.

### Pitfall 2: Torn Reads on Shared Buffer
**What goes wrong:** Audio thread reads a VoiceData struct while main thread is mid-write, resulting in corrupted data (e.g., frequency from entity A paired with amplitude from entity B).
**Why it happens:** VoiceData is larger than pointer width (>8 bytes), so reads/writes are not atomic.
**How to avoid:** Double-buffer with atomic index swap. Main thread writes the inactive buffer completely, then atomically swaps the read index. Audio thread always reads a fully consistent buffer.
**Warning signs:** Occasional random frequency spikes or amplitude glitches.

### Pitfall 3: Phase Accumulator Drift
**What goes wrong:** After running for minutes, phase accumulator grows to very large float values, losing precision and causing audible frequency drift.
**Why it happens:** Float32 has ~7 significant digits. At 48kHz with frequency 1000Hz, phase wraps every ~48 samples, but if wrapping fails the value grows without bound.
**How to avoid:** Always wrap phase with `if (phase > 2*PI) phase -= 2*PI`. Use subtraction, not modulo, to avoid accumulated error.
**Warning signs:** Tone pitch gradually drifting after minutes of playback.

### Pitfall 4: OnAudioFilterRead Not Called
**What goes wrong:** OnAudioFilterRead never fires, no audio output.
**Why it happens:** The AudioSource needs to be "playing" for the callback to fire. Even with no AudioClip, you must call AudioSource.Play().
**How to avoid:** On voice initialization: set AudioSource.clip = null, AudioSource.loop = true (technically not needed without clip but good practice), then call AudioSource.Play(). The OnAudioFilterRead callback will fire as the audio source.
**Warning signs:** No audio at all despite correct synthesis code.

### Pitfall 5: Spatial Blend and OnAudioFilterRead Interaction
**What goes wrong:** 3D audio positioning doesn't seem to work with procedural audio.
**Why it happens:** OnAudioFilterRead sits in the DSP chain. When used as the audio source (no clip attached), the AudioSource still applies its spatial blend settings to the output.
**How to avoid:** Set AudioSource.spatialBlend = 1 for full 3D. The MonoBehaviour generating audio must be on the same GameObject as the AudioSource. Keep the AudioSource transform synced with the entity's world position.
**Warning signs:** All sounds come from the same location regardless of object position.

## Code Examples

### VoiceData Struct (Shared Buffer Entry)
```csharp
// Source: designed for this project based on ResonantObjectData fields
using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Blittable struct representing one voice slot in the double-buffer.
    /// Written by ResonanceAudioBridge on main thread, read by VoiceSynthesizer on audio thread.
    /// </summary>
    public struct VoiceData
    {
        public float Frequency;         // Hz, from ResonantObjectData.NaturalFrequency
        public float Amplitude;         // [0,1], from ResonantObjectData.CurrentAmplitude
        public float StrikeAmplitude;   // from EmitterTag.StrikeAmplitude (for transient scaling)
        public float QFactor;           // for per-partial decay rate calculation
        public float3 Position;         // world position for AudioSource transform sync
        public ShapeType Shape;         // for harmonic ratio lookup
        public byte Active;             // 1 = voice should produce sound
        public byte IsNewStrike;        // 1 = trigger strike transient this frame
        public byte IsSympathetic;      // 1 = activated sympathetically (no transient)
    }
}
```

### Harmonic Profile Lookup
```csharp
// Source: Euler-Bernoulli beam theory (euphonics.org), Kirchhoff/Donnell from context decisions
namespace SoundResonance
{
    public static class HarmonicProfile
    {
        // Frequency ratios relative to fundamental
        // Bar: Euler-Bernoulli free-free beam modes: (n+1/2)^2 pattern
        // Verified: 1.00, 2.76, 5.40, 8.93 (enDAQ, euphonics.org)
        private static readonly float[] BarRatios = { 1.0f, 2.756f, 5.404f, 8.933f };

        // Plate: Kirchhoff circular plate modes (0,0), (0,1), (0,2), (1,0)
        private static readonly float[] PlateRatios = { 1.0f, 1.594f, 2.136f, 2.653f };

        // Shell: Donnell thin shell modes
        private static readonly float[] ShellRatios = { 1.0f, 1.506f, 1.927f, 2.292f };

        // Amplitude weights: how much each partial contributes
        // Bar: fundamental-heavy (metallic ring character)
        private static readonly float[] BarWeights = { 1.0f, 0.5f, 0.25f, 0.12f };

        // Plate: more evenly distributed (shimmery character)
        private static readonly float[] PlateWeights = { 1.0f, 0.7f, 0.5f, 0.35f };

        // Shell: mid-heavy (bell-like character)
        private static readonly float[] ShellWeights = { 0.8f, 1.0f, 0.7f, 0.4f };

        // Sympathetic: purer tone, fundamental dominant
        private static readonly float[] SympatheticWeights = { 1.0f, 0.15f, 0.05f, 0.02f };

        public static float[] GetRatios(ShapeType shape) => shape switch
        {
            ShapeType.Bar => BarRatios,
            ShapeType.Plate => PlateRatios,
            ShapeType.Shell => ShellRatios,
            _ => BarRatios
        };

        public static float[] GetWeights(ShapeType shape, bool isSympathetic) =>
            isSympathetic ? SympatheticWeights : shape switch
            {
                ShapeType.Bar => BarWeights,
                ShapeType.Plate => PlateWeights,
                ShapeType.Shell => ShellWeights,
                _ => BarWeights
            };
    }
}
```

### Strike Transient Noise Burst
```csharp
// Source: designed for this project per context decisions
// Band-limited noise burst around fundamental frequency
// Material-specific duration and character

private float _transientTimer;
private float _transientDuration;
private System.Random _rng = new System.Random();

void TriggerTransient(float frequency, float strikeAmplitude, ShapeType shape)
{
    // Material-dependent duration (steel is sharp, wood is softer)
    // Shape used as proxy for material character
    _transientDuration = shape switch
    {
        ShapeType.Bar => 0.005f,    // ~5ms (steel-like)
        ShapeType.Shell => 0.003f,  // ~3ms (glass/bell-like)
        ShapeType.Plate => 0.012f,  // ~12ms (wood/panel-like)
        _ => 0.005f
    };
    _transientTimer = _transientDuration;
}

float GenerateTransientSample(float frequency, float strikeAmplitude, int sampleRate)
{
    if (_transientTimer <= 0f) return 0f;

    // White noise
    float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);

    // Simple bandpass: multiply by sine at fundamental to concentrate energy
    // This is a cheap approximation of a proper bandpass filter
    float bandpass = noise * Mathf.Sin(_transientPhase);
    _transientPhase += frequency * 2f * Mathf.PI / sampleRate;

    // Amplitude envelope: linear fade-out over transient duration
    float envelope = _transientTimer / _transientDuration;

    _transientTimer -= 1f / sampleRate;

    return bandpass * envelope * strikeAmplitude * 0.3f; // 0.3 mix level
}
```

### Bridge LateUpdate Collection Pattern
```csharp
// Source: follows EmitterSnapshot pattern from SympatheticPropagationSystem.cs
void LateUpdate()
{
    var entityManager = World.DefaultGameObjectInjectionWorld?.EntityManager;
    if (entityManager == null) return;

    // Query active emitters (EmitterTag enabled)
    var query = entityManager.Value.CreateEntityQuery(
        ComponentType.ReadOnly<EmitterTag>(),
        ComponentType.ReadOnly<ResonantObjectData>(),
        ComponentType.ReadOnly<LocalTransform>()
    );

    var entities = query.ToEntityArray(Allocator.Temp);
    var dataArray = query.ToComponentDataArray<ResonantObjectData>(Allocator.Temp);
    var transforms = query.ToComponentDataArray<LocalTransform>(Allocator.Temp);

    // Write to inactive buffer
    var writeBuffer = _readIndex == 0 ? _bufferB : _bufferA;

    // Clear all slots
    for (int i = 0; i < writeBuffer.Length; i++)
    {
        var cleared = writeBuffer[i];
        cleared.Active = 0;
        writeBuffer[i] = cleared;
    }

    // Assign active emitters to voice slots via VoicePool
    for (int i = 0; i < entities.Length && i < MaxVoices; i++)
    {
        int voiceSlot = _voicePool.GetOrAssign(entities[i], dataArray[i].CurrentAmplitude);
        var vd = new VoiceData
        {
            Frequency = dataArray[i].NaturalFrequency,
            Amplitude = dataArray[i].CurrentAmplitude,
            QFactor = dataArray[i].QFactor,
            Shape = dataArray[i].Shape,
            Position = transforms[i].Position,
            Active = 1
        };
        writeBuffer[voiceSlot] = vd;
    }

    // Sync AudioSource positions on main thread
    for (int i = 0; i < MaxVoices; i++)
    {
        if (writeBuffer[i].Active == 1)
            _voices[i].transform.position = (Vector3)writeBuffer[i].Position;
    }

    // Atomic swap
    _readIndex = _readIndex == 0 ? 1 : 0;

    entities.Dispose();
    dataArray.Dispose();
    transforms.Dispose();
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| DSPGraph for real-time audio | OnAudioFilterRead | DSPGraph deprecated ~2023 | OnAudioFilterRead is the only stable real-time synthesis path |
| AudioClip.Create with PCM | OnAudioFilterRead | N/A | AudioClip.Create is for pre-generated audio, not real-time |
| Single AudioSource mixer | Per-voice AudioSource | Project decision | Enables free 3D spatialization per voice |

**Deprecated/outdated:**
- DSPGraph: Was Unity's attempt at a modern audio graph API. Deprecated, removed from package manager. Do not use.
- AudioClip.Create with SetData: Works for pre-computed audio but has latency and cannot respond to real-time parameter changes.

## Open Questions

1. **Exact per-partial amplitude weights**
   - What we know: User wants bars fundamental-heavy, plates even, shells mid-heavy. Sympathetic should be purer.
   - What's unclear: Exact numerical weights are not prescribed by physics -- they depend on strike location, excitation method, and material.
   - Recommendation: Use the weights in HarmonicProfile above as starting values. They are physically motivated (bars are struck at antinodes emphasizing fundamental; plates distribute energy more evenly; shells have strong first overtone like bells). Tune by ear during testing.

2. **Per-partial faster decay implementation**
   - What we know: "Partial N decays proportionally N times faster than fundamental."
   - What's unclear: Should this be computed on the audio thread or the ECS side?
   - Recommendation: Compute on audio thread. ECS provides a single CurrentAmplitude for the whole voice. Each partial's effective amplitude = CurrentAmplitude * weight * exp(-partialIndex * decayBoost). The audio thread can derive per-partial decay from the QFactor in the VoiceData struct using: `partialAmplitude = amplitude * weight * pow(amplitude, partialIndex * decayExponent)` where decayExponent controls how much faster upper partials fade. This keeps ECS simple (one amplitude value) while letting audio thread handle timbre evolution.

3. **Entity-to-voice tracking across frames**
   - What we know: VoicePool must map entity indices to voice slots persistently.
   - What's unclear: Entity indices can change if structural changes occur in ECS.
   - Recommendation: Use Entity (struct with Index + Version) as the key, not raw index. Store a Dictionary<Entity, int> in VoicePool. Clear mappings when entities are destroyed.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Unity Test Framework (NUnit) in PlayMode |
| Config file | `Assets/SoundResonance/Tests/PlayMode/SoundResonance.Tests.PlayMode.asmdef` |
| Quick run command | Unity Editor > Window > General > Test Runner > PlayMode > Run Selected |
| Full suite command | Unity Editor > Window > General > Test Runner > PlayMode > Run All |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| AUD-01 | Bridge copies ECS data to shared buffer | unit | EditMode test: verify buffer population from mock data | No -- Wave 0 |
| AUD-02 | OnAudioFilterRead generates sine from buffer | manual-only | Listen for tone on strike, verify no clicks | No -- manual |
| AUD-01 | Voice pool assigns/steals voices correctly | unit | EditMode test: verify assignment and stealing logic | No -- Wave 0 |
| AUD-02 | Multiple simultaneous tones mix without artifacts | manual-only | Strike 3+ objects, listen for clean additive mix | No -- manual |

### Sampling Rate
- **Per task commit:** Run EditMode tests for VoicePool logic
- **Per wave merge:** Full test suite (EditMode + PlayMode)
- **Phase gate:** Manual audio verification: strike object, hear tone, verify decay sync, verify no clicks

### Wave 0 Gaps
- [ ] `Assets/SoundResonance/Tests/EditMode/VoicePoolTests.cs` -- covers voice assignment/stealing logic
- [ ] `Assets/SoundResonance/Tests/EditMode/HarmonicProfileTests.cs` -- covers ratio/weight lookups
- [ ] Assembly reference: Tests.EditMode.asmdef needs reference to SoundResonance.Runtime

*(Audio synthesis quality is inherently manual verification -- cannot automate "does it sound right")*

## Sources

### Primary (HIGH confidence)
- [Unity OnAudioFilterRead docs](https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnAudioFilterRead.html) -- threading model, filter chain behavior, AudioSource requirements
- [Unity AudioSource.spatialBlend docs](https://docs.unity3d.com/ScriptReference/AudioSource-spatialBlend.html) -- 3D spatialization configuration
- [Unity NativeContainer docs](https://docs.unity3d.com/6000.0/Documentation/Manual/job-system-native-container.html) -- thread safety guarantees

### Secondary (MEDIUM confidence)
- [Euphonics - Beam vibration modes](https://euphonics.org/2-2-beam-vibration-and-free-free-modes/) -- Euler-Bernoulli mode ratios 1, 2.76, 5.40, 8.93 verified
- [enDAQ - Bernoulli-Euler Beams](https://endaq.com/pages/bernoulli-euler-beams) -- beam vibration frequency formula verification
- [Wikipedia - Vibration of plates](https://en.wikipedia.org/wiki/Vibration_of_plates) -- Kirchhoff plate mode information
- [Simple sine wave generator gist](https://gist.github.com/MirzaBeig/639fe29d075703287fb82b37e2ad6c2f) -- OnAudioFilterRead synthesis pattern verified
- [Ryan Hedgecock - Unity audio generation](https://blog.hedgecock.dev/2022/unity-audio-generation-simple-sounds/) -- procedural audio patterns

### Tertiary (LOW confidence)
- Plate ratios (1, 1.594, 2.136, 2.653) and shell ratios (1, 1.506, 1.927, 2.292): These are from the CONTEXT.md user decisions. The beam ratios are well-verified in literature. The plate and shell ratios are physically reasonable (Kirchhoff and Donnell solutions depend on boundary conditions) but exact values depend on specific geometry and boundary conditions chosen. For thesis purposes, these values produce the correct qualitative character (inharmonic partials, plate shimmer, bell-like shell) and are acceptable.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- Unity OnAudioFilterRead + NativeArray is well-documented and the only viable path
- Architecture: HIGH -- Double-buffer bridge pattern is standard for real-time audio; voice pool is straightforward
- Harmonic ratios: MEDIUM -- Beam modes verified; plate/shell modes are physically motivated but specific to boundary conditions
- Pitfalls: HIGH -- Audio threading pitfalls are well-known in Unity audio community

**Research date:** 2026-03-22
**Valid until:** 2026-04-22 (stable domain, Unity 6 API unlikely to change)
