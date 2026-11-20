using FAST_Scan.Core.Analysis;
using FAST_Scan.MVVM.ViewModel;
using ScottPlot;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace FAST_Scan.MVVM.View
{
    /// <summary>
    /// Interação lógica para TimeResolutionView.xaml
    /// A View só desenha: recebe TimeResolutionDisplay do ViewModel (waveform ou histograma).
    /// </summary>
    public partial class TimeResolutionView : UserControl
    {
        private TimeResolutionViewModel viewModel;
        private TimeResolutionDisplay lastDisplay;

        // 1 enquanto um desenho já está agendado na thread da interface.
        private int drawPending;

        public TimeResolutionView()
        {
            InitializeComponent();
            MainPlot.UserInputProcessor.IsEnabled = false;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (viewModel != null) viewModel.DisplayReady -= OnDisplayReady;

            viewModel = DataContext as TimeResolutionViewModel;
            if (viewModel != null) viewModel.DisplayReady += OnDisplayReady;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                viewModel.DisplayReady -= OnDisplayReady;
                viewModel = null;
            }
        }

        // Chamado na thread de análise.
        private void OnDisplayReady(TimeResolutionDisplay display)
        {
            lastDisplay = display;
            if (Interlocked.Exchange(ref drawPending, 1) == 1)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { Draw(lastDisplay); }
                finally { Interlocked.Exchange(ref drawPending, 0); }
            }));
        }

        private void OnViewModeChanged(object sender, RoutedEventArgs e)
        {
            if (MainPlot == null || ShowHistogramRadio == null) return;   // durante InitializeComponent
            Draw(lastDisplay);
        }

        private void Draw(TimeResolutionDisplay display)
        {
            if (display == null) return;
            bool histogram = ShowHistogramRadio.IsChecked == true;
            MainPlot.Plot.Clear();
            if (histogram) DrawHistogram(display); else DrawWaveform(display);
            MainPlot.Plot.Axes.AutoScale();
            MainPlot.Refresh();
        }

        // Segmento de reta como Scatter sem marcadores (evita depender de APIs específicas de linhas).
        private void AddLine(double x1, double y1, double x2, double y2, Color color, float width)
        {
            var line = MainPlot.Plot.Add.Scatter(new double[] { x1, x2 }, new double[] { y1, y2 });
            line.Color = color;
            line.LineWidth = width;
            line.MarkerSize = 0;
        }

        private void DrawWaveform(TimeResolutionDisplay d)
        {
            if (d.Volts == null)
            {
                MainPlot.Plot.Title("Aguardando eventos...");
                return;
            }

            double xMin = d.TimeNs[0];
            double xMax = d.TimeNs[d.TimeNs.Length - 1];

            var signal = MainPlot.Plot.Add.SignalXY(d.TimeNs, d.Volts);
            signal.Color = Colors.Blue;
            signal.LineWidth = 1.5f;

            if (d.TriggerVolts != null)
            {
                var trig = MainPlot.Plot.Add.SignalXY(d.TimeNs, d.TriggerVolts);
                trig.Color = Colors.Gray;
                trig.LineWidth = 1f;
            }

            if (!double.IsNaN(d.LevelVolts))
                AddLine(xMin, d.LevelVolts, xMax, d.LevelVolts, Colors.Orange, 1.5f);   // limiar
            if (!double.IsNaN(d.BaselineVolts))
                AddLine(d.BaselineStartNs, d.BaselineVolts, d.BaselineEndNs, d.BaselineVolts, Colors.Green, 3f);   // janela da baseline
            if (!double.IsNaN(d.PeakNs) && !double.IsNaN(d.PeakVolts))
            {
                var peak = MainPlot.Plot.Add.Scatter(new double[] { d.PeakNs }, new double[] { d.PeakVolts });
                peak.Color = Colors.Purple;
                peak.MarkerSize = 9;
                peak.LineWidth = 0;
            }
            if (!double.IsNaN(d.CrossingNs) && !double.IsNaN(d.LevelVolts))
                AddLine(d.CrossingNs, d.LevelVolts - 0.02, d.CrossingNs, d.LevelVolts + 0.02, Colors.Red, 2f);     // cruzamento
            if (!double.IsNaN(d.TriggerCrossingNs))
                AddLine(d.TriggerCrossingNs, -0.05, d.TriggerCrossingNs, 0.05, Colors.Black, 2f);                   // trigger digitalizado

            MainPlot.Plot.Title(d.WaveformTitle ?? "Waveform");
            MainPlot.Plot.XLabel("Tempo relativo ao trigger (ns)");
            MainPlot.Plot.YLabel("Tensão (V)");
        }

        private void DrawHistogram(TimeResolutionDisplay d)
        {
            VariantStats s = d.Stats;
            if (s == null || s.Count == 0 || s.Counts == null)
            {
                MainPlot.Plot.Title("Sem dados para: " + d.VariantName);
                return;
            }

            // Contorno em degraus do histograma.
            int bins = s.Counts.Length;
            double[] xs = new double[bins * 4];
            double[] ys = new double[bins * 4];
            for (int b = 0; b < bins; b++)
            {
                double left = s.RangeMin + b * s.BinWidth;
                double right = left + s.BinWidth;
                xs[4 * b] = left;      ys[4 * b] = 0;
                xs[4 * b + 1] = left;  ys[4 * b + 1] = s.Counts[b];
                xs[4 * b + 2] = right; ys[4 * b + 2] = s.Counts[b];
                xs[4 * b + 3] = right; ys[4 * b + 3] = 0;
            }
            var outline = MainPlot.Plot.Add.Scatter(xs, ys);
            outline.Color = Colors.Blue;
            outline.LineWidth = 1.5f;
            outline.MarkerSize = 0;

            string title = d.VariantName + ": N = " + s.Count + ", média = " + s.Mean.ToString("0.000") + " ns, std = " + (s.Std * 1000).ToString("0.0") + " ps";
            if (s.Fit != null && s.Fit.Ok && s.CurveX != null)
            {
                var curve = MainPlot.Plot.Add.Scatter(s.CurveX, s.CurveY);
                curve.Color = Colors.Red;
                curve.LineWidth = 2f;
                curve.MarkerSize = 0;
                title += ", σ(fit) = " + (s.Fit.Sigma * 1000).ToString("0.0") + " ps";
            }

            MainPlot.Plot.Title(title);
            MainPlot.Plot.XLabel("Δt = pulso - trigger (ns)");
            MainPlot.Plot.YLabel("Contagens");
        }

        private void OnSaveCsvClick(object sender, RoutedEventArgs e)
        {
            if (viewModel == null) return;
            string csv = viewModel.BuildCsv();
            if (csv == null)
            {
                MessageBox.Show("Ainda não há eventos válidos para salvar.", "Salvar Δt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = "time_resolution_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
            };
            if (dialog.ShowDialog() == true)
                File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(true));
        }
    }
}
