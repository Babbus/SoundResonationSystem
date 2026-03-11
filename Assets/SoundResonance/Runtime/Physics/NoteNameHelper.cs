using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Converts frequencies in Hz to musical note names using the equal temperament tuning
    /// system (A4 = 440 Hz, 12 semitones per octave).
    ///
    /// The formula: semitones from A4 = 12 * log2(f / 440)
    ///
    /// Each semitone is a frequency ratio of 2^(1/12) ≈ 1.0595.
    /// An octave is exactly a factor of 2 in frequency.
    ///
    /// The cents deviation shows how far the frequency is from the nearest note:
    /// 100 cents = 1 semitone. ±50 cents means the frequency is exactly between two notes.
    /// </summary>
    public static class NoteNameHelper
    {
        private static readonly string[] NoteNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        /// <summary>
        /// Converts a frequency in Hz to the nearest musical note name with octave number
        /// and cents deviation.
        /// </summary>
        /// <param name="frequencyHz">Frequency in Hz. Must be positive.</param>
        /// <returns>String like "A4 +0c" or "C#5 -14c". Returns "—" for invalid frequencies.</returns>
        public static string FrequencyToNoteName(float frequencyHz)
        {
            if (frequencyHz <= 0f || float.IsNaN(frequencyHz) || float.IsInfinity(frequencyHz))
                return "\u2014"; // em dash

            // Semitones from A4 (440 Hz)
            float semitonesFromA4 = 12f * math.log2(frequencyHz / 440f);
            int nearestSemitone = (int)math.round(semitonesFromA4);
            float centsDeviation = (semitonesFromA4 - nearestSemitone) * 100f;

            // A4 is MIDI note 69. Convert semitone offset to absolute note number.
            int midiNote = 69 + nearestSemitone;
            int noteIndex = ((midiNote % 12) + 12) % 12; // handle negative modulo
            int octave = (midiNote / 12) - 1;

            // Handle negative MIDI notes (extremely low frequencies)
            if (midiNote < 0)
                octave = (midiNote - 11) / 12 - 1;

            int centsRounded = (int)math.round(centsDeviation);
            string sign = centsRounded >= 0 ? "+" : "";
            return $"{NoteNames[noteIndex]}{octave} {sign}{centsRounded}c";
        }
    }
}
