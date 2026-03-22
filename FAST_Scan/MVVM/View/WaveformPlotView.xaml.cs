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
using FAST_Scan.MVVM.ViewModel;
using Microsoft.Win32;

namespace FAST_Scan.MVVM.View
{
    /// <summary>
    /// Interação lógica para WaveformPlotView.xam
    /// </summary>
    public partial class WaveformPlotView : UserControl
    {
        private DispatcherTimer timer;
        public WaveformPlotView()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // desativa interação do usuário
            WaveformPlot.UserInputProcessor.IsEnabled = false;

            WaveformPlot.Plot.Title("Waveform");
            WaveformPlot.Plot.XLabel("Bin");
            WaveformPlot.Plot.YLabel("Amplitude");

            WaveformPlot.Plot.Axes.SetLimits(
                left: 0,
                right: 1023,
                bottom: -500,
                top: 500);

            WaveformPlot.Plot.Add.Signal(DataStore.Waveform);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(50);
            timer.Tick += (s,e) =>
            {
                WaveformPlot.Refresh();
            };

            timer.Start();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //Quando botão de salvar é pressionado, salva plot
            if (DataContext is WaveformPlotViewModel vm)
            {
                vm.SaveImageAction = SaveImage;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

        }

        public void SaveImage()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "PNG Image (*.png)|*.png";
            bool? success = saveFileDialog.ShowDialog();
            if (success == true)
            {
                string path = saveFileDialog.FileName;
                WaveformPlot.Plot.SavePng(path, 800, 600);
            }
            else
            {
                //não selecionou nada
            };
        }
    }
}
