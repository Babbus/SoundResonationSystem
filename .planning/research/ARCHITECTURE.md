# Architecture Patterns

**Domain:** Unity DOTS runtime systems for physics-based resonance simulation with procedural audio synthesis
**Researched:** 2026-03-11
**Confidence:** MEDIUM (based on training knowledge of Unity Entities 1.x and established DOTS patterns; WebSearch unavailable for verification of latest API changes)

## Recommended Architecture

The runtime layer adds three concerns to the existing 5-layer architecture: **ECS system scheduling**, **ECS-to-MonoBehaviour bridging**, and **audio-thread synthesis**. These operate across three distinct execution contexts (ECS world update, MonoBehaviour main thread, audio callback thread) with different timing, threading, and data access constraints.

```
EXISTING LAYERS (unchanged):
  Data (ScriptableObjects) -> Physics (math) -> ECS Components -> Authoring (Baker) -> Editor

NEW RUNTIME LAYERS:
  ECS Systems (ISystem)  ->  Hybrid Bridge (MonoBehaviour)  ->  Audio Synthesis (OnAudioFilterRead)
  [SimulationSystemGroup]    [main thread, LateUpdate]         [audio thread, 44100/48000 Hz]
```

### The Three Execution Contexts

| Context | Thread | Tick Rate | Data Access | Burst? |
|---------|--------|-----------|-------------|--------|
| ECS Systems | Worker threads (jobs) or main thread | Once per frame (~60Hz) | EntityManager, SystemAPI | Yes |
| MonoBehaviour Bridge | Main thread | LateUpdate (~60Hz) | EntityManager + AudioSource | No (managed) |
| Audio Callback | Audio thread | Per buffer (~every 21ms at 48kHz/1024 samples) | Only local fields | Yes (via NativeArray) |

### Component Boundaries

| Component | Responsibility | Reads From | Writes To | Execution Context |
|-----------|---------------|------------|-----------|-------------------|
| **StrikeInputSystem** | Detect player input (raycast click), enable StrikeEvent on hit entity | Input System, Physics raycasts | StrikeEvent.NormalizedForce, enables StrikeEvent | ECS, main thread (raycast requires it) |
| **EmitterActivationSystem** | Consume StrikeEvent, enable EmitterTag, set initial amplitude | StrikeEvent (enabled), ResonantObjectData | EmitterTag.StrikeAmplitude, enables EmitterTag, disables StrikeEvent | ECS, main thread or Burst job |
| **ResonanceDecaySystem** | Apply exponential decay per frame, update phase accumulator, deactivate below threshold | ResonantObjectData (where EmitterTag enabled), deltaTime | ResonantObjectData.CurrentAmplitude, .Phase, disables EmitterTag | ECS, Burst job (IJobEntity) |
| **ResonanceAudioBridge** | Read ECS amplitude/phase data, feed to audio synthesis buffer | EntityManager query for active emitters | Shared NativeArray consumed by audio thread | MonoBehaviour, LateUpdate |
| **ResonanceSynthesizer** | Generate PCM samples from amplitude/frequency/phase data | NativeArray snapshot from bridge | AudioSource output buffer (OnAudioFilterRead) | Audio thread |

### Data Flow

```
 Player Click
     |
     v
 [StrikeInputSystem] -- raycast hit --> enables StrikeEvent(force) on entity
     |
     v
 [EmitterActivationSystem] -- reads StrikeEvent --> enables EmitterTag, sets amplitude
     |                                              disables StrikeEvent
     v
 [ResonanceDecaySystem] -- per frame, for each enabled EmitterTag:
     |   amplitude = ExponentialDecay(strikeAmplitude, timeSinceStrike, f0, Q)
     |   phase += 2*PI * f0 * deltaTime   (mod 2*PI)
     |   if amplitude < threshold: disable EmitterTag
     |
     v
 [ResonanceAudioBridge (LateUpdate)] -- queries all enabled emitters
     |   copies (frequency, amplitude, phase) into shared NativeArray
     |   updates emitter count
     |
     v
 [ResonanceSynthesizer (OnAudioFilterRead)] -- runs on audio thread
     reads NativeArray snapshot
     for each sample in buffer:
       output += amplitude * sin(phase)
       phase += 2*PI * frequency / sampleRate
```

## System Ordering and Update Groups

### ISystem vs SystemBase

**Recommendation: Use ISystem with SystemAPI for all new systems.**

Rationale: ISystem is the modern Unity Entities 1.x pattern. It uses struct-based systems that are Burst-compatible by default. SystemBase is the older class-based approach that prevents Burst compilation of the OnUpdate method itself. Since this project already uses Entities 1.3.9, ISystem is fully available.

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct ResonanceDecaySystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        // SystemAPI.Query iterates over matching archetypes
        foreach (var (data, emitter) in
            SystemAPI.Query<RefRW<ResonantObjectData>, RefRO<EmitterTag>>()
                .WithAll<EmitterTag>())  // only enabled EmitterTag
        {
            // decay logic using ResonanceMath
        }
    }
}
```

### System Execution Order

Systems must run in dependency order within a single frame:

```
SimulationSystemGroup (default group, runs every frame):
  1. StrikeInputSystem        [UpdateBefore(EmitterActivationSystem)]
  2. EmitterActivationSystem  [UpdateBefore(ResonanceDecaySystem)]
  3. ResonanceDecaySystem     (no ordering constraint needed after this)

MonoBehaviour (outside ECS scheduling):
  4. ResonanceAudioBridge.LateUpdate()  -- runs after all ECS systems complete
```

**Why LateUpdate for the bridge:** ECS systems in SimulationSystemGroup run during the Update phase. LateUpdate guarantees all ECS writes are committed before the bridge reads amplitude/phase values. This avoids reading stale data.

**Why NOT a PresentationSystemGroup system for the bridge:** The bridge must interact with AudioSource (a managed MonoBehaviour). Managed object access from an ISystem requires EntityManager.GetComponentObject or a managed system, both of which prevent Burst compilation and add complexity. A plain MonoBehaviour in LateUpdate is simpler and equally correct for this use case.

## The ECS-to-Audio Bridge: Core Architecture Challenge

This is the hardest architectural problem in the project. ECS data lives in chunk memory managed by EntityManager. Audio synthesis runs on a separate thread via OnAudioFilterRead. These two worlds cannot directly share data.

### Pattern: NativeArray Double Buffer

**Recommended approach:** The bridge copies active emitter data into a NativeArray allocated with `Allocator.Persistent`. The audio callback reads from this array. Thread safety is achieved through atomic operations or a simple double-buffer swap.

```csharp
public class ResonanceAudioBridge : MonoBehaviour
{
    // Shared state between main thread and audio thread
    // NativeArray is thread-safe for single-writer/single-reader when used carefully
    private NativeArray<EmitterSnapshot> _emitterBuffer;
    private int _activeEmitterCount;

    // Snapshot of one emitter's state for audio synthesis
    public struct EmitterSnapshot
    {
        public float Frequency;
        public float Amplitude;
        public float Phase;
    }

    private EntityManager _entityManager;
    private EntityQuery _activeEmitterQuery;

    void OnEnable()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        _entityManager = world.EntityManager;
        _activeEmitterQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<ResonantObjectData>(),
            ComponentType.ReadOnly<EmitterTag>()
        );
        // Allocate persistent buffer for max simultaneous emitters
        _emitterBuffer = new NativeArray<EmitterSnapshot>(64, Allocator.Persistent);
    }

    void LateUpdate()
    {
        // Read all active emitters from ECS and snapshot into NativeArray
        // This runs on main thread, after ECS systems have written amplitude/phase
        var entities = _activeEmitterQuery.ToEntityArray(Allocator.Temp);
        int count = math.min(entities.Length, _emitterBuffer.Length);

        for (int i = 0; i < count; i++)
        {
            var data = _entityManager.GetComponentData<ResonantObjectData>(entities[i]);
            _emitterBuffer[i] = new EmitterSnapshot
            {
                Frequency = data.NaturalFrequency,
                Amplitude = data.CurrentAmplitude,
                Phase = data.Phase
            };
        }
        _activeEmitterCount = count;
        entities.Dispose();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        // Runs on audio thread -- only reads from _emitterBuffer
        int sampleRate = AudioSettings.outputSampleRate;
        int count = _activeEmitterCount; // volatile read

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;
            for (int e = 0; e < count; e++)
            {
                var emitter = _emitterBuffer[e];
                sample += emitter.Amplitude * Mathf.Sin(emitter.Phase);
                // Phase advancement happens here at audio rate for smooth synthesis
            }
            // Write to all channels
            for (int c = 0; c < channels; c++)
                data[i + c] = sample;
        }
    }

    void OnDisable()
    {
        if (_emitterBuffer.IsCreated) _emitterBuffer.Dispose();
    }
}
```

### Why Not Other Approaches

| Approach | Why Not |
|----------|---------|
| **EntityManager access from audio thread** | EntityManager is not thread-safe. Audio thread would race with ECS world updates. Guaranteed crashes. |
| **Burst-compiled audio callback** | OnAudioFilterRead receives managed float[], cannot be Burst-compiled directly. Would need unsafe pointer casting and [MonoPInvokeCallback], adding complexity for marginal gain at this scale. |
| **Unity DOTS Audio (DSPGraph)** | DSPGraph was experimental and has been discontinued/stalled. Not available in Unity 6 as a production API. Do not use. |
| **One AudioSource per emitter** | Works for small counts but each AudioSource is a managed object with overhead. At 20+ simultaneous emitters, this creates GC pressure and mixer load. Single-source additive synthesis is more efficient. |
| **Audio clip generation** | Pre-generating AudioClips per strike loses the continuous, physics-driven amplitude envelope. The whole point is that amplitude evolves frame-by-frame from ECS physics. |

### Thread Safety Analysis

The NativeArray bridge has a benign race condition: the audio thread may read a partially-updated buffer during LateUpdate writes. This produces at most one buffer's worth of slightly stale data (~21ms at 48kHz/1024 samples). For audio, this is inaudible -- human perception of amplitude changes is on the order of 50-100ms.

For stricter correctness (if needed later), use a double-buffer with atomic index swap:

```csharp
private NativeArray<EmitterSnapshot>[] _doubleBuffer = new NativeArray<EmitterSnapshot>[2];
private int _writeIndex = 0;  // main thread writes here
private int _readIndex = 1;   // audio thread reads here

void LateUpdate()
{
    // Write to _doubleBuffer[_writeIndex]
    // ...
    // Atomic swap
    int temp = _writeIndex;
    _writeIndex = _readIndex;
    Interlocked.Exchange(ref _readIndex, temp);
}
```

## Phase Continuity: Critical for Glitch-Free Audio

The existing ResonantObjectData.Phase field is updated per-frame in ECS (~60Hz). But audio synthesis needs phase advancement per-sample (~48000Hz). Two options:

### Option A: ECS Phase as Seed (Recommended)

ECS systems update Phase per-frame as a coarse accumulator. The audio callback uses the snapshot's frequency to advance its own per-sample phase counter independently. The ECS phase serves only as the initial value when an emitter first becomes active.

**Pros:** Audio thread is self-contained, no coupling to frame rate.
**Cons:** Phase may drift slightly from ECS state over time (irrelevant for audio quality).

### Option B: ECS Phase as Authority

ECS systems advance Phase at audio sample rate (multiply by samples-per-frame). Audio callback reads Phase directly from snapshot each buffer.

**Pros:** Single source of truth.
**Cons:** Phase jumps between buffers cause audible clicks. Requires interpolation logic. More complex.

**Recommendation: Option A.** The audio thread maintains its own phase accumulators per voice. ECS amplitude drives volume envelope only. Phase is a local concern of the synthesizer.

## Component Data Additions

The existing components need minor extensions for runtime:

### ResonantObjectData (existing, needs addition)

```csharp
public struct ResonantObjectData : IComponentData
{
    // EXISTING (baked, immutable at runtime):
    public float NaturalFrequency;
    public float QFactor;
    public ShapeType Shape;

    // EXISTING (runtime state):
    public float CurrentAmplitude;
    public float Phase;

    // NEW: needed for decay timing
    // Time when driving stopped, used by ExponentialDecay
    // Set by EmitterActivationSystem when StrikeEvent consumed
    public float StrikeTime;
}
```

### New: ActiveEmitterCount Singleton

```csharp
// Singleton component for bridge to know how many emitters are active
// Avoids the bridge needing to run its own query
public struct ActiveEmitterStats : IComponentData
{
    public int Count;
}
```

This is optional -- the bridge can run its own EntityQuery. But a singleton avoids redundant query execution.

## Patterns to Follow

### Pattern 1: Enableable Component State Machine

**What:** Use IEnableableComponent bits as a state machine instead of adding/removing components.

**When:** Any time an entity transitions between states (idle/active, alive/dead, emitting/silent).

**Why:** Structural changes (AddComponent/RemoveComponent) cause sync points that block all running jobs, force chunk moves, and fragment memory. Enableable bits are stored in the chunk header and toggled in O(1) with zero structural impact.

**Already established in this codebase:** EmitterTag and StrikeEvent both use this pattern correctly. Continue using it for all state transitions.

```csharp
// Good: toggle enable bit
SystemAPI.SetComponentEnabled<EmitterTag>(entity, true);

// Bad: structural change
EntityManager.AddComponent<EmitterTag>(entity); // causes sync point
```

### Pattern 2: SystemAPI.Query with RefRW/RefRO

**What:** Use SystemAPI.Query<RefRW<T>, RefRO<T>> for inline iteration in ISystem.OnUpdate.

**When:** System processes a moderate number of entities (under ~10K) and logic is straightforward.

**Why:** More readable than IJobEntity for simple systems. Still Burst-compilable when the ISystem is marked [BurstCompile]. For this project's scale (tens of resonant objects), the overhead difference vs. IJobEntity is negligible.

```csharp
[BurstCompile]
public void OnUpdate(ref SystemState state)
{
    float dt = SystemAPI.Time.DeltaTime;
    foreach (var (data, emitter, entity) in
        SystemAPI.Query<RefRW<ResonantObjectData>, RefRO<EmitterTag>>()
            .WithEntityAccess()
            .WithAll<EmitterTag>())  // filters to only enabled EmitterTag
    {
        data.ValueRW.CurrentAmplitude = ResonanceMath.ExponentialDecay(
            emitter.ValueRO.StrikeAmplitude,
            (float)SystemAPI.Time.ElapsedTime - data.ValueRO.StrikeTime,
            data.ValueRO.NaturalFrequency,
            data.ValueRO.QFactor);
    }
}
```

### Pattern 3: Single AudioSource Additive Synthesis

**What:** Use one AudioSource with OnAudioFilterRead to mix all active emitters into a single output stream.

**When:** Multiple simultaneous sound sources need procedural generation.

**Why:** Each AudioSource has overhead (managed object, mixer channel, spatial processing). Additive synthesis in a single callback is more CPU-efficient and gives full control over the mix. Phase continuity and amplitude envelopes are trivially managed per-voice in a local array.

```csharp
// One AudioSource on the bridge GameObject
// OnAudioFilterRead sums all active emitters
float sample = 0f;
for (int e = 0; e < activeCount; e++)
    sample += voices[e].amplitude * Mathf.Sin(voices[e].phase);
// Clamp to prevent clipping
data[i] = Mathf.Clamp(sample, -1f, 1f);
```

### Pattern 4: Struct-of-Arrays for Voice Data

**What:** Store synthesizer voice data in parallel NativeArrays (one for frequency, one for amplitude, one for phase) rather than an array of structs.

**When:** Audio callback processes many voices per buffer.

**Why:** Better cache locality when iterating one field across all voices. The audio callback primarily needs to read frequency and advance phase for ALL voices, then read amplitude for ALL voices. SoA layout means each of these passes touches contiguous memory.

**However:** For this project's expected scale (tens of voices, not thousands), AoS (array of EmitterSnapshot structs) is simpler and the cache difference is negligible. Use AoS until profiling shows otherwise.

## Anti-Patterns to Avoid

### Anti-Pattern 1: Managed Types in ISystem

**What:** Using class references, strings, or managed arrays inside [BurstCompile] ISystem methods.

**Why bad:** Burst cannot compile managed types. The system silently falls back to Mono/IL2CPP execution, losing 10-100x performance. No compiler error in some configurations -- just silent degradation.

**Instead:** Use only blittable types (int, float, bool, NativeArray, NativeList, FixedString). Access managed objects (AudioSource, GameObject) only from MonoBehaviour code, never from ISystem.

### Anti-Pattern 2: EntityManager Access from Audio Thread

**What:** Calling EntityManager.GetComponentData or SystemAPI methods from OnAudioFilterRead.

**Why bad:** EntityManager is not thread-safe. The audio thread runs concurrently with the main thread. This produces race conditions, corrupted data, and crashes that are intermittent and nearly impossible to debug.

**Instead:** Copy needed data to a NativeArray in LateUpdate (main thread). Audio thread reads only from that NativeArray.

### Anti-Pattern 3: Structural Changes in Hot Path

**What:** Using AddComponent/RemoveComponent inside per-frame system loops.

**Why bad:** Each structural change causes a sync point (all running jobs must complete), moves the entity to a different archetype chunk, and fragments memory. With N entities struck per frame, this creates N sync points.

**Instead:** Use IEnableableComponent (already established in this project). Toggle enable bits instead of adding/removing components.

### Anti-Pattern 4: Frame-Rate-Dependent Audio Phase

**What:** Advancing phase only in ECS at frame rate, then using that phase directly in audio synthesis.

**Why bad:** At 60fps, phase advances in steps of f0/60 radians. For a 440Hz tone, that is ~46 radians per step. The audio callback at 48kHz needs phase steps of 440/48000 = ~0.058 radians. Using the frame-rate phase produces severe aliasing and clicks.

**Instead:** ECS tracks amplitude envelope only. Audio callback maintains its own per-voice phase accumulator at sample rate.

### Anti-Pattern 5: Allocating in OnAudioFilterRead

**What:** Creating new arrays, lists, or any managed objects inside OnAudioFilterRead.

**Why bad:** GC allocation on the audio thread causes unpredictable stalls. Even a single allocation can cause a buffer underrun (audio glitch/pop) if GC decides to collect at that moment.

**Instead:** Pre-allocate all buffers in OnEnable. Use only stack variables and pre-allocated NativeArrays in the audio callback.

## Scalability Considerations

| Concern | At 10 emitters | At 50 emitters | At 200+ emitters |
|---------|----------------|----------------|------------------|
| ECS iteration | Negligible, single chunk | Negligible, 1-2 chunks | Consider IJobEntity for parallel processing |
| Audio synthesis | Single callback, no issue | Sum of 50 sines per sample, ~2% CPU | Voice stealing or LOD needed. Cull by amplitude or distance |
| Bridge copy | 10 struct copies, negligible | 50 copies, negligible | Consider chunked NativeArray copy instead of per-entity |
| Memory | ~640 bytes (10 * 64B snapshot) | ~3.2KB | ~12.8KB, still negligible |
| Mix headroom | Sum of 10 voices, easy to normalize | Normalization factor 1/sqrt(50) needed | Dynamic gain control essential to prevent clipping |

### Voice Limiting Strategy (for 50+ emitters)

When too many emitters are active for clean audio synthesis:
1. Sort by amplitude (descending)
2. Only synthesize top N voices (N = 32 is a reasonable default)
3. Remaining emitters still decay in ECS but are not heard
4. When a loud emitter's amplitude drops below a soft unheard emitter, swap

## Suggested Build Order

Build order is strictly dependency-driven. Each layer requires the previous one to be functional and testable.

### Phase 1: Core ECS Runtime Systems

**Build these first -- they have no dependency on audio.**

1. **EmitterActivationSystem** -- Consumes StrikeEvent, enables EmitterTag. Testable with a unit test that manually enables StrikeEvent and verifies EmitterTag state.
2. **ResonanceDecaySystem** -- Applies exponential decay per frame. Testable by setting initial amplitude and verifying decay curve over N frames.
3. **StrikeInputSystem** -- Raycast click to enable StrikeEvent. Testable in PlayMode with simulated input.

**Why this order:** Activation must exist before decay can run (nothing to decay). Decay must work before strike input matters (no visible result of striking). Strike input is last because it depends on both activation and decay to produce observable behavior.

### Phase 2: Hybrid Bridge

**Build after ECS systems are verified working.**

4. **ResonanceAudioBridge (MonoBehaviour)** -- Queries active emitters in LateUpdate, populates NativeArray snapshot. Testable by verifying snapshot contents match ECS state after a strike.

**Why after Phase 1:** The bridge has nothing to read until systems are writing amplitude values. Testing the bridge requires working ECS systems.

### Phase 3: Audio Synthesis

**Build after bridge is passing data correctly.**

5. **ResonanceSynthesizer (OnAudioFilterRead)** -- Reads snapshot, generates PCM. Testable by providing a known snapshot and verifying output waveform mathematically.

**Why last:** Synthesis depends on bridge data. Cannot test end-to-end audio without working bridge. Can be developed with mock data if needed for parallel work.

### Phase 4: Integration and Polish

6. **End-to-end integration tests** -- Strike object, verify audio output matches expected waveform.
7. **Voice management** -- Limiting, priority, gain normalization.
8. **Performance profiling** -- Verify 60fps with target entity count.

## File Organization

```
Assets/SoundResonance/Runtime/
  Systems/
    StrikeInputSystem.cs          -- ISystem, main thread (raycast)
    EmitterActivationSystem.cs    -- ISystem, [BurstCompile]
    ResonanceDecaySystem.cs       -- ISystem, [BurstCompile]
  Hybrid/
    ResonanceAudioBridge.cs       -- MonoBehaviour, LateUpdate
  Audio/
    ResonanceSynthesizer.cs       -- OnAudioFilterRead logic
    EmitterSnapshot.cs            -- Blittable struct for bridge data
    VoiceManager.cs               -- Voice limiting/priority (Phase 4)
```

This mirrors the empty directories already existing in the project (`Runtime/Systems/`, `Runtime/Audio/`, `Runtime/Hybrid/`).

## Sources

- Unity Entities 1.3.9 package (installed in project, `com.unity.entities`)
- Existing codebase analysis (ResonantObjectData.cs, EmitterTag.cs, StrikeEvent.cs, ResonanceMath.cs)
- Existing `.planning/codebase/ARCHITECTURE.md` and `.planning/codebase/CONCERNS.md`
- Unity documentation patterns for ISystem, SystemAPI (training knowledge, MEDIUM confidence)
- OnAudioFilterRead threading model (training knowledge, HIGH confidence -- this is a long-established Unity API)
- NativeArray thread safety characteristics (training knowledge, HIGH confidence)

**Confidence notes:**
- ISystem/SystemAPI patterns: MEDIUM. The API surface is well-known for Entities 1.x but specific method signatures should be verified against Context7 or the installed package source when implementing.
- OnAudioFilterRead threading: HIGH. This API has been stable since Unity 5 and its threading model is well-documented.
- NativeArray cross-thread access: HIGH. Well-established pattern in Unity DOTS.
- DSPGraph status: MEDIUM. It was experimental/stalled as of training data. Verify before dismissing entirely, though the project does not need it.
