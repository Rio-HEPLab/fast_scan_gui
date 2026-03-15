using FAST_Scan.Core;
using FAST_Scan.MVVM.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.MVVM.ViewModel
{
    internal class GraphViewModel : ObservebleObject
    {
        public WaveformPlotViewModel WaveformPlotVM { get; set; }
        public HeatmapPlotViewModel HeatmapPlotVM { get; set; }
        public GraphContainerViewModel GraphContainerVM { get; set; }
        public FocalPlotViewModel FocalPlotVM { get; set; }



        private object _currentView;
        public object CurrentView
        {
            get { return _currentView; }
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        public GraphViewModel()
        {
            WaveformPlotVM = new WaveformPlotViewModel();
            HeatmapPlotVM = new HeatmapPlotViewModel();
            FocalPlotVM = new FocalPlotViewModel();

            GraphContainerVM = new GraphContainerViewModel();
            //CurrentView = GraphContainerVM;
            CurrentView = FocalPlotVM;
        }

    }
}
