using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    enum LogType { Info , Warning, Error }
    internal class LogMessage
    {
        public string Text { get; set; }
        public LogType Type { get; set; }

        public string Timestamp = $"[{System.DateTime.Now:HH:mm:ss}] ";

        public LogMessage(String text, LogType type)
        {
            Text = text;
            Type = type;

        }
    }
}