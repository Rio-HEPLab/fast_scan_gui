using System;
using System.Threading;

namespace FAST_Scan.Core.Oscilloscope
{
    // Parâmetros do simulador (valores "verdadeiros" que a análise deve recuperar).
    public class SimulationSettings
    {
        public double DelayNs { get; set; } = 9.8;            // atraso médio do pulso após o trigger
        public double JitterPs { get; set; } = 50;            // jitter gaussiano (sigma) do pulso
        public double NoiseMv { get; set; } = 1.0;            // ruído rms
        public double AmplitudeDivisions { get; set; } = 3.0; // amplitude do pulso em divisões verticais
        public double EmptyFractionPercent { get; set; } = 10; // frames sem pulso
        public bool XZeroPlusConvention { get; set; } = true; // convenção "verdadeira" do XZero
    }

    // Gera blocos sintéticos com o mesmo formato do osciloscópio real (bytes com sinal,
    // 8 bits, XZero por frame), para testar a análise sem sensor.
    // Modelo: o trigger ocorre no tempo 0; a amostra i do frame está em
    // t_i = (i - TriggerIndex)*dt + sinal*XZero, com XZero uniforme em [0, dt).
    public class SimulatedScope : IBlockSource
    {
        private readonly ScopeSettings settings;
        private readonly SimulationSettings sim;
        private readonly Random random = new Random(Environment.TickCount);
        private bool opened;

        public event Action<string> Message;
        public event Action<string> Trace;

        public SimulatedScope(ScopeSettings settings, SimulationSettings sim)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (sim == null) throw new ArgumentNullException("sim");
            this.settings = settings;
            this.sim = sim;
        }

        public void Open()
        {
            opened = true;
            Raise("SIMULATION: no real instrument is being used.");
        }

        public void Apply()
        {
            if (!opened) throw new ScopeException("The simulator has not been opened.");
            Raise("SIMULATION: " + settings.FrameCount + " frames of " + settings.RecordLength + " points at " +
                  (settings.SampleRateHz / 1e9).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " GS/s.");
        }

        public void Dispose()
        {
            opened = false;
        }

        private double Gauss()
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private static byte Quantize(double volts, double yMult)
        {
            double code = Math.Round(volts / yMult);
            if (code > 127) code = 127;
            if (code < -128) code = -128;
            return unchecked((byte)(sbyte)code);
        }

        public WaveformBlock CaptureBlock(CancellationToken token)
        {
            if (!opened) throw new ScopeException("The simulator has not been opened.");
            token.ThrowIfCancellationRequested();

            // Emula o tempo de captura do osciloscópio real (~2,5 ms por frame).
            int sleepMs = (int)Math.Min(5000, settings.FrameCount * 2.5);
            for (int waited = 0; waited < sleepMs; waited += 50)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(Math.Min(50, sleepMs - waited));
            }

            int n = settings.RecordLength;
            int frames = settings.FrameCount;
            double dt = 1.0 / settings.SampleRateHz;
            double triggerIndex = settings.TriggerPositionPercent / 100.0 * n;
            double yMult = settings.VerticalScaleVoltsPerDiv * 10.0 / 250.0;
            double trigYMult = settings.TriggerVerticalScaleVoltsPerDiv * 10.0 / 250.0;
            double noise = sim.NoiseMv / 1000.0;
            double sign = sim.XZeroPlusConvention ? 1.0 : -1.0;

            // Pulso: A * (exp(-x/tf) - exp(-x/tr)) normalizado para ter pico A.
            const double tauRise = 0.3e-9;
            const double tauFall = 1.0e-9;
            double xPeak = Math.Log(tauFall / tauRise) * tauFall * tauRise / (tauFall - tauRise);
            double norm = 1.0 / (Math.Exp(-xPeak / tauFall) - Math.Exp(-xPeak / tauRise));

            // Trigger: rampa com 4 V/ns passando pelo nível em t = 0.
            const double slew = 4e9;
            double slewSign = settings.Slope == TriggerSlope.Falling ? -1.0 : 1.0;

            var block = new WaveformBlock
            {
                FrameCount = frames,
                PointsPerFrame = n,
                YMultiplier = yMult,
                YOffset = 0,
                YZero = 0,
                XIncrementSeconds = dt,
                TriggerIndex = triggerIndex,
                RawData = new byte[(long)frames * n]
            };
            if (settings.ReadXZero) block.XZeroSeconds = new double[frames];
            if (settings.DigitizeTriggerChannel)
            {
                block.TriggerRawData = new byte[(long)frames * n];
                block.TriggerYMultiplier = trigYMult;
                block.TriggerYOffset = 0;
                block.TriggerYZero = 0;
            }

            for (int f = 0; f < frames; f++)
            {
                double xzero = random.NextDouble() * dt;
                if (block.XZeroSeconds != null) block.XZeroSeconds[f] = xzero;

                bool hasPulse = random.NextDouble() * 100.0 >= sim.EmptyFractionPercent;
                double t0 = sim.DelayNs * 1e-9 + Gauss() * sim.JitterPs * 1e-12;
                double amplitude = sim.AmplitudeDivisions * settings.VerticalScaleVoltsPerDiv * (1.0 + 0.15 * Gauss());
                if (amplitude < 0.0001) amplitude = 0.0001;

                int offset = f * n;
                for (int i = 0; i < n; i++)
                {
                    double ts = (i - triggerIndex) * dt + sign * xzero;
                    double v = noise * Gauss();
                    if (hasPulse && ts > t0)
                    {
                        double x = ts - t0;
                        if (x < 12e-9)
                            v += amplitude * norm * (Math.Exp(-x / tauFall) - Math.Exp(-x / tauRise));
                    }
                    block.RawData[offset + i] = Quantize(v, yMult);

                    if (block.TriggerRawData != null)
                    {
                        double vt = settings.TriggerLevelVolts + slewSign * slew * ts;
                        double lowest = settings.TriggerLevelVolts - 0.5;
                        double highest = settings.TriggerLevelVolts + 0.5;
                        if (vt < lowest) vt = lowest;
                        if (vt > highest) vt = highest;
                        vt += 0.002 * Gauss();
                        block.TriggerRawData[offset + i] = Quantize(vt, trigYMult);
                    }
                }
            }

            block.CaptureMs = sleepMs;
            return block;
        }

        private void Raise(string text)
        {
            Action<string> handler = Message;
            if (handler != null) handler(text);
        }
    }
}
