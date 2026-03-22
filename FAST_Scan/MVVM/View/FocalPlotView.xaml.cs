using FAST_Scan.Core;
using FAST_Scan.MVVM.ViewModel;
using Microsoft.Win32;
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
    /// 
    public partial class FocalPlotView : UserControl
    {

        private ScottPlot.Plottables.SignalXY plotFocal;
        public FocalPlotView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            plotFocal = PlotFocal.Plot.Add.SignalXY(DataStore.FocalData_x, DataStore.FocalData_y);

            PlotFocal.Plot.Title("Scan 1D");
            PlotFocal.Plot.XLabel("Amplitude");
            PlotFocal.Plot.YLabel("Position (mm)");

            PlotFocal.UserInputProcessor.IsEnabled = false;
            //PlotFocal.Plot.Axes.SetLimitsY(425, 475);
            ConfigureFocalPlot();

            DataStore.FocalPlotChanged += OnFocalPlotChanged;
            DataStore.NewFocalPlotChanged += OnNewFocalPlotChanged;
        }

        private void OnFocalPlotChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var valid = DataStore.FocalData_y.Where(v => !double.IsNaN(v));

                if (valid.Any())
                {
                    double min = valid.Min();
                    double max = valid.Max();
                    double margin = 0.05 * (max - min);

                    PlotFocal.Plot.Axes.SetLimitsY(min - margin, max + margin);
                }
                PlotFocal.Refresh();

            });
        }

        private void OnNewFocalPlotChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PlotFocal.Plot.Remove(plotFocal);
                plotFocal = PlotFocal.Plot.Add.SignalXY(DataStore.FocalData_x, DataStore.FocalData_y);
                ConfigureFocalPlot();
                PlotFocal.Refresh();
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // evita duplicação de inscrição
            DataStore.FocalPlotChanged -= OnFocalPlotChanged;
            DataStore.FocalPlotChanged += OnFocalPlotChanged;

            Console.WriteLine("INSCRITA: " + GetHashCode());
            //Atualizar quando a view reaparecer na tela
            DataStore.NotifyFocalPlotChanged();

            //Quando botão de salvar é pressionado, salva plot
            if (DataContext is FocalPlotViewModel vm)
            {
                vm.SaveImageAction = SaveImage;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DataStore.FocalPlotChanged -= OnFocalPlotChanged;

            Console.WriteLine("REMOVIDA: " + GetHashCode());
        }

        private void ConfigureFocalPlot()
        {
            PlotFocal.Plot.Axes.AutoScaleY();
            PlotFocal.Plot.Axes.SetLimitsX(0, (DataStore.NumPoints - 1) * DataStore.StepValue);
        }

        public void SaveImage()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG Image (*.png)|*.png";
            bool? success = saveFileDialog.ShowDialog();
            if (success == true)
            {
                string path = saveFileDialog.FileName;
                PlotFocal.Plot.SavePng(path, 800, 600);
            }
            else
            {
                //não selecionou nada
            };
        }
    }
}
