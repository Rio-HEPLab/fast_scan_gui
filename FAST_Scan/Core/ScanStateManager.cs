using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class ScanStateManager
    {
        //Estado do Scan
        public static bool ScanRunning { get; private set; }

        public static event Action<bool> OnScanStateChanged;

        public static void SetScanRunning(bool isRunning)
        {
            ScanRunning = isRunning;
            OnScanStateChanged?.Invoke(isRunning);
        }

        //Forçar parada do scan solicitado
        public static bool StopScan {  get; private set; }
        
        public static event Action<bool> StopScanStateChanged;

        public static void SetStopScan(bool stop)
        {
            StopScan = stop;
            StopScanStateChanged?.Invoke(stop);
        }
    }
}
