using FAST_Scan.MVVM.ViewModel;
using ScottPlot;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace FAST_Scan.MVVM.View
{
    /// <summary>
    /// Interação lógica para WaveformViewerView.xaml
    /// A View só desenha: recebe WaveformDisplay do ViewModel e atualiza o gráfico.
    /// </summary>
    public partial class WaveformViewerView : UserControl
    {
        private WaveformViewerViewModel viewModel;

        // 1 enquanto um desenho já está agendado na thread da interface.
        // Se chegar outro bloco nesse intervalo, ele não é desenhado (a aquisição não espera).
        private int drawPending;

        public WaveformViewerView()
        {
            InitializeComponent();

            WaveformPlot.UserInputProcessor.IsEnabled = false;   // mesmo comportamento da WaveformPlotView
            WaveformPlot.Plot.Title("Waveform");

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // evita inscrição duplicada
            if (viewModel != null) viewModel.DisplayReady -= OnDisplayReady;

            viewModel = DataContext as WaveformViewerViewModel;
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

        // Chamado na thread de aquisição.
        private void OnDisplayReady(WaveformDisplay display)
        {
            if (Interlocked.Exchange(ref drawPending, 1) == 1)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { Draw(display); }
                finally { Interlocked.Exchange(ref drawPending, 0); }
            }));
        }

        private void Draw(WaveformDisplay display)
        {
            WaveformPlot.Plot.Clear();

            if (display.Traces != null)
            {
                DrawTraces(display);
                return;
            }
            WaveformPlot.Plot.HideLegend();

            for (int i = 0; i < display.Frames.Count; i++)
            {
                bool isLast = i == display.Frames.Count - 1;
                var signal = WaveformPlot.Plot.Add.SignalXY(display.X, display.Frames[i]);
                signal.Color = isLast ? Colors.Red : Colors.Blue.WithAlpha(0.3);
                signal.LineWidth = isLast ? 1.5f : 1f;
            }

            WaveformPlot.Plot.Title(display.Title);
            WaveformPlot.Plot.XLabel(display.XLabel);
            WaveformPlot.Plot.YLabel(display.YLabel);
            WaveformPlot.Plot.Axes.AutoScale();
            WaveformPlot.Refresh();
        }

        // Cores fixas por posição: a mesma curva mantém a cor entre atualizações.
        private static readonly string[] TraceColors =
        {
            "#D62728", "#1F77B4", "#2CA02C", "#FF7F0E", "#9467BD",
            "#17BECF", "#E377C2", "#8C564B", "#7F7F7F", "#BCBD22"
        };

        // Várias curvas do mesmo evento (canais e TR), com legenda.
        private void DrawTraces(WaveformDisplay display)
        {
            for (int i = 0; i < display.Traces.Count; i++)
            {
                var signal = WaveformPlot.Plot.Add.SignalXY(display.X, display.Traces[i].Y);
                signal.Color = Color.FromHex(TraceColors[i % TraceColors.Length]);
                signal.LineWidth = 1.5f;
                signal.LegendText = display.Traces[i].Label;
            }

            WaveformPlot.Plot.ShowLegend(Alignment.UpperRight);
            WaveformPlot.Plot.Title(display.Title);
            WaveformPlot.Plot.XLabel(display.XLabel);
            WaveformPlot.Plot.YLabel(display.YLabel);
            WaveformPlot.Plot.Axes.AutoScale();
            WaveformPlot.Refresh();
        }
    }
}
