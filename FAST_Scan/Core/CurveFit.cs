using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class CurveFit
    {
        private static Func<double, double, double, double, double, double> sCurve = (sigma, x0, I, C, x) => 
        {
            return 0.5 * I * (1+SpecialFunctions.Erf((x-x0)/(Math.Sqrt(2)*sigma))) + C;
        }; 

        public static (double sigma, double x0, double I, double C) SCurve(double[] x, double[] y, double[] p)
        {
            double _s = p[0];
            double _x0 = p[1];
            double _I = p[2];
            double _C = p[3];
            return Fit.Curve(x, y, sCurve, _s, _x0, _I, _C );
        }
    }
}
