using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace FAST_Scan.Core
{
    enum LogType { Info, Warning, Error, Data }
    internal class LogMessage
    {
        public string Text { get; set; }
        public LogType Type { get; set; }

        public string Timestamp = $"[{System.DateTime.Now:HH:mm:ss}] ";
        public string Log { get; private set; }


        public LogMessage(String text, LogType type)
        {
            Text = text;
            Type = type;

            //Log = $"[{Type.ToString()}]\t{Timestamp}\t{Text}";
            if(type != LogType.Data)
            {
                Log = $"[{Type.ToString()}]{Timestamp}{Text}";
            }
            else
            {
                Log = $"{Timestamp}{Text}";
            }


        }
    }

}