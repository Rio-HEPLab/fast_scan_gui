using FAST_Scan.MVVM.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class Logger
    {
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        public static Logger Instance => _instance.Value;

        private TerminalViewModel _target;

        private Logger() { }

        public void SetTarget(TerminalViewModel terminal)
        {
            _target = terminal;
        }

        public void Log(string text, LogType type = LogType.Info)
        {
            var message = new LogMessage(text, type);
            string timestamp = $"[{System.DateTime.Now:HH:mm:ss}] ";
            Console.WriteLine($"[{type}]\t{timestamp}\t{text}");
            if(_target != null)
            {
                _target.AddLog(message);
            }
        }
    }
}
