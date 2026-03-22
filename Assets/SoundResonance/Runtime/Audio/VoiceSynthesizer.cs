using UnityEngine;

namespace SoundResonance
{
    /// <summary>
    /// Per-voice MonoBehaviour that generates PCM audio via OnAudioFilterRead.
    /// Each instance lives on a pooled AudioSource GameObject managed by ResonanceAudioBridge.
    /// Reads its assigned VoiceData slot from the double-buffer on the audio thread.
    ///
    /// Synthesis pipeline:
    /// 1. 4-partial additive sine waves with shape-specific harmonic ratios
    /// 2. Per-partial faster decay (upper partials fade faster than fundamental)
    /// 3. Strike transient: band-limited noise burst filtered around fundamental
    /// 4. Attack/release envelope to prevent clicks on activation/deactivation
    ///
    /// Critical audio thread rules:
    /// - NO allocations (no new, no LINQ, no strings)
    /// - NO locks or mutexes
    /// - NEVER reset partial phases (prevents clicks on re-strike or frequency change)
    /// - Always write to ALL channels in the interleaved buffer
    /// - Phase wrapping uses subtraction, not modulo
    /// </summary>
    public class VoiceSynthesizer : MonoBehaviour
    {
        private ResonanceAudioBridge _bridge;
        private int _voiceIndex;

        // Phase accumulators per partial (persistent across callbacks for continuity)
        private float[] _partialPhases = new float[HarmonicProfile.PartialCount];

        // Envelope state
        private float _envelopeGain;
        private bool _wasActive;

        // Strike transient state
        private float _transientTimer;
        private float _transientDuration;
        private float _transientPhase;
        private float _transientAmplitude;

        // Deterministic noise generator for strike transient
        private System.Random _rng;

        // Timing constants
        private const float AttackTime = 0.002f;   // 2ms ramp up
        private const float ReleaseTime = 0.075f;   // 75ms fade out
        private const float TransientMix = 0.3f;    // transient blend level
        /// <summary>
        /// Called by ResonanceAudioBridge during pool initialization.
        /// </summary>
        public void Initialize(ResonanceAudioBridge bridge, int voiceIndex)
        {
            _bridge = bridge;
            _voiceIndex = voiceIndex;
            _rng = new System.Random(voiceIndex);
        }

        /// <summary>
        /// Audio thread callback. Generates PCM samples from double-buffer voice data.
        /// Called at audio sample rate (~48kHz) on the audio thread.
        /// MUST be allocation-free and lock-free.
        /// </summary>
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_bridge == null) return;

            var readBuffer = _bridge.GetReadBuffer();
            if (!readBuffer.IsCreated) return;

            var voiceData = readBuffer[_voiceIndex];

            int sampleRate = AudioSettings.outputSampleRate;
            float increment = 2f * Mathf.PI / sampleRate;

            // Get harmonic ratios and weights for this voice's shape
            float[] ratios = HarmonicProfile.GetRatios(voiceData.Shape);
            float[] weights = HarmonicProfile.GetWeights(voiceData.Shape, voiceData.IsSympathetic == 1);

            // --- State transitions ---

            // Voice just activated
            if (voiceData.Active == 1 && !_wasActive)
            {
                if (voiceData.IsNewStrike == 1)
                {
                    TriggerTransient(voiceData, sampleRate);
                }
                _wasActive = true;
            }
            // Re-strike on already active voice
            else if (voiceData.IsNewStrike == 1 && _wasActive)
            {
                // Trigger transient but do NOT reset partial phases (prevents click)
                TriggerTransient(voiceData, sampleRate);
            }
            // Release complete: voice fully faded out
            else if (voiceData.Active == 0 && _wasActive && _envelopeGain <= 0.0001f)
            {
                _wasActive = false;
                // Zero the buffer and return
                for (int i = 0; i < data.Length; i++)
                    data[i] = 0f;
                return;
            }

            // If never activated and not active, zero and return
            if (!_wasActive && voiceData.Active == 0)
            {
                for (int i = 0; i < data.Length; i++)
                    data[i] = 0f;
                return;
            }

            // --- Per-sample loop ---
            for (int i = 0; i < data.Length; i += channels)
            {
                // Envelope update
                if (voiceData.Active == 1)
                {
                    // Attack: ramp toward 1.0
                    _envelopeGain += (1f - _envelopeGain) * Mathf.Min(1f, 1f / (AttackTime * sampleRate));
                }
                else
                {
                    // Release: ramp toward 0.0
                    _envelopeGain *= 1f - Mathf.Min(1f, 1f / (ReleaseTime * sampleRate));
                    if (_envelopeGain < 0.00001f)
                        _envelopeGain = 0f;
                }

                // Additive sine synthesis with per-partial decay
                float sample = 0f;
                for (int p = 0; p < HarmonicProfile.PartialCount; p++)
                {
                    float freq = voiceData.Frequency * ratios[p];
                    // Per-partial faster decay: upper partials decay faster
                    // When amplitude is 0.5: partial 0 = 0.5, partial 1 = 0.35, etc.
                    float partialDecay = Mathf.Pow(voiceData.Amplitude, 1f + p * 0.5f);
                    float partialAmp = partialDecay * weights[p];
                    sample += Mathf.Sin(_partialPhases[p]) * partialAmp;
                    _partialPhases[p] += freq * increment;
                    if (_partialPhases[p] > 2f * Mathf.PI)
                        _partialPhases[p] -= 2f * Mathf.PI;
                }

                // Strike transient
                if (_transientTimer > 0f)
                {
                    float noise = (float)(_rng.NextDouble() * 2.0 - 1.0);
                    float bandpass = noise * Mathf.Sin(_transientPhase);
                    _transientPhase += voiceData.Frequency * increment;
                    if (_transientPhase > 2f * Mathf.PI)
                        _transientPhase -= 2f * Mathf.PI;
                    float envelope = _transientTimer / _transientDuration;
                    sample += bandpass * envelope * _transientAmplitude * TransientMix;
                    _transientTimer -= 1f / sampleRate;
                }

                // Apply envelope and master volume scale
                sample *= _envelopeGain;
                sample *= 0.15f; // prevent clipping with multiple voices

                // Write to all channels
                for (int ch = 0; ch < channels; ch++)
                    data[i + ch] = sample;
            }
        }

        /// <summary>
        /// Triggers a strike transient with shape-dependent duration.
        /// Does NOT reset partial phases to maintain phase continuity.
        /// </summary>
        private void TriggerTransient(VoiceData voiceData, int sampleRate)
        {
            switch (voiceData.Shape)
            {
                case ShapeType.Bar:
                    _transientDuration = 0.005f;  // ~5ms steel-like
                    break;
                case ShapeType.Shell:
                    _transientDuration = 0.003f;  // ~3ms glass/bell-like
                    break;
                case ShapeType.Plate:
                    _transientDuration = 0.012f;  // ~12ms wood/panel-like
                    break;
                default:
                    _transientDuration = 0.005f;
                    break;
            }
            _transientTimer = _transientDuration;
            _transientAmplitude = voiceData.StrikeAmplitude;
            _transientPhase = 0f;
        }
    }
}
