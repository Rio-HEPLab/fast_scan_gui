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
        public WaveformPlotViewModel WaveformPlotVM { get; set; }
        public HeatmapPlotViewModel HeatmapPlotVM { get; set; }
        public FocalPlotViewModel FocalPlotVM { get; set; }


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

        public GraphContainerViewModel()
        {
            WaveformPlotVM = new WaveformPlotViewModel();
            HeatmapPlotVM = new HeatmapPlotViewModel();
            FocalPlotVM = new FocalPlotViewModel();

            //CurrentGraphView = FocalPlotVM;
            CurrentGraphView = WaveformPlotVM;

            //if(AppManager.State == AppState.SCAN_1D)
            //{
            //    CurrentGraphView = WaveformPlotVM;
            //}
            //else if(AppManager.State == AppState.SCAN_2D)
            //{
            //    CurrentGraphView = HeatmapPlotVM;
            //}
        }
    }
}
