using System;
using FAST_Scan.Core.Oscilloscope;

namespace FAST_Scan.Core.Analysis
{
    public enum RejectReason
    {
        None = 0,
        NoPulse,            // nenhuma amostra acima do limiar na janela
        MultiplePulses,     // mais de uma região contínua acima do limiar
        StartsAbove,        // já estava acima do limiar no início da janela (cruzamento não observado)
        BadBaselineWindow,  // janela da baseline fora do frame
        BadSearchWindow,    // janela de busca fora do frame
        Saturated           // pulso saturou o ADC (só rejeita se a opção estiver ligada)
    }

    public class PulseTimingResult
    {
        public bool Valid { get; set; }
        public RejectReason Reason { get; set; }
        public double CrossingIndex { get; set; }   // índice (fracionário, se interpolado) do cruzamento
        public double BaselineVolts { get; set; } = double.NaN;
        public double LevelVolts { get; set; } = double.NaN;       // portão de detecção: baseline +/- limiar
        public double ReferenceVolts { get; set; } = double.NaN;   // nível onde o cruzamento foi medido
        public double AmplitudeVolts { get; set; }                 // amplitude (média em torno do pico), módulo
        public int PeakIndex { get; set; } = -1;                   // índice da amostra do pico
        public bool Saturated { get; set; }                        // o pulso tocou o fundo de escala do ADC
    }

    // Análise de um evento. Funções puras: não guardam estado, podem rodar em qualquer thread.
    public static class PulseTimingAnalyzer
    {
        // Índice (amostras) -> ns relativos ao trigger nominal.
        public static double IndexToNs(double index, WaveformBlock block)
        {
            return (index - block.TriggerIndex) * block.XIncrementSeconds * 1e9;
        }

        public static PulseTimingResult AnalyzePulse(double[] v, WaveformBlock block, PulseTimingSettings s)
        {
            var result = new PulseTimingResult();
            int n = block.PointsPerFrame;
            double dtNs = block.XIncrementSeconds * 1e9;

            // Baseline = média na janela [b0, b1).
            int b0 = Math.Max(0, (int)Math.Round(block.TriggerIndex + s.BaselineStartNs / dtNs));
            int b1 = Math.Min(n, (int)Math.Round(block.TriggerIndex + s.BaselineEndNs / dtNs));
            if (b1 <= b0)
            {
                result.Reason = RejectReason.BadBaselineWindow;
                return result;
            }
            double sum = 0;
            for (int i = b0; i < b1; i++) sum += v[i];
            double baseline = sum / (b1 - b0);
            result.BaselineVolts = baseline;

            // Janela de busca [j0, j1] (inclusive).
            int j0 = Math.Max(0, (int)Math.Round(block.TriggerIndex + s.SearchStartNs / dtNs));
            int j1 = Math.Min(n - 1, (int)Math.Round(block.TriggerIndex + s.SearchEndNs / dtNs));
            if (j1 - j0 < 1)
            {
                result.Reason = RejectReason.BadSearchWindow;
                return result;
            }

            double pol = s.Polarity == PulsePolarity.Positive ? 1.0 : -1.0;
            double gate = baseline + pol * s.ThresholdVolts;
            result.LevelVolts = gate;
            result.ReferenceVolts = gate;

            // Conta as regiões contínuas acima do portão e guarda início e fim da primeira.
            int runs = 0;
            int first = -1;
            int firstEnd = -1;
            bool startsAbove = false;
            bool previous = false;
            for (int i = j0; i <= j1; i++)
            {
                bool above = pol * (v[i] - gate) > 0;
                if (above && !previous)
                {
                    runs++;
                    if (runs == 1)
                    {
                        first = i;
                        if (i == j0) startsAbove = true;
                    }
                }
                else if (!above && previous && runs == 1 && firstEnd < 0)
                {
                    firstEnd = i - 1;
                }
                previous = above;
            }
            if (runs == 1 && firstEnd < 0) firstEnd = j1;

            if (runs == 0) { result.Reason = RejectReason.NoPulse; return result; }
            if (runs > 1 && s.RequireSingleRun) { result.Reason = RejectReason.MultiplePulses; return result; }
            if (startsAbove) { result.Reason = RejectReason.StartsAbove; return result; }

            // Pico, saturação e amplitude (média de N amostras em torno do pico).
            int peakIndex = first;
            double peakRel = double.NegativeInfinity;
            bool saturated = false;
            for (int i = first; i <= firstEnd; i++)
            {
                double rel = pol * (v[i] - baseline);
                if (rel > peakRel) { peakRel = rel; peakIndex = i; }
                if (pol > 0 ? v[i] >= block.SaturationHighVolts : v[i] <= block.SaturationLowVolts)
                    saturated = true;
            }
            int points = Math.Min(Math.Max(1, s.AmplitudeAveragePoints), n);
            int windowStart = peakIndex - points / 2;
            if (windowStart < 0) windowStart = 0;
            if (windowStart + points > n) windowStart = n - points;
            double sumAmplitude = 0;
            for (int k = windowStart; k < windowStart + points; k++)
                sumAmplitude += pol * (v[k] - baseline);

            result.PeakIndex = peakIndex;
            result.AmplitudeVolts = sumAmplitude / points;
            result.Saturated = saturated;

            if (saturated && s.RejectSaturated) { result.Reason = RejectReason.Saturated; return result; }

            if (s.ReferenceMode == PulseReferenceMode.FixedThreshold)
            {
                result.CrossingIndex = s.LinearInterpolation ? Interpolate(v[first - 1], v[first], gate, first - 1) : first;
            }
            else
            {
                // Fração constante: a partir do pico, anda para trás até a última amostra
                // que não está acima do nível de referência (a subida do pulso).
                double level = baseline + pol * s.FractionOfAmplitude * result.AmplitudeVolts;
                result.ReferenceVolts = level;
                int below = -1;
                for (int i = peakIndex - 1; i >= j0; i--)
                {
                    if (pol * (v[i] - level) <= 0) { below = i; break; }
                }
                if (below < 0) { result.Reason = RejectReason.StartsAbove; return result; }
                result.CrossingIndex = s.LinearInterpolation ? Interpolate(v[below], v[below + 1], level, below) : below + 1;
            }

            result.Valid = true;
            result.Reason = RejectReason.None;
            return result;
        }

        // Procura o cruzamento do nível do trigger no canal do trigger digitalizado,
        // perto do índice nominal do trigger. Devolve NaN se não achar.
        public static double FindTriggerCrossing(double[] v, WaveformBlock block, PulseTimingSettings s)
        {
            int n = block.PointsPerFrame;
            double dtNs = block.XIncrementSeconds * 1e9;
            int half = (int)Math.Ceiling(s.TriggerSearchHalfWidthNs / dtNs);
            int lo = Math.Max(1, (int)Math.Floor(block.TriggerIndex) - half);
            int hi = Math.Min(n - 1, (int)Math.Ceiling(block.TriggerIndex) + half);
            double level = s.TriggerLevelVolts;

            for (int i = lo; i <= hi; i++)
            {
                double prev = v[i - 1];
                double cur = v[i];
                bool crossed = s.TriggerFalling
                    ? (prev > level && cur <= level)
                    : (prev < level && cur >= level);
                if (crossed)
                    return s.LinearInterpolation ? Interpolate(prev, cur, level, i - 1) : i;
            }
            return double.NaN;
        }

        // Índice fracionário onde a reta entre (index, v0) e (index+1, v1) passa por 'level'.
        private static double Interpolate(double v0, double v1, double level, int index)
        {
            double dv = v1 - v0;
            if (dv == 0) return index + 1;
            return index + (level - v0) / dv;
        }
    }
}
