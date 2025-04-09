using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class ScanStateManager
    {
        public static bool ScanRunning { get; private set; }

        public static event Action<bool> OnScanStateChanged;

        public static void SetScanRunning(bool isRunning)
        {
            ScanRunning = isRunning;
            OnScanStateChanged?.Invoke(isRunning);
        }
    }
}
