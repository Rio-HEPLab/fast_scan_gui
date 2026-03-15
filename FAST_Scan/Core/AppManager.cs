using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    enum AppState
    {
        NONE,
        SCAN_1D,
        SCAN_2D,
        FOCAL_ANALYSIS,
        WAVEFORM_VIEWER,
        CONFIGURATIONS
    }
    internal class AppManager
    {
        public static AppState State { get; private set; }
        public static event Action<AppState> OnAppStateChanged;

        public static void SetState(AppState state)
        {
            State = state;
            OnAppStateChanged?.Invoke(state);
        }


    }
}
