using FAST_Scan.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.MVVM.ViewModel
{
    internal class ScanTabControlViewModel: ObservebleObject
    {
        //Objetos para visualização dos gráficos
        public GraphContainerViewModel WaveformGraphContainerVM;
        public GraphContainerViewModel ScanGraphContainerVM;

        //Objetos para visualização das configurações
        public Scan1DViewModel Scan1DVM;
        public Scan2DViewModel Scan2DVM;

        private object _waveformGraphView;
        private object _scanGraphView;
        private object _scanConfigurationsView;

        public string Title { get; set; } = "Meu título dinâmico";
        string Scan1D_Title = "Scan 1D";
        string Scan2D_Title = "Scan 2D";


        public object ScanConfigurationsView
        {
            get { return _scanConfigurationsView; }
            set
            {
                _scanConfigurationsView = value;
                OnPropertyChanged();
            }
        }

        public object WaveformGraphView
        {
            get { return _waveformGraphView; }
            set
            {
                _waveformGraphView = value;
                OnPropertyChanged();
            }
        }

        public object ScanGraphView
        {
            get { return _scanGraphView; }
            set
            {
                _scanGraphView = value;
                OnPropertyChanged();
            }
        }

        public ScanTabControlViewModel(AppState appState)
        {
            if(appState == AppState.SCAN_1D)
            {
                WaveformGraphContainerVM = new GraphContainerViewModel(GraphType.Waveform);
                ScanGraphContainerVM = new GraphContainerViewModel(GraphType.FocalPlot);
                Scan1DVM = new Scan1DViewModel();
                ScanConfigurationsView = Scan1DVM;
                Title = Scan1D_Title;
            }
            if (appState == AppState.SCAN_2D)
            //if(true)
            {
                WaveformGraphContainerVM = new GraphContainerViewModel(GraphType.Waveform);
                ScanGraphContainerVM = new GraphContainerViewModel(GraphType.Heatmap);
                Scan2DVM = new Scan2DViewModel();
                ScanConfigurationsView = Scan2DVM;
                Title = Scan2D_Title;
            }

            ScanGraphView = ScanGraphContainerVM;
            WaveformGraphView = WaveformGraphContainerVM;
        }
    }
}
