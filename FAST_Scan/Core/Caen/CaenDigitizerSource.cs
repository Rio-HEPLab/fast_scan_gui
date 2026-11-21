using System;
using System.Diagnostics;
using System.Threading;
using FAST_Scan.Core.Oscilloscope;

namespace FAST_Scan.Core.Caen
{
    // Fonte de blocos baseada no digitizer DT5742 (alternativa ao osciloscópio).
    // Cada bloco reúne vários eventos de um canal e, quando disponível, o TR do MESMO grupo
    // (referência de tempo do trigger digitalizado).
    public class CaenDigitizerSource : IBlockSource
    {
        // Escala nominal: o ADC tem 12 bits sobre 1 Vpp. Não é uma calibração: serve para
        // que limiares e eixos fiquem em mV de forma coerente.
        private const double NominalVoltsPerCode = 1.0 / 4096.0;
        private const double MidCode = 2048.0;
        private const int ReadTimeoutMs = 5000;

        private readonly DigitizerSettings settings;
        private readonly int channel;
        private readonly int eventsPerBlock;

        private DgtzDevice device;
        private DigitizerEventBlock buffer;
        private int signalSlot = -1;
        private int triggerSlot = -1;

        public event Action<string> Message;
        public event Action<string> Trace;

        public CaenDigitizerSource(DigitizerSettings settings, int channel, int eventsPerBlock)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            if (channel < 0 || channel >= DgtzNative.NumChannels) throw new ArgumentOutOfRangeException("channel");
            if (eventsPerBlock < 1) throw new ArgumentOutOfRangeException("eventsPerBlock");
            this.settings = settings;
            this.channel = channel;
            this.eventsPerBlock = eventsPerBlock;
        }

        private void Say(string text) { Action<string> h = Message; if (h != null) h(text); }
        private void Tr(string text) { Action<string> h = Trace; if (h != null) h(text); }

        public void Open()
        {
            // Só o canal medido (e o TR do grupo dele): buffer menor e leitura mais rápida.
            settings.ChannelMask = 1u << channel;
            settings.TrDigitize = true;
            settings.TrTriggerEnabled = true;
            settings.MaxEventsPerTransfer = Math.Max(1, Math.Min(1023, eventsPerBlock));

            Digitizer.EnsureLogHook();   // mensagens da dgtz.dll vão para o log do programa
            device = DgtzDevice.Open(settings);
            DigitizerInfo info = device.Info;
            signalSlot = info.SlotOf(channel);
            triggerSlot = info.TrSlotOfChannel(channel);
            if (signalSlot < 0)
                throw new DigitizerException("Channel " + channel + " is not in the digitizer buffer.");

            buffer = device.CreateBlock(eventsPerBlock);
            Say("Digitizer " + info.Model + ": " + info.RecordLength + " samples per event at " +
                (info.SampleRateHz / 1e9).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " GS/s, channel " + channel +
                (triggerSlot >= 0 ? " + TR of group " + (channel / 8) : " (no TR in the data)") + ".");
        }

        // Aqui "aplicar" é iniciar a aquisição contínua (a placa já foi programada no Open).
        public void Apply()
        {
            if (device == null) throw new InvalidOperationException("Call Open() first.");
            device.Start();
            Tr("Digitizer acquisition started.");
        }

        public WaveformBlock CaptureBlock(CancellationToken token)
        {
            if (device == null) throw new InvalidOperationException("Call Open() first.");

            var clock = Stopwatch.StartNew();
            int count = device.ReadEvents(buffer, eventsPerBlock, ReadTimeoutMs, token);
            double captureMs = clock.Elapsed.TotalMilliseconds;
            if (count < 1)
                throw new DigitizerException(DgtzStatus.Timeout, "No trigger received from the digitizer within " + ReadTimeoutMs + " ms.");

            DigitizerInfo info = device.Info;
            int n = info.RecordLength;
            bool hasTrigger = triggerSlot >= 0;

            var block = new WaveformBlock
            {
                FrameCount = count,
                PointsPerFrame = n,
                Samples = new float[(long)count * n],
                YMultiplier = NominalVoltsPerCode,
                YOffset = MidCode,
                YZero = 0,
                SaturationHighCode = 4094,
                SaturationLowCode = 1,
                XIncrementSeconds = 1.0 / info.SampleRateHz,
                // Posição do trigger na janela: amostras antes do trigger = (100 - pós-trigger)%.
                TriggerIndex = (100 - settings.PostTriggerPercent) / 100.0 * n,
                XZeroSeconds = null   // o DRS4 não tem o conceito de XZero do osciloscópio
            };
            if (hasTrigger)
            {
                block.TriggerSamples = new float[(long)count * n];
                block.TriggerYMultiplier = NominalVoltsPerCode;
                block.TriggerYOffset = MidCode;
                block.TriggerYZero = 0;
            }

            for (int e = 0; e < count; e++)
            {
                Array.Copy(buffer.Samples, buffer.OffsetOf(e, signalSlot), block.Samples, (long)e * n, n);
                if (hasTrigger)
                    Array.Copy(buffer.Samples, buffer.OffsetOf(e, triggerSlot), block.TriggerSamples, (long)e * n, n);
            }

            block.CaptureMs = captureMs;
            block.TransferMs = clock.Elapsed.TotalMilliseconds;
            Tr("Block: " + count + " events in " + captureMs.ToString("0") + " ms.");
            return block;
        }

        public void Dispose()
        {
            if (device == null) return;
            try { if (device.IsRunning) device.Stop(); } catch (DigitizerException) { }
            device.Dispose();
            device = null;
        }
    }
}
