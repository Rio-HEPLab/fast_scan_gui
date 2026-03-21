using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FAST_Scan.Core;

namespace FAST_Scan.MVVM.ViewModel
{
    internal class GraphContainerViewModel : ObservebleObject
    {
        GraphType _graphType;
        public RelayCommand ExpandWindowCommand { get; set; }
        public WaveformPlotViewModel WaveformPlotVM { get; set; }
        public HeatmapPlotViewModel HeatmapPlotVM { get; set; }
        public FocalPlotViewModel FocalPlotVM { get; set; }

        //Para garantir que apenas uma janela expandida de cada tipo seja aberta
        private static Dictionary<GraphType, GraphExpandedWindow> _openWindows
            = new Dictionary<GraphType, GraphExpandedWindow>();

        private object _currentGraphView;
        public object CurrentGraphView
        {
            get { return _currentGraphView; }
            set
            {
                _currentGraphView = value;
                OnPropertyChanged();
            }
        }

        public GraphContainerViewModel(GraphType graphType) 
        {
            _graphType = graphType;

            ExpandWindowCommand = new RelayCommand(o =>
            {
                OpenGraphWindow();
            });

            if (graphType == GraphType.Waveform)
            {
                WaveformPlotVM = new WaveformPlotViewModel();
                CurrentGraphView = WaveformPlotVM;
            }
            if (graphType == GraphType.FocalPlot)
            {
                FocalPlotVM = new FocalPlotViewModel();
                CurrentGraphView = FocalPlotVM;
            }
            if (graphType == GraphType.Heatmap)
            {
                HeatmapPlotVM = new HeatmapPlotViewModel();
                CurrentGraphView = HeatmapPlotVM;
            }
        }

        private void OpenGraphWindow()
        {
            if (_openWindows.ContainsKey(_graphType))
            {
                _openWindows[_graphType].Activate();
                return;
            }

            var window = new GraphExpandedWindow
            {
                DataContext = this
            };

            _openWindows[_graphType] = window;

            window.Closed += (s, e) =>
            {
                _openWindows.Remove(_graphType);
            };

            window.Show();
        }
    }
}
