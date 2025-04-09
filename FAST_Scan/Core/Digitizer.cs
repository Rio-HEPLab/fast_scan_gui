using System;
using System.Runtime.InteropServices;


namespace FAST_Scan.Core
{
    internal class Digitizer
    {
        [DllImport("DigitizerLib.dll", EntryPoint = "ConfigureDigitizer")]
        public static extern int Configure();

        [DllImport("DigitizerLib.dll", EntryPoint = "getMinValue")]
        public static extern int GetMinValue();

        [DllImport("DigitizerLib.dll", EntryPoint = "getMaxValue")]
        public static extern int GetMaxValue();

        [DllImport("DigitizerLib.dll", EntryPoint = "CloseDigitizer")]
        public static extern int Close();

        [DllImport("DigitizerLib.dll", EntryPoint = "getAvgMinValue")]
        public static extern int GetAvgMinValue(int numSamples);

        [DllImport("DigitizerLib.dll", EntryPoint = "getAvgMaxValue")]
        public static extern int GetAvgMaxValue(int numSamples);

        [DllImport("DigitizerLib.dll", EntryPoint = "getAvgMaxValueInterval")]
        public static extern int GetAvgMaxValueInterval(int numSamples, int binStart, int binEnd);

        [DllImport("DigitizerLib.dll", EntryPoint = "getAvgMinValueInterval")]
        public static extern int GetAvgMinValueInterval(int numSamples, int binStart, int binEnd);
    }
}
