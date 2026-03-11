using Unity.Burst;
using Unity.Mathematics;

namespace SoundResonance
{
    /// <summary>
    /// Pure static math functions for resonance simulation. All methods are Burst-compatible.
    ///
    /// These functions implement the physics of a driven damped harmonic oscillator —
    /// the fundamental model underlying ALL resonance phenomena, from tuning forks to
    /// bridges to radio circuits.
    ///
    /// The governing differential equation is:
    ///   m*x'' + b*x' + k*x = F0*cos(omega*t)
    /// where m=mass, b=damping, k=stiffness, F0=driving force amplitude, omega=driving frequency.
    ///
    /// This equation has two key solutions:
    /// 1. The STEADY-STATE response (how the object responds to continuous driving):
    ///    amplitude is given by the Lorentzian curve as a function of driving frequency.
    /// 2. The TRANSIENT response (how the object approaches or leaves steady state):
    ///    exponential approach to steady state (when driven) or exponential decay (when released).
    /// </summary>
    [BurstCompile]
    public static class ResonanceMath
    {
        /// <summary>
        /// Minimum amplitude below which we consider the object at rest.
        /// Prevents infinitely long decay tails that waste computation.
        /// Value chosen to be well below audible threshold (~-60dB = 0.001).
        /// </summary>
        public const float AmplitudeThreshold = 0.0001f;

        /// <summary>
        /// Computes the Lorentzian (Cauchy) frequency response of a damped harmonic oscillator.
        ///
        /// This is the exact analytical solution for how strongly a resonant system responds
        /// to a driving force at frequency f, given that its natural frequency is f0 and its
        /// quality factor is Q.
        ///
        /// Formula: A(f) = 1 / sqrt((1 - r^2)^2 + (r/Q)^2)   where r = f/f0
        ///
        /// At f = f0 (r = 1): response = Q (maximum, limited only by damping)
        /// At f far from f0: response → 0 (the system doesn't care about that frequency)
        ///
        /// The result is normalized so the peak response = 1.0 (we divide by Q).
        /// This makes it a transfer coefficient [0,1] suitable for scaling amplitudes.
        ///
        /// Why Lorentzian and not Gaussian:
        /// The Lorentzian emerges directly from solving the differential equation of a damped
        /// harmonic oscillator. It has "fatter tails" than a Gaussian — meaning there IS a
        /// small response even at frequencies far from resonance. This is physically correct:
        /// a tuning fork at 440Hz does technically respond to 200Hz, just barely.
        /// A Gaussian would suppress this response too aggressively.
        ///
        /// The -3dB bandwidth (frequency range where response > 0.707 of peak) is:
        ///   Bandwidth = f0 / Q
        /// For steel (Q ≈ 10000) at 440Hz, bandwidth ≈ 0.044Hz — incredibly selective.
        /// For wood (Q ≈ 100) at 440Hz, bandwidth ≈ 4.4Hz — much broader.
        /// </summary>
        /// <param name="drivingFrequency">Frequency of the driving force in Hz.</param>
        /// <param name="naturalFrequency">Natural frequency of the resonator in Hz.</param>
        /// <param name="qFactor">Quality factor (1/lossFactor) of the resonator.</param>
        /// <returns>Response amplitude normalized to [0, 1] where 1 = peak resonance.</returns>
        [BurstCompile]
        public static float LorentzianResponse(float drivingFrequency, float naturalFrequency,
            float qFactor)
        {
            if (naturalFrequency <= 0f || qFactor <= 0f) return 0f;

            float r = drivingFrequency / naturalFrequency;
            float r2 = r * r;
            float term1 = (1f - r2) * (1f - r2);
            float term2 = (r / qFactor) * (r / qFactor);

            // Raw response peaks at Q, so divide by Q to normalize to [0,1]
            float rawResponse = 1f / math.sqrt(term1 + term2);
            return rawResponse / qFactor;
        }

        /// <summary>
        /// Computes the time constant for amplitude buildup of a driven resonant system.
        ///
        /// When you start driving a resonant system at its natural frequency, the amplitude
        /// doesn't jump instantly to its steady-state value. It builds up exponentially
        /// with time constant tau:
        ///   tau = 2*Q / omega0 = Q / (pi * f0)
        ///
        /// After time tau, amplitude reaches ~63% of steady state.
        /// After 3*tau, ~95%. After 5*tau, ~99%.
        ///
        /// Physical intuition: high-Q systems take longer to build up because they lose
        /// less energy per cycle — they need many cycles to accumulate energy. This is why
        /// an opera singer must hold the note for several seconds before the wine glass shatters:
        /// the glass has high Q, so energy accumulates slowly but to a very high level.
        ///
        /// For steel (Q ≈ 10000) at 440Hz: tau ≈ 7.2 seconds (!). Very slow buildup.
        /// For wood (Q ≈ 100) at 440Hz: tau ≈ 0.072 seconds. Nearly instant.
        /// </summary>
        /// <param name="naturalFrequency">Natural frequency in Hz.</param>
        /// <param name="qFactor">Quality factor.</param>
        /// <returns>Time constant in seconds.</returns>
        [BurstCompile]
        public static float DriveTimeConstant(float naturalFrequency, float qFactor)
        {
            if (naturalFrequency <= 0f) return 0f;
            float omega0 = 2f * math.PI * naturalFrequency;
            return 2f * qFactor / omega0;
        }

        /// <summary>
        /// Computes the exponential decay of amplitude after driving force is removed.
        ///
        /// Formula: A(t) = A0 * exp(-t * omega0 / (2*Q))
        ///
        /// This is the "ring-down" — how a bell, tuning fork, or wine glass fades to silence.
        /// The decay rate is determined by the same Q-factor that controls the resonance width:
        /// high-Q objects ring longer because they lose less energy per cycle.
        ///
        /// The decay time constant (time to reach 1/e ≈ 37% of initial amplitude) is
        /// the same as the drive time constant: tau = 2*Q / omega0.
        /// This symmetry is not a coincidence — it's because the same physical damping
        /// mechanism controls both how fast energy enters and how fast it leaves.
        /// </summary>
        /// <param name="initialAmplitude">Amplitude when driving stopped.</param>
        /// <param name="timeSinceStop">Time elapsed since driving stopped, in seconds.</param>
        /// <param name="naturalFrequency">Natural frequency in Hz.</param>
        /// <param name="qFactor">Quality factor.</param>
        /// <returns>Current amplitude after decay.</returns>
        [BurstCompile]
        public static float ExponentialDecay(float initialAmplitude, float timeSinceStop,
            float naturalFrequency, float qFactor)
        {
            if (naturalFrequency <= 0f || qFactor <= 0f) return 0f;
            float omega0 = 2f * math.PI * naturalFrequency;
            float decayRate = omega0 / (2f * qFactor);
            return initialAmplitude * math.exp(-timeSinceStop * decayRate);
        }

        /// <summary>
        /// Computes inverse-square law attenuation for sound intensity over distance.
        ///
        /// Formula: gain = (referenceDistance / distance)^2, clamped to [0, 1]
        ///
        /// Sound waves in air spread as an expanding sphere. The power per unit area
        /// (intensity) drops as 1/r^2 because the same total power is spread over a
        /// sphere of surface area 4*pi*r^2. This is not an approximation — it's the
        /// geometry of 3D space.
        ///
        /// The referenceDistance parameter defines the distance at which gain = 1.0
        /// (no attenuation). This prevents infinite amplitude at distance = 0.
        /// A value of 1.0m is standard in audio engineering.
        ///
        /// At 2x reference distance: gain = 0.25 (quarter intensity, -6dB)
        /// At 10x reference distance: gain = 0.01 (1% intensity, -20dB)
        /// </summary>
        /// <param name="distance">Distance between emitter and receiver in meters.</param>
        /// <param name="referenceDistance">Distance at which gain = 1.0. Default: 1.0m.</param>
        /// <returns>Gain factor [0, 1].</returns>
        [BurstCompile]
        public static float InverseSquareAttenuation(float distance, float referenceDistance = 1f)
        {
            if (distance <= referenceDistance) return 1f;
            float ratio = referenceDistance / distance;
            return ratio * ratio;
        }

        /// <summary>
        /// Performs one discrete time step of the driven harmonic oscillator.
        ///
        /// This is the discrete-time approximation of the continuous buildup:
        ///   amplitude(t+dt) = lerp(amplitude(t), targetAmplitude, 1 - exp(-dt/tau))
        ///
        /// The (1 - exp(-dt/tau)) factor ensures frame-rate independence: whether the game
        /// runs at 30fps or 144fps, the amplitude converges to the target at the same
        /// physical rate. This is the standard approach for exponential smoothing in games.
        ///
        /// targetAmplitude is the steady-state amplitude the system would reach if driven
        /// indefinitely at the current driving force. It equals the driving force amplitude
        /// times the Lorentzian response times the distance attenuation.
        /// </summary>
        /// <param name="currentAmplitude">Current vibration amplitude.</param>
        /// <param name="targetAmplitude">Steady-state target amplitude from driving force.</param>
        /// <param name="deltaTime">Frame time step in seconds.</param>
        /// <param name="naturalFrequency">Natural frequency in Hz.</param>
        /// <param name="qFactor">Quality factor.</param>
        /// <returns>Updated amplitude after one time step.</returns>
        [BurstCompile]
        public static float DrivenOscillatorStep(float currentAmplitude, float targetAmplitude,
            float deltaTime, float naturalFrequency, float qFactor)
        {
            float tau = DriveTimeConstant(naturalFrequency, qFactor);
            if (tau <= 0f) return targetAmplitude;
            float alpha = 1f - math.exp(-deltaTime / tau);
            return math.lerp(currentAmplitude, targetAmplitude, alpha);
        }
    }
}
