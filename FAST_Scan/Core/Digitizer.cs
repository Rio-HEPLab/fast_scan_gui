using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using FAST_Scan.Core.Caen;

namespace FAST_Scan.Core
{
    // Fachada compatível com a antiga Digitizer (DigitizerLib.dll), agora sobre a dgtz.dll.
    // Cada GetWaveform faz start -> lê 1 evento -> stop, como o modelo antigo: os scans precisam
    // de um evento novo a cada ponto (eventos guardados na placa ficariam velhos após mover o stage).
    internal class Digitizer
    {
        private const int BaseTimeoutMs = 5000;

        private static readonly object sync = new object();
        private static DgtzDevice device;
        private static DigitizerEventBlock block;
        private static bool logHooked;
        private static DigitizerSettings settings;

        // Criado sob demanda: sem dgtz.dll o modo simulado continua funcionando.
        public static DigitizerSettings Settings
        {
            get
            {
                if (settings == null) settings = DigitizerSettings.CreateDefault();
                return settings;
            }
        }

        public static DigitizerInfo Info { get { return device != null ? device.Info : null; } }

        // Convenção da DLL antiga: 0 = sucesso, diferente de zero = falha (o Scan e o viewer testam 'if (Configure())').
        public static int Configure()
        {
            lock (sync)
            {
                try
                {
                    CloseUnlocked();
                    EnsureLogHook();
                    device = DgtzDevice.Open(Settings);
                    block = device.CreateBlock(1);
                    Logger.Instance.Log("Digitizer opened: " + device.Info.Model, LogType.Info);
                    return 0;
                }
                catch (DigitizerException ex)
                {
                    Logger.Instance.Log("Digitizer error: " + ex.Message, LogType.Error);
                    CloseUnlocked();
                    return 1;
                }
            }
        }

        // Encaminha as mensagens da dgtz.dll para o Logger (uma vez só, mesmo com vários usuários).
        public static void EnsureLogHook()
        {
            lock (sync)
            {
                if (logHooked) return;
                DgtzDevice.LogMessage += OnDllLog;
                logHooked = true;
            }
        }

        public static int Close()
        {
            lock (sync) { CloseUnlocked(); }
            return 0;
        }

        private static void CloseUnlocked()
        {
            if (device != null)
            {
                try { if (device.IsRunning) device.Stop(); } catch (DigitizerException) { }
                device.Dispose();
                device = null;
            }
            block = null;
        }

        private static void OnDllLog(int level, string message)
        {
            LogType type = level >= 2 ? LogType.Error : (level == 1 ? LogType.Warning : LogType.Info);
            Logger.Instance.Log("[dgtz] " + message, type);
        }

        // Lê um evento e preenche 'waveform' com o canal pedido, em inteiros, subtraindo a baseline
        // (média das primeiras baseline_numPoints amostras). Lança DigitizerException em caso de falha.
        // Diferença em relação à DLL antiga: arredonda em vez de truncar.
        public static int GetWaveform(int[] waveform, int channel, int baseline_numPoints)
        {
            lock (sync)
            {
                if (device == null) throw new DigitizerException("Digitizer is not open.");

                int slot = device.Info.SlotOf(channel);
                if (slot < 0)
                    throw new DigitizerException("Channel " + channel + " is not enabled in the digitizer channel mask (0x" + Settings.ChannelMask.ToString("X4") + ").");

                device.Start();
                try
                {
                    int read = device.ReadEvents(block, 1, BaseTimeoutMs, CancellationToken.None);
                    if (read < 1) throw new DigitizerException(DgtzStatus.Timeout, "No trigger received from the digitizer within " + BaseTimeoutMs + " ms.");
                }
                finally
                {
                    try { device.Stop(); } catch (DigitizerException) { }
                }

                int length = Math.Min(waveform.Length, device.Info.RecordLength);
                int offset = block.OffsetOf(0, slot);
                int points = Math.Max(1, Math.Min(baseline_numPoints, device.Info.RecordLength));

                double baseline = 0;
                for (int i = 0; i < points; i++) baseline += block.Samples[offset + i];
                baseline /= points;

                for (int i = 0; i < length; i++)
                    waveform[i] = (int)Math.Round(block.Samples[offset + i] - baseline);
                return 0;
            }
        }

        // Nos scans um erro do digitizer não pode passar em silêncio: registra, pede para parar o scan
        // e devolve 0 (o ponto atual fica inválido).
        private static int SafeAmplitude(Func<int> measure)
        {
            try { return measure(); }
            catch (DigitizerException ex)
            {
                Logger.Instance.Log("Digitizer error during scan: " + ex.Message, LogType.Error);
                ScanStateManager.SetStopScan(true);
                return 0;
            }
        }

        /********************************************************************/
        /*          Funções para operar em cima da waveform obtida          */
        /********************************************************************/
        public static int GetMaxAvgFromWaveform(int[] wf, int ch, int numSamples, int binStart, int binEnd, int baseline_numPoints)
        {
            return SafeAmplitude(() =>
            {
                int avgAmplitude = 0;
                for (int i = 0; i < numSamples; i++)
                {
                    GetWaveform(wf, ch, baseline_numPoints);
                    avgAmplitude += maxInInterval(wf, binStart, binEnd);
                }
                return avgAmplitude / numSamples;
            });
        }

        public static int GetMinAvgFromWaveform(int[] wf, int ch, int numSamples, int binStart, int binEnd, int baseline_numPoints)
        {
            return SafeAmplitude(() =>
            {
                int avgAmplitude = 0;
                for (int i = 0; i < numSamples; i++)
                {
                    GetWaveform(wf, ch, baseline_numPoints);
                    avgAmplitude += minInInterval(wf, binStart, binEnd);
                }
                return avgAmplitude / numSamples;
            });
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
