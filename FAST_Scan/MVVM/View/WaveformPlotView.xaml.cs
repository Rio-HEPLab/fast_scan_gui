using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ScottPlot;
using FAST_Scan.Core;

namespace FAST_Scan.MVVM.View
{
    /// <summary>
    /// Interação lógica para WaveformPlotView.xam
    /// </summary>
    public partial class WaveformPlotView : UserControl
    {
        private DispatcherTimer timer;
        private Random rand = new Random();

        private double[] x;
        private double[] y;
        public WaveformPlotView()
        {
            InitializeComponent();

            // desativa interação do usuário
            PlotArea.UserInputProcessor.IsEnabled = false;

            PlotArea.Plot.Title("Random Vector (1024 pontos)");
            PlotArea.Plot.XLabel("Index");
            PlotArea.Plot.YLabel("Value");

            PlotArea.Plot.Axes.SetLimits(
                left: 0,
                right: 1023,
                bottom: -500,
                top: 500);

            PlotArea.Plot.Add.Signal(DataStore.Waveform);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(50);
            timer.Tick += (s,e) =>
            {
                PlotArea.Refresh();
            };

            timer.Start();
        }
    }
}
