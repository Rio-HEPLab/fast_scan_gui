using FAST_Scan.Core;
using FAST_Scan.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.MVVM.ViewModel
{
    internal class HeatmapPlotViewModel : IGraphActions
    {
        public RelayCommand SaveImageCommand { get; private set; }
        public Action SaveImageAction { get; set; }

        public HeatmapPlotViewModel() 
        {
            SaveImageCommand = new RelayCommand( o=>SaveImage());
        }

        public void SaveImage()
        {
            SaveImageAction?.Invoke();
        }
    }
}
