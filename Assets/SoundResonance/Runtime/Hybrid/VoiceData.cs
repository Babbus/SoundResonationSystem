using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Blittable struct for double-buffer entries. Each voice slot holds the data
    /// needed by the audio synthesizer to generate sound for one resonant entity.
    /// Written by ResonanceAudioBridge on the main thread in LateUpdate,
    /// read by VoiceSynthesizer on the audio thread via GetReadBuffer().
    /// </summary>
    public struct VoiceData
    {
        /// <summary>Fundamental frequency in Hz, from ResonantObjectData.NaturalFrequency.</summary>
        public float Frequency;

        /// <summary>Current vibration amplitude [0,1], from ResonantObjectData.CurrentAmplitude.</summary>
        public float Amplitude;

        /// <summary>Strike force amplitude from EmitterTag.StrikeAmplitude, for transient scaling.</summary>
        public float StrikeAmplitude;

        /// <summary>Quality factor for per-partial decay rate calculation.</summary>
        public float QFactor;

        /// <summary>World position for AudioSource transform synchronization.</summary>
        public float3 Position;

        /// <summary>Shape type for harmonic ratio lookup via HarmonicProfile.</summary>
        public ShapeType Shape;

        /// <summary>1 = active voice, 0 = inactive (slot empty or entity deactivated).</summary>
        public byte Active;

        /// <summary>1 = just struck this frame, triggers transient envelope. 0 = sustaining.</summary>
        public byte IsNewStrike;

        /// <summary>1 = activated sympathetically (purer tone, no transient). 0 = direct strike.</summary>
        public byte IsSympathetic;
    }
}
