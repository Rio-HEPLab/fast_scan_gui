using FAST_Scan.Core;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Interação lógica para FocalPlotView.xam
    /// </summary>
    public partial class FocalPlotView : UserControl
    {
        private DispatcherTimer timer;
        public FocalPlotView()
        {
            InitializeComponent();
            PlotFocal.Plot.Add.SignalXY(DataStore.FocalData_x, DataStore.FocalData_y);

            PlotFocal.UserInputProcessor.IsEnabled = false;
            //PlotFocal.Plot.Axes.SetLimitsY(425, 475);
            PlotFocal.Plot.Axes.AutoScaleY();
            PlotFocal.Plot.Axes.SetLimitsX(0,(DataStore.NumPoints - 1) * DataStore.StepValue);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(50);
            timer.Tick += (s, e) =>
            {
                var valid = DataStore.FocalData_y.Where(v => !double.IsNaN(v));

                if (valid.Any())
                {
                    double min = valid.Min();
                    double max = valid.Max();
                    double margin = 0.05 * (max - min);

                    PlotFocal.Plot.Axes.SetLimitsY(min-margin, max+margin);
                }
                PlotFocal.Refresh();
            };

            timer.Start();
        }

    }
}
