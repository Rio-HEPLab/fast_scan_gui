using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Interfaces
{
    internal interface IGraphSavable
    {
        //Implementa interface para salvar os plots das views de gráfico, em que cada uma das views implementa a função de salvar
        void SaveImage();
    }
}
