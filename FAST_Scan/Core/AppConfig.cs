using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace FAST_Scan.Core
{

    //Classe que grava os parametros  de scan para salvar no arquivo json
    internal class AppConfig
    {
        public Scan1DParams params1D { get; set; } = new Scan1DParams();
        public Scan2DParams params2D { get; set; } = new Scan2DParams();
        public string PulsePolarity { get; set; } = null;
        public string DigitizerSamples { get; set; } = null;
    }

    internal class Scan1DParams
    {
        public Scan.Axis Axis { get; set; } = Scan.Axis.X;
        public string InitialPosition { get; set; } = null;
        public string FinalPosition { get; set; } = null;
        public string NumberOfSteps { get; set; } = null;
        public string XPosition { get; set; } = null;
        public string YPosition { get; set; } = null;
        public string ZPosition { get; set; } = null;
        public bool? SetX { get; set; } = true;
        public bool? SetY { get; set; } = false;
        public bool? SetZ { get; set; } = false;
    }

    internal class Scan2DParams
    {
        public string X_InitialPosition { get; set; } = null;
        public string X_FinalPosition { get; set; } = null;
        public string X_NumberOfSteps { get; set; } = null;
        public string Y_InitialPosition { get; set; } = null;
        public string Y_FinalPosition { get; set; } = null;
        public string Y_NumberOfSteps { get; set; } = null;
        public string ZPosition { get; set; } = null;
        public bool? SetZ { get; set; } = false;
    }
}
