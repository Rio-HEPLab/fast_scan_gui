using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FAST_Scan.Core.Caen
{
    public class DigitizerException : Exception
    {
        public DgtzStatus Status { get; private set; }

        public DigitizerException(string message) : base(message) { Status = DgtzStatus.Ok; }

        public DigitizerException(DgtzStatus status, string message) : base(message) { Status = status; }

        public DigitizerException(string message, Exception inner) : base(message, inner) { Status = DgtzStatus.Ok; }
    }

    public class DigitizerInfo
    {
        public const int TrSlotBase = 16;       // slot_id = 16 + g -> sinal TR do grupo g

        public string Model { get; internal set; }
        public int NumChannels { get; internal set; }
        public int NumBits { get; internal set; }
        public double SampleRateHz { get; internal set; }
        public int RecordLength { get; internal set; }
        public int SlotCount { get; internal set; }
        public int[] SlotIds { get; internal set; }     // 0..15 = canal, 16 + g = TR do grupo g

        public double SamplePeriodNs { get { return 1e9 / SampleRateHz; } }

        // Índice do slot de um canal (ou do TR, com TrSlotBase + grupo). -1 se não está no buffer.
        public int SlotOf(int slotId)
        {
            for (int i = 0; i < SlotCount; i++)
                if (SlotIds[i] == slotId) return i;
            return -1;
        }

        // Slot do TR que serve de referência de tempo para o canal (o do mesmo grupo).
        public int TrSlotOfChannel(int channel)
        {
            return SlotOf(TrSlotBase + channel / 8);
        }
    }

    // Metadados de um evento. O layout é idêntico ao DgtzEventMeta de dgtz.h.
    [StructLayout(LayoutKind.Sequential)]
    public struct DigitizerEventMeta
    {
        public uint EventCounter;
        public uint TriggerTimeTag0;     // grupo 0 (ticks do relógio da placa)
        public uint TriggerTimeTag1;     // grupo 1
        public ushort StartIndexCell0;
        public ushort StartIndexCell1;
    }

    // Buffer reutilizável para vários eventos: Samples[evento][slot][amostra].
    public sealed class DigitizerEventBlock
    {
        public DigitizerInfo Info { get; private set; }
        public int Capacity { get; private set; }
        public float[] Samples { get; private set; }
        public DigitizerEventMeta[] Meta { get; private set; }
        public int Count { get; internal set; }

        public DigitizerEventBlock(DigitizerInfo info, int capacity)
        {
            if (info == null) throw new ArgumentNullException("info");
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            Info = info;
            Capacity = capacity;
            Samples = new float[(long)capacity * info.SlotCount * info.RecordLength];
            Meta = new DigitizerEventMeta[capacity];
        }

        public int OffsetOf(int eventIndex, int slotIndex)
        {
            return (eventIndex * Info.SlotCount + slotIndex) * Info.RecordLength;
        }

        public void CopyWaveform(int eventIndex, int slotIndex, float[] destination)
        {
            Array.Copy(Samples, OffsetOf(eventIndex, slotIndex), destination, 0, Math.Min(destination.Length, Info.RecordLength));
        }
    }

    // Digitizer aberto. Não é thread-safe: use sempre a partir de uma thread por vez.
    public sealed class DgtzDevice : IDisposable
    {
        private const int ChunkMs = 200;   // fatia de espera, para o cancelamento reagir rápido

        private static DgtzNative.LogCallback logCallback;   // referência estática: impede o GC de coletar o delegate
        private static bool libraryChecked;

        private IntPtr handle;

        public DigitizerInfo Info { get; private set; }
        public bool IsRunning { get; private set; }

        // Mensagens de log da DLL: (nível 0 = info, 1 = aviso, 2 = erro, texto).
        public static event Action<int, string> LogMessage;

        private DgtzDevice(IntPtr handle, DigitizerInfo info)
        {
            this.handle = handle;
            Info = info;
        }

        // Garante que a dgtz.dll carrega e instala o callback de log (uma vez).
        internal static void EnsureLibrary()
        {
            if (libraryChecked) return;
            try
            {
                logCallback = OnNativeLog;
                DgtzNative.dgtz_set_log_callback(logCallback, IntPtr.Zero);
                libraryChecked = true;
            }
            catch (DllNotFoundException ex)
            {
                throw new DigitizerException("dgtz.dll (or a CAEN library it depends on) was not found. Copy dgtz.dll next to the executable and install the CAEN Digitizer library (x64).", ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new DigitizerException("dgtz.dll has the wrong architecture: the application must run as x64.", ex);
            }
        }

        private static void OnNativeLog(int level, string message, IntPtr user)
        {
            Action<int, string> handler = LogMessage;
            if (handler != null) handler(level, message);
        }

        private static string StatusText(int status)
        {
            return Marshal.PtrToStringAnsi(DgtzNative.dgtz_status_text(status));
        }

        private static string LastError(IntPtr dev)
        {
            return Marshal.PtrToStringAnsi(DgtzNative.dgtz_last_error(dev));
        }

        private void Check(int status, string action)
        {
            if (status == (int)DgtzStatus.Ok) return;
            string detail = LastError(handle);
            throw new DigitizerException((DgtzStatus)status, action + ": " + (string.IsNullOrEmpty(detail) ? StatusText(status) : detail));
        }

        public static DgtzDevice Open(DigitizerSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            EnsureLibrary();

            DgtzNative.Config config = settings.ToNative();
            IntPtr dev;
            int status = DgtzNative.dgtz_open(ref config, out dev);
            if (status != (int)DgtzStatus.Ok)
                {
                string detail = LastError(IntPtr.Zero);
                throw new DigitizerException((DgtzStatus)status, string.IsNullOrEmpty(detail) ? StatusText(status) : detail);
            }

            DgtzNative.Info native;
            status = DgtzNative.dgtz_get_info(dev, out native);
            if (status != (int)DgtzStatus.Ok)
            {
                DgtzNative.dgtz_close(dev);
                throw new DigitizerException((DgtzStatus)status, "Cannot read the digitizer information: " + StatusText(status));
            }

            var info = new DigitizerInfo
            {
                Model = native.Model,
                NumChannels = native.NumChannels,
                NumBits = native.NumBits,
                SampleRateHz = native.SampleRateHz,
                RecordLength = native.RecordLength,
                SlotCount = native.SlotCount,
                SlotIds = new int[native.SlotCount]
            };
            Array.Copy(native.SlotId, info.SlotIds, native.SlotCount);
            return new DgtzDevice(dev, info);
        }

        public DigitizerEventBlock CreateBlock(int capacity)
        {
            return new DigitizerEventBlock(Info, capacity);
        }

        public void Start()
        {
            ThrowIfDisposed();
            Check(DgtzNative.dgtz_start(handle), "Start acquisition");
            IsRunning = true;
        }

        public void Stop()
        {
            ThrowIfDisposed();
            int status = DgtzNative.dgtz_stop(handle);
            IsRunning = false;
            Check(status, "Stop acquisition");
        }

        // Lê até maxEvents eventos para 'block'. Devolve quantos chegaram (pode ser menos, ou 0,
        // se o tempo acabar). Lança OperationCanceledException se o token for cancelado.
        public int ReadEvents(DigitizerEventBlock block, int maxEvents, int timeoutMs, CancellationToken token)
        {
            ThrowIfDisposed();
            if (block == null) throw new ArgumentNullException("block");
            if (maxEvents < 1 || maxEvents > block.Capacity) throw new ArgumentOutOfRangeException("maxEvents");

            int floatsPerEvent = Info.SlotCount * Info.RecordLength;
            int metaSize = Marshal.SizeOf(typeof(DigitizerEventMeta));
            int count = 0;
            var clock = Stopwatch.StartNew();

            GCHandle samplesPin = GCHandle.Alloc(block.Samples, GCHandleType.Pinned);
            GCHandle metaPin = GCHandle.Alloc(block.Meta, GCHandleType.Pinned);
            try
            {
                long samplesBase = samplesPin.AddrOfPinnedObject().ToInt64();
                long metaBase = metaPin.AddrOfPinnedObject().ToInt64();

                while (count < maxEvents)
                {
                    token.ThrowIfCancellationRequested();

                    long remainingMs = timeoutMs - clock.ElapsedMilliseconds;
                    int chunk = (int)Math.Max(0, Math.Min(ChunkMs, remainingMs));

                    int read;
                    int status = DgtzNative.dgtz_read_events(
                        handle, maxEvents - count, chunk,
                        new IntPtr(samplesBase + (long)count * floatsPerEvent * sizeof(float)),
                        (maxEvents - count) * floatsPerEvent,
                        new IntPtr(metaBase + (long)count * metaSize),
                        out read);

                    if (status == (int)DgtzStatus.Ok) count += read;
                    else if (status != (int)DgtzStatus.Timeout) Check(status, "Read events");

                    if (clock.ElapsedMilliseconds >= timeoutMs) break;
                }
            }
            finally
            {
                samplesPin.Free();
                metaPin.Free();
            }

            block.Count = count;
            return count;
        }

        private void ThrowIfDisposed()
        {
            if (handle == IntPtr.Zero) throw new ObjectDisposedException("DgtzDevice");
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            DgtzNative.dgtz_close(handle);
            handle = IntPtr.Zero;
            IsRunning = false;
        }
    }
}
