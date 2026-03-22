using NUnit.Framework;

namespace SoundResonance.Tests
{
    /// <summary>
    /// Validates the resonance math functions against known physical behavior.
    /// </summary>
    public class ResonanceMathTests
    {
        [Test]
        public void LorentzianResponse_PeaksAtNaturalFrequency()
        {
            // When driving frequency exactly matches natural frequency, response should be maximum (1.0)
            float response = ResonanceMath.LorentzianResponse(440f, 440f, 1000f);
            Assert.That(response, Is.InRange(0.95f, 1.05f),
                $"Response at resonance should be ~1.0, got {response}");
        }

        [Test]
        public void LorentzianResponse_FallsOffAwayFromResonance()
        {
            float Q = 1000f;
            float f0 = 440f;

            float atResonance = ResonanceMath.LorentzianResponse(f0, f0, Q);
            float slightlyOff = ResonanceMath.LorentzianResponse(f0 + 1f, f0, Q);
            float farOff = ResonanceMath.LorentzianResponse(f0 + 100f, f0, Q);

            Assert.Greater(atResonance, slightlyOff, "Response should decrease away from resonance");
            Assert.Greater(slightlyOff, farOff, "Response should continue decreasing further away");
        }

        [Test]
        public void LorentzianResponse_HighQ_NarrowPeak()
        {
            float f0 = 440f;
            float highQ = 10000f;
            float lowQ = 10f;

            // At 1Hz offset from resonance
            float highQResponse = ResonanceMath.LorentzianResponse(f0 + 1f, f0, highQ);
            float lowQResponse = ResonanceMath.LorentzianResponse(f0 + 1f, f0, lowQ);

            // High Q should have fallen off more at the same offset because its peak is narrower
            Assert.Less(highQResponse, lowQResponse,
                "High Q should have narrower peak (lower response at same offset)");
        }

        [Test]
        public void LorentzianResponse_HighQ_NearZeroFarFromResonance()
        {
            // Steel Q ≈ 10000 at 440Hz. 10% frequency offset (44Hz) should give near-zero response.
            // Bandwidth = f0/Q = 0.044Hz, so 44Hz offset is ~1000 bandwidths away.
            float response = ResonanceMath.LorentzianResponse(484f, 440f, 10000f);
            Assert.Less(response, 0.01f,
                $"High-Q response 10% from resonance should be near zero, got {response}");
        }

        [Test]
        public void LorentzianResponse_ZeroFrequency_ReturnsZero()
        {
            float response = ResonanceMath.LorentzianResponse(440f, 0f, 1000f);
            Assert.AreEqual(0f, response);
        }

        [Test]
        public void DriveTimeConstant_HighQ_SlowBuildup()
        {
            // Steel at 440Hz: tau = 2*Q/omega0 = 2*10000/(2*pi*440) ≈ 7.2 seconds
            float tau = ResonanceMath.DriveTimeConstant(440f, 10000f);
            Assert.That(tau, Is.InRange(5f, 10f),
                $"Steel time constant at 440Hz should be ~7.2s, got {tau}s");
        }

        [Test]
        public void DriveTimeConstant_LowQ_FastBuildup()
        {
            // Wood at 440Hz: tau = 2*100/(2*pi*440) ≈ 0.072 seconds
            float tau = ResonanceMath.DriveTimeConstant(440f, 100f);
            Assert.That(tau, Is.InRange(0.05f, 0.1f),
                $"Wood time constant at 440Hz should be ~0.072s, got {tau}s");
        }

        [Test]
        public void ExponentialDecay_DecreasesOverTime()
        {
            float A0 = 1.0f;
            float f0 = 440f;
            float Q = 1000f;

            float A1 = ResonanceMath.ExponentialDecay(A0, 0.1f, f0, Q);
            float A2 = ResonanceMath.ExponentialDecay(A0, 1.0f, f0, Q);
            float A3 = ResonanceMath.ExponentialDecay(A0, 5.0f, f0, Q);

            Assert.Less(A1, A0, "Amplitude should decrease after 0.1s");
            Assert.Less(A2, A1, "Amplitude should decrease further after 1.0s");
            Assert.Less(A3, A2, "Amplitude should decrease further after 5.0s");
        }

        [Test]
        public void ExponentialDecay_AtTimeConstant_Reaches37Percent()
        {
            float A0 = 1.0f;
            float f0 = 440f;
            float Q = 1000f;
            float tau = ResonanceMath.DriveTimeConstant(f0, Q);

            float atTau = ResonanceMath.ExponentialDecay(A0, tau, f0, Q);

            // At t = tau, amplitude should be ~1/e ≈ 0.368
            Assert.That(atTau, Is.InRange(0.33f, 0.40f),
                $"Amplitude at time constant should be ~0.368, got {atTau}");
        }

        [Test]
        public void InverseSquareAttenuation_AtReference_IsOne()
        {
            float gain = ResonanceMath.InverseSquareAttenuation(1f, 1f);
            Assert.AreEqual(1f, gain, 0.001f, "Gain at reference distance should be 1.0");
        }

        [Test]
        public void InverseSquareAttenuation_AtDoubleDistance_IsQuarter()
        {
            float gain = ResonanceMath.InverseSquareAttenuation(2f, 1f);
            Assert.That(gain, Is.InRange(0.24f, 0.26f),
                $"Gain at 2x distance should be 0.25, got {gain}");
        }

        [Test]
        public void InverseSquareAttenuation_CloserThanReference_ClampedToOne()
        {
            float gain = ResonanceMath.InverseSquareAttenuation(0.5f, 1f);
            Assert.AreEqual(1f, gain, 0.001f, "Gain closer than reference should be clamped to 1.0");
        }

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

        [Test]
        public void DrivenOscillatorStep_HighQ_SlowApproach()
        {
            float current = 0f;
            float target = 1f;
            float f0 = 440f;
            float Q = 10000f; // Very high Q — slow buildup

            // Simulate 0.1 second at 60fps
            for (int i = 0; i < 6; i++)
            {
                current = ResonanceMath.DrivenOscillatorStep(current, target, 1f / 60f, f0, Q);
            }

            // After 0.1s with tau ≈ 7.2s, should barely have moved
            Assert.Less(current, 0.05f,
                $"After 0.1s with high-Q drive, amplitude should still be near zero, got {current}");
        }
    }
}
