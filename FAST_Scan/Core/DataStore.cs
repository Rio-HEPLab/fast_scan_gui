using ScottPlot.MultiplotLayouts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ScottPlot;

namespace FAST_Scan.Core
{
    //Classe que armazena valores globais para variaveis utilizadas em plots
    public static class DataStore
    {
        public static int[] Waveform = new int[1024];

        public static double[,] Heatmap { get; private set; }
        public static int HeatmapRows { get; private set; }
        public static int HeatmapCols { get; private set; }

        public static bool HeatmapUpdated;


        public static void HeatmapCreate(int rows, int cols)
        {
            HeatmapRows = rows;
            HeatmapCols = cols;
            Heatmap = new double[rows, cols];
            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < rows; j++)
                    Heatmap[i, j] = double.NaN;
            }
            Heatmap[0,0] = 0;
        }

        public static void HeatmapAddPoint(int x, int y, double amplitude) 
        {
            Heatmap[x,y] = amplitude;
            HeatmapUpdated = true;
        }

    }
}
