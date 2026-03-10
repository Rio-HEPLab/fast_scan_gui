using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Threading.Tasks;


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

        [DllImport("DigitizerLib.dll", EntryPoint = "getWaveform")]
        public static extern int GetWaveform(int[] waveform, int channel, int baseline_numPoints);

        /********************************************************************/
        /*          Funções para operar em cima da waveform obtida          */
        /********************************************************************/
        public static int GetMaxAvgFromWaveform(int[] wf, int ch, int numSamples, int binStart, int binEnd, int baseline_numPoints )
        {
            int avgAmplitude = 0;

            for (int i = 0; i < numSamples; i++)
            {
                GetWaveform(wf, ch, baseline_numPoints);
                avgAmplitude += maxInInterval(wf, binStart, binEnd);
            }
            avgAmplitude/= numSamples;
            return avgAmplitude;
        }

        public static int GetMinAvgFromWaveform(int[] wf, int ch, int numSamples, int binStart, int binEnd, int baseline_numPoints)
        {
            int avgAmplitude = 0;

            for (int i = 0; i < numSamples; i++)
            {
                GetWaveform(wf, ch, baseline_numPoints);
                avgAmplitude += minInInterval(wf, binStart, binEnd);
            }
            avgAmplitude /= numSamples;
            return avgAmplitude;
        }

        private static int maxInInterval(int[] array, int a, int b)
        {
            int max = array[a];
            for (int i = a; i <= b; i++)
            {
                if (array[i] > max)
                    max = array[i];
            }
            return max;
        }

        private static int minInInterval(int[] array, int a, int b)
        {
            int min = array[a];
            for (int i = a; i <= b; i++)
            {
                if (array[i] < min)
                    min = array[i];
            }
            return min;
        }


        /********************************************************************/
        /*         Função para gerar Waveform de Teste -- SIMULAÇÃO         */
        /********************************************************************/

        [DllImport("testWaveformGenerator.dll", EntryPoint = "GenerateWaveform")]
        public static extern int GenerateWaveform_Sim(int[] waveform);

        public static int GetMaxAvgFromWaveform_Sim(int[] wf, int numSamples, int binStart, int binEnd)
        {
            int avgAmplitude = 0;

            for (int i = 0; i < numSamples; i++)
            {
                GenerateWaveform_Sim(wf);
                Task.Delay(1).Wait();
                avgAmplitude += maxInInterval(wf, binStart, binEnd);
            }
            avgAmplitude /= numSamples;
            return avgAmplitude;
        }

        public static int GetMinAvgFromWaveform_Sim(int[] wf, int numSamples, int binStart, int binEnd)
        {
            int avgAmplitude = 0;

            for (int i = 0; i < numSamples; i++)
            {
                GenerateWaveform_Sim(wf);
                avgAmplitude += minInInterval(wf, binStart, binEnd);
            }
            avgAmplitude /= numSamples;
            return avgAmplitude;
        }

    }
}
