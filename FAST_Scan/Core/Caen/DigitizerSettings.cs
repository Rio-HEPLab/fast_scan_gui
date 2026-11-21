using System;

namespace FAST_Scan.Core.Caen
{
    public enum DrsSamplingFrequency
    {
        GSps5 = 0,       // 5 GS/s (200 ps por amostra)
        GSps2_5 = 1,
        GSps1 = 2,
        MSps750 = 3
    }

    // Configuração do digitizer DT5742. Os valores padrão vêm da DLL (dgtz_config_default),
    // que é a única fonte deles: "voltar ao padrão" = ResetToDefault().
    public class DigitizerSettings
    {
        public int LinkType { get; set; }               // 0 = USB, 1 = optical link
        public int LinkNum { get; set; }
        public int ConetNode { get; set; }

        public int RecordLength { get; set; }           // 1024, 520, 256 ou 136
        public int PostTriggerPercent { get; set; }     // 0..100
        public DrsSamplingFrequency SamplingFrequency { get; set; }
        public uint ChannelMask { get; set; }           // bit n = canal n (0..15)
        public int MaxEventsPerTransfer { get; set; }   // 1..1023

        public float[] ChannelDcOffsetPercent { get; private set; }   // 16 valores, -50..+50 %

        public bool TrTriggerEnabled { get; set; }
        public bool TrDigitize { get; set; }
        public bool TriggerFallingEdge { get; set; }
        public uint TrThreshold { get; set; }           // valor bruto do DAC
        public uint TrDcOffset { get; set; }            // valor bruto do DAC

        public DigitizerSettings()
        {
            ChannelDcOffsetPercent = new float[DgtzNative.NumChannels];
        }

        public static DigitizerSettings CreateDefault()
        {
            var settings = new DigitizerSettings();
            settings.ResetToDefault();
            return settings;
        }

        public void ResetToDefault()
        {
            DgtzNative.Config native = DgtzNative.NewConfig();
            DgtzDevice.EnsureLibrary();
            DgtzNative.dgtz_config_default(ref native);

            LinkType = native.LinkType;
            LinkNum = native.LinkNum;
            ConetNode = native.ConetNode;
            RecordLength = native.RecordLength;
            PostTriggerPercent = native.PostTriggerPercent;
            SamplingFrequency = (DrsSamplingFrequency)native.SamplingFrequency;
            ChannelMask = native.ChannelMask;
            MaxEventsPerTransfer = native.MaxEventsPerTransfer;
            Array.Copy(native.ChannelDcOffsetPercent, ChannelDcOffsetPercent, DgtzNative.NumChannels);
            TrTriggerEnabled = native.TrTriggerEnabled != 0;
            TrDigitize = native.TrDigitize != 0;
            TriggerFallingEdge = native.TriggerFallingEdge != 0;
            TrThreshold = native.TrThreshold;
            TrDcOffset = native.TrDcOffset;
        }

        internal DgtzNative.Config ToNative()
        {
            // Parte do padrão da DLL para que StructSize venha sempre correto.
            DgtzNative.Config native = DgtzNative.NewConfig();
            DgtzDevice.EnsureLibrary();
            DgtzNative.dgtz_config_default(ref native);

            native.LinkType = LinkType;
            native.LinkNum = LinkNum;
            native.ConetNode = ConetNode;
            native.RecordLength = RecordLength;
            native.PostTriggerPercent = PostTriggerPercent;
            native.SamplingFrequency = (int)SamplingFrequency;
            native.ChannelMask = ChannelMask;
            native.MaxEventsPerTransfer = MaxEventsPerTransfer;
            native.ChannelDcOffsetPercent = new float[DgtzNative.NumChannels];
            Array.Copy(ChannelDcOffsetPercent, native.ChannelDcOffsetPercent, DgtzNative.NumChannels);
            native.TrTriggerEnabled = TrTriggerEnabled ? 1 : 0;
            native.TrDigitize = TrDigitize ? 1 : 0;
            native.TriggerFallingEdge = TriggerFallingEdge ? 1 : 0;
            native.TrThreshold = TrThreshold;
            native.TrDcOffset = TrDcOffset;
            return native;
        }
    }
}
