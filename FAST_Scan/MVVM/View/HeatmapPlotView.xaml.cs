using FAST_Scan.Core;
using ScottPlot;
using ScottPlot.Panels;
using ScottPlot.Plottables;
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
        private ScottPlot.Panels.ColorBar colorbar;
        public HeatmapPlotView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            Console.WriteLine("VIEW CRIADA: " + GetHashCode());

            // desativa interação do usuário
            PlotHeatmap.UserInputProcessor.IsEnabled = false;

            PlotHeatmap.Plot.Title("2D Scan");
            PlotHeatmap.Plot.XLabel("x (mm)");
            PlotHeatmap.Plot.YLabel("y (mm)");

            heatmap = PlotHeatmap.Plot.Add.Heatmap(DataStore.Heatmap);
            ConfigureHeatmapPlot(heatmap);
            PlotHeatmap.Refresh();

            DataStore.HeatmapChanged += OnHeatmapChanged;
            DataStore.NewHeatmapChanged += OnNewHeatmapChanged;

        }

        public void ConfigureHeatmapPlot(ScottPlot.Plottables.Heatmap plot)
        {
            plot.Colormap = new ScottPlot.Colormaps.Plasma();
            if(colorbar != null)
            {
                PlotHeatmap.Plot.Remove(colorbar);
            }
            colorbar = PlotHeatmap.Plot.Add.ColorBar(heatmap);
            Console.WriteLine("Tipo Colorbar: " + colorbar.GetType());
            colorbar.Label = "Amplitude";
            colorbar.Axis.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
            //Ajusta escala para milimetros
            //heatmap.Extent = new ScottPlot.CoordinateRect(
            plot.Extent = new ScottPlot.CoordinateRect(
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

        private void OnHeatmapChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                heatmap.Update();
                //ConfigureHeatmapPlot(heatmap);
                PlotHeatmap.Refresh();
            });
        }

        private void OnNewHeatmapChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PlotHeatmap.Plot.Remove(heatmap);
                heatmap = PlotHeatmap.Plot.Add.Heatmap(DataStore.Heatmap);
                ConfigureHeatmapPlot(heatmap);
                PlotHeatmap.Refresh();
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // evita duplicação de inscrição
            DataStore.HeatmapChanged -= OnHeatmapChanged;
            DataStore.HeatmapChanged += OnHeatmapChanged;

            Console.WriteLine("INSCRITA: " + GetHashCode());
            DataStore.NotifyHeatmapChanged();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DataStore.HeatmapChanged -= OnHeatmapChanged;

            Console.WriteLine("REMOVIDA: " + GetHashCode());
        }
    }
}
