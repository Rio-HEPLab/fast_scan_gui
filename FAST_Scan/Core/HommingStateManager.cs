using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class HommingStateManager
    {
        public static bool ScanHomed { get; private set; }

        public static event Action<bool> OnScanHomedChanged;

        public static void SetIsHomed(bool isHomed)
        {
            ScanHomed = isHomed;
            OnScanHomedChanged?.Invoke(isHomed);
        }
    }
}
