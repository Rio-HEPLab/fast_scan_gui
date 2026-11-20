using System;
using System.Threading;

namespace FAST_Scan.Core.Oscilloscope
{
    // Fonte de blocos de frames: o osciloscópio real (TektronixScope) ou o simulador.
    // A view de Time Resolution só conhece esta interface.
    public interface IBlockSource : IDisposable
    {
        event Action<string> Message;
        event Action<string> Trace;

        void Open();
        void Apply();
        WaveformBlock CaptureBlock(CancellationToken token);
    }
}
