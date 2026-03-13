using FAST_Scan.Core;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

namespace FAST_Scan.MVVM.View
{
    /// <summary>
    /// Interação lógica para HeatmapPlotView.xam
    /// </summary>
    public partial class HeatmapPlotView : UserControl
    {
        private DispatcherTimer timer;
        private ScottPlot.Plottables.Heatmap heatmap;
        public HeatmapPlotView()
        {
            InitializeComponent();

            // desativa interação do usuário
            PlotHeatmap.UserInputProcessor.IsEnabled = false;

            heatmap = PlotHeatmap.Plot.Add.Heatmap(DataStore.Heatmap);
            PlotHeatmap.Plot.Add.ColorBar(heatmap);
            ConfigureHeatmapPlot(heatmap);

            //PlotHeatmap.Plot.Axes.SetLimits(0, DataStore.HeatmapCols, 0, DataStore.HeatmapRows);



            PlotHeatmap.Refresh();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(50);


            timer.Tick += (s, e) =>
            {
                if (DataStore.HeatmapUpdated)
                {
                    PlotHeatmap.Plot.Remove(heatmap);
                    heatmap = PlotHeatmap.Plot.Add.Heatmap(DataStore.Heatmap);
                    ConfigureHeatmapPlot(heatmap);

                    PlotHeatmap.Refresh();
                    DataStore.HeatmapUpdated = false;
                }
            };

            timer.Start();
        }

        public void ConfigureHeatmapPlot(ScottPlot.Plottables.Heatmap plot)
        {
            plot.Colormap = new ScottPlot.Colormaps.Plasma();
            //Ajusta escala para milimetros
            heatmap.Extent = new ScottPlot.CoordinateRect(
                0,
                (DataStore.HeatmapCols-1) * Scan.getStepX(),
                0,
                (DataStore.HeatmapRows-1) * Scan.getStepY()
            );
            //Ajusta os limites de visualização do gráfico
            PlotHeatmap.Plot.Axes.SetLimits(
                0-Scan.getStepX() / 2,
                Scan.getSpanX() + Scan.getStepX() / 2,
                0 - Scan.getStepY() / 2,
                Scan.getSpanY() + Scan.getStepX() / 2
            );
            //heatmap.Smooth = true;
        }
    }
}
