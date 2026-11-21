using System;
using System.Runtime.InteropServices;

namespace FAST_Scan.Core.Caen
{
    // Códigos de retorno da biblioteca nativa (espelha DgtzStatus em dgtz.h).
    public enum DgtzStatus
    {
        Ok = 0,
        InvalidArg,
        Open,
        BoardInfo,
        Unsupported,
        Program,
        Memory,
        NotRunning,
        Timeout,
        Readout,
        EventDecode,
        BufferTooSmall,
        OverTemp,
        BoardFailure
    }

    // Declarações P/Invoke da dgtz.dll. Os layouts das structs espelham dgtz.h (Sequential, sem padding).
    internal static class DgtzNative
    {
        private const string Dll = "dgtz.dll";

        public const int NumChannels = 16;
        public const int NumGroups = 2;
        public const int MaxSlots = 18;

        [StructLayout(LayoutKind.Sequential)]
        public struct Config
        {
            public int StructSize;
            public int LinkType;
            public int LinkNum;
            public int ConetNode;
            public int RecordLength;
            public int PostTriggerPercent;
            public int SamplingFrequency;
            public uint ChannelMask;
            public int MaxEventsPerTransfer;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NumChannels)]
            public float[] ChannelDcOffsetPercent;
            public int TrTriggerEnabled;
            public int TrDigitize;
            public int TriggerFallingEdge;
            public uint TrThreshold;
            public uint TrDcOffset;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct Info
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string Model;
            public int NumChannels;
            public int NumBits;
            public double SampleRateHz;
            public int RecordLength;
            public int SlotCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxSlots)]
            public int[] SlotId;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback(int level, [MarshalAs(UnmanagedType.LPStr)] string message, IntPtr user);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void dgtz_config_default(ref Config cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dgtz_open(ref Config cfg, out IntPtr dev);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dgtz_get_info(IntPtr dev, out Info info);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dgtz_start(IntPtr dev);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dgtz_stop(IntPtr dev);

        // samples e meta são ponteiros para memória fixada (GCHandle) pelo chamador.
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int dgtz_read_events(IntPtr dev, int maxEvents, int timeoutMs,
                                                  IntPtr samples, int samplesCapacity,
                                                  IntPtr meta, out int eventsRead);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void dgtz_close(IntPtr dev);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr dgtz_status_text(int status);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr dgtz_last_error(IntPtr dev);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void dgtz_set_log_callback(LogCallback fn, IntPtr user);

        public static Config NewConfig()
        {
            return new Config { ChannelDcOffsetPercent = new float[NumChannels] };
        }
    }
}
