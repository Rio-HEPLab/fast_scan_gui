using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FAST_Scan.Core.Analysis
{
    // Resultado do ajuste gaussiano y = A exp(-(x-mu)^2 / (2 sigma^2)) (mínimos quadrados sem pesos,
    // como o curve_fit do notebook). As incertezas vêm da matriz de covariância (JtJ)^-1 * RSS/(n-3).
    public class GaussianFit
    {
        public bool Ok { get; set; }
        public double Amplitude { get; set; }
        public double Mu { get; set; }
        public double Sigma { get; set; }
        public double MuError { get; set; }
        public double SigmaError { get; set; }

        public double Evaluate(double x)
        {
            double d = (x - Mu) / Sigma;
            return Amplitude * Math.Exp(-0.5 * d * d);
        }

        public static GaussianFit Fit(double[] x, double[] y, double sigmaGuess, double muGuess)
        {
            var fit = new GaussianFit();
            int n = x.Length;
            if (n < 4) return fit;

            double a0 = 0;
            for (int i = 0; i < n; i++) if (y[i] > a0) a0 = y[i];
            if (a0 <= 0 || sigmaGuess <= 0) return fit;

            double[] p = { a0, muGuess, sigmaGuess };
            double rss = Rss(x, y, p);
            double lambda = 1e-3;
            double[,] jtj = new double[3, 3];

            for (int iter = 0; iter < 200; iter++)
            {
                double[] jtr = new double[3];
                Array.Clear(jtj, 0, 9);
                BuildNormalEquations(x, y, p, jtj, jtr);

                double[,] damped = (double[,])jtj.Clone();
                for (int k = 0; k < 3; k++) damped[k, k] *= (1 + lambda);

                double[] delta;
                bool improved = false;
                if (Solve3(damped, jtr, out delta))
                {
                    double[] trial = { p[0] + delta[0], p[1] + delta[1], p[2] + delta[2] };
                    if (trial[2] != 0)
                    {
                        double trialRss = Rss(x, y, trial);
                        if (trialRss < rss)
                        {
                            double gain = (rss - trialRss) / Math.Max(rss, 1e-300);
                            p = trial;
                            rss = trialRss;
                            lambda = Math.Max(lambda / 10, 1e-12);
                            improved = true;
                            if (gain < 1e-12) break;
                        }
                    }
                }
                if (!improved)
                {
                    lambda *= 10;
                    if (lambda > 1e12) break;
                }
            }

            fit.Amplitude = p[0];
            fit.Mu = p[1];
            fit.Sigma = Math.Abs(p[2]);
            fit.Ok = !double.IsNaN(fit.Sigma) && !double.IsInfinity(fit.Sigma) && fit.Sigma > 0;

            // Covariância: (JtJ)^-1 * s^2, s^2 = RSS/(n-3).
            Array.Clear(jtj, 0, 9);
            BuildNormalEquations(x, y, p, jtj, new double[3]);
            double s2 = n > 3 ? rss / (n - 3) : double.NaN;
            double[] e0 = null, e1 = null, e2 = null;
            bool inv = Solve3((double[,])jtj.Clone(), new double[] { 1, 0, 0 }, out e0) &&
                       Solve3((double[,])jtj.Clone(), new double[] { 0, 1, 0 }, out e1) &&
                       Solve3((double[,])jtj.Clone(), new double[] { 0, 0, 1 }, out e2);
            if (inv && !double.IsNaN(s2))
            {
                // e1 = coluna 1 da inversa -> variância de mu = e1[1]; e2[2] -> sigma.
                fit.MuError = Math.Sqrt(Math.Max(0, e1[1] * s2));
                fit.SigmaError = Math.Sqrt(Math.Max(0, e2[2] * s2));
            }
            else
            {
                fit.MuError = double.NaN;
                fit.SigmaError = double.NaN;
            }
            return fit;
        }

        private static double Rss(double[] x, double[] y, double[] p)
        {
            double sum = 0;
            for (int i = 0; i < x.Length; i++)
            {
                double d = (x[i] - p[1]) / p[2];
                double r = y[i] - p[0] * Math.Exp(-0.5 * d * d);
                sum += r * r;
            }
            return sum;
        }

        private static void BuildNormalEquations(double[] x, double[] y, double[] p, double[,] jtj, double[] jtr)
        {
            double a = p[0], mu = p[1], s = p[2];
            for (int i = 0; i < x.Length; i++)
            {
                double dx = x[i] - mu;
                double e = Math.Exp(-0.5 * dx * dx / (s * s));
                double g = a * e;
                double r = y[i] - g;
                double[] j = { e, g * dx / (s * s), g * dx * dx / (s * s * s) };
                for (int u = 0; u < 3; u++)
                {
                    jtr[u] += j[u] * r;
                    for (int w = 0; w < 3; w++) jtj[u, w] += j[u] * j[w];
                }
            }
        }

        // Eliminação de Gauss com pivotamento parcial para sistemas 3x3.
        private static bool Solve3(double[,] a, double[] b, out double[] x)
        {
            x = new double[3];
            double[] rhs = (double[])b.Clone();
            for (int col = 0; col < 3; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < 3; r++)
                    if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
                if (Math.Abs(a[pivot, col]) < 1e-300) return false;
                if (pivot != col)
                {
                    for (int c = 0; c < 3; c++) { double t = a[col, c]; a[col, c] = a[pivot, c]; a[pivot, c] = t; }
                    double tb = rhs[col]; rhs[col] = rhs[pivot]; rhs[pivot] = tb;
                }
                for (int r = col + 1; r < 3; r++)
                {
                    double f = a[r, col] / a[col, col];
                    for (int c = col; c < 3; c++) a[r, c] -= f * a[col, c];
                    rhs[r] -= f * rhs[col];
                }
            }
            for (int r = 2; r >= 0; r--)
            {
                double sum = rhs[r];
                for (int c = r + 1; c < 3; c++) sum -= a[r, c] * x[c];
                x[r] = sum / a[r, r];
            }
            return true;
        }
    }

    // Estatísticas e histograma de uma variante de Δt (tudo em ns).
    public class VariantStats
    {
        public int Count { get; set; }
        public double Mean { get; set; }
        public double Std { get; set; }          // desvio padrão amostral (ddof = 1)
        public double Min { get; set; }
        public double Max { get; set; }

        public double RangeMin { get; set; }
        public double RangeMax { get; set; }
        public double BinWidth { get; set; }
        public double[] BinCenters { get; set; }
        public double[] Counts { get; set; }

        public GaussianFit Fit { get; set; }
        public double[] CurveX { get; set; }
        public double[] CurveY { get; set; }
    }

    // Acumula os Δt dos eventos válidos (uma lista por variante) e calcula estatísticas.
    // Thread-safe: a thread de análise escreve, a interface lê por cópias (snapshots).
    public class TimeResolutionAccumulator
    {
        public const int VariantCount = 4;

        private readonly object sync = new object();
        private readonly List<double>[] deltas = new List<double>[VariantCount];
        private readonly int[] rejectCounts = new int[Enum.GetValues(typeof(RejectReason)).Length];
        private int triggerMissing;
        private int saturated;
        private double amplitudeSum;
        private int amplitudeCount;
        private int valid;

        public TimeResolutionAccumulator()
        {
            for (int i = 0; i < VariantCount; i++) deltas[i] = new List<double>();
        }

        public int ValidEvents { get { lock (sync) return valid; } }

        public long FramesAnalyzed
        {
            get
            {
                lock (sync)
                {
                    long total = valid;
                    for (int i = 0; i < rejectCounts.Length; i++) total += rejectCounts[i];
                    return total;
                }
            }
        }

        public void AddRejected(RejectReason reason)
        {
            lock (sync) rejectCounts[(int)reason]++;
        }

        public void AddSaturated()
        {
            lock (sync) saturated++;
        }

        public void AddAmplitude(double volts)
        {
            lock (sync) { amplitudeSum += volts; amplitudeCount++; }
        }

        // Amplitude média dos eventos válidos (V); NaN se ainda não há eventos.
        public double MeanAmplitudeVolts
        {
            get { lock (sync) return amplitudeCount > 0 ? amplitudeSum / amplitudeCount : double.NaN; }
        }

        public void AddTriggerMissing()
        {
            lock (sync) triggerMissing++;
        }

        // dts: um valor por variante (NaN = indisponível neste evento).
        public void AddEvent(double[] dts)
        {
            lock (sync)
            {
                for (int i = 0; i < VariantCount; i++) deltas[i].Add(dts[i]);
                valid++;
            }
        }

        public string RejectSummary()
        {
            lock (sync)
            {
                var sb = new StringBuilder();
                sb.Append("no pulse " + rejectCounts[(int)RejectReason.NoPulse]);
                sb.Append(" | multiple " + rejectCounts[(int)RejectReason.MultiplePulses]);
                sb.Append(" | already above " + rejectCounts[(int)RejectReason.StartsAbove]);
                int bad = rejectCounts[(int)RejectReason.BadBaselineWindow] + rejectCounts[(int)RejectReason.BadSearchWindow];
                if (bad > 0) sb.Append(" | invalid window " + bad);
                if (saturated > 0) sb.Append(" | SATURATED " + saturated);
                if (triggerMissing > 0) sb.Append(" | trigger not found " + triggerMissing);
                return sb.ToString();
            }
        }

        // Cópia sem NaN dos Δt de uma variante.
        public double[] SnapshotDeltas(int variant)
        {
            lock (sync)
            {
                var list = new List<double>(deltas[variant].Count);
                foreach (double d in deltas[variant])
                    if (!double.IsNaN(d)) list.Add(d);
                return list.ToArray();
            }
        }

        // gridStepNs > 0: os Δt são quantizados numa grade (passo, origem) e, com faixa automática,
        // os bins passam a ter exatamente o passo da grade, centrados nos pontos possíveis
        // (bins mais estreitos que o passo deixam bins vazios intercalados e estragam o ajuste).
        public VariantStats ComputeStats(int variant, bool autoRange, double rangeMin, double rangeMax, int bins,
                                         double gridStepNs = 0, double gridOriginNs = 0)
        {
            double[] data = SnapshotDeltas(variant);
            var stats = new VariantStats { Count = data.Length };
            if (data.Length == 0) return stats;

            if (bins < 5) bins = 5;
            if (bins > 2000) bins = 2000;

            double sum = 0, min = double.MaxValue, max = double.MinValue;
            foreach (double d in data)
            {
                sum += d;
                if (d < min) min = d;
                if (d > max) max = d;
            }
            double mean = sum / data.Length;
            double sq = 0;
            foreach (double d in data) sq += (d - mean) * (d - mean);
            double std = data.Length > 1 ? Math.Sqrt(sq / (data.Length - 1)) : 0;

            stats.Mean = mean;
            stats.Std = std;
            stats.Min = min;
            stats.Max = max;

            // Faixa do histograma.
            double lo, hi;
            if (!autoRange && rangeMax > rangeMin)
            {
                lo = rangeMin;
                hi = rangeMax;
            }
            else
            {
                lo = Math.Max(min, mean - 5 * std);
                hi = Math.Min(max, mean + 5 * std);
                if (hi - lo < 1e-9) { lo -= 0.5; hi += 0.5; }
            }
            if (autoRange && gridStepNs > 0)
            {
                double gLo = gridOriginNs + (Math.Round((lo - gridOriginNs) / gridStepNs) - 0.5) * gridStepNs;
                double gHi = gridOriginNs + (Math.Round((hi - gridOriginNs) / gridStepNs) + 0.5) * gridStepNs;
                int gBins = (int)Math.Round((gHi - gLo) / gridStepNs);
                if (gBins < 5) { gHi = gLo + 5 * gridStepNs; gBins = 5; }
                if (gBins <= 2000)
                {
                    lo = gLo;
                    hi = gHi;
                    bins = gBins;
                }
            }
            stats.RangeMin = lo;
            stats.RangeMax = hi;
            stats.BinWidth = (hi - lo) / bins;

            double[] counts = new double[bins];
            double[] centers = new double[bins];
            for (int b = 0; b < bins; b++) centers[b] = lo + (b + 0.5) * stats.BinWidth;
            foreach (double d in data)
            {
                if (d < lo || d > hi) continue;
                int idx = (int)((d - lo) / stats.BinWidth);
                if (idx >= bins) idx = bins - 1;
                counts[idx]++;
            }
            stats.BinCenters = centers;
            stats.Counts = counts;

            // Ajuste gaussiano no histograma. Chute inicial: média e desvio dos dados dentro da faixa.
            if (data.Length >= 10)
            {
                double inSum = 0, inSq = 0, inN = 0;
                foreach (double d in data)
                {
                    if (d < lo || d > hi) continue;
                    inSum += d; inN++;
                }
                if (inN > 1)
                {
                    double inMean = inSum / inN;
                    foreach (double d in data)
                    {
                        if (d < lo || d > hi) continue;
                        inSq += (d - inMean) * (d - inMean);
                    }
                    double inStd = Math.Sqrt(inSq / (inN - 1));
                    if (inStd <= 0) inStd = stats.BinWidth;
                    GaussianFit fit = GaussianFit.Fit(centers, counts, inStd, inMean);
                    stats.Fit = fit;
                    if (fit.Ok)
                    {
                        int points = 200;
                        stats.CurveX = new double[points];
                        stats.CurveY = new double[points];
                        for (int i = 0; i < points; i++)
                        {
                            double x = lo + (hi - lo) * i / (points - 1);
                            stats.CurveX[i] = x;
                            stats.CurveY[i] = fit.Evaluate(x);
                        }
                    }
                }
            }
            return stats;
        }

        // CSV de todos os eventos válidos (NaN fica em branco).
        public string BuildCsv()
        {
            lock (sync)
            {
                var sb = new StringBuilder();
                sb.AppendLine("event,dt_no_correction_ns,dt_xzero_plus_ns,dt_xzero_minus_ns,dt_digitized_trigger_ns");
                for (int e = 0; e < valid; e++)
                {
                    sb.Append((e + 1).ToString(CultureInfo.InvariantCulture));
                    for (int v = 0; v < VariantCount; v++)
                    {
                        sb.Append(',');
                        double d = deltas[v][e];
                        if (!double.IsNaN(d)) sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    }
                    sb.AppendLine();
                }
                return sb.ToString();
            }
        }
    }
}
