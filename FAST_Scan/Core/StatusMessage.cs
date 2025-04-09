using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.Core
{
    internal class StatusMessage : INotifyPropertyChanged
    {
        private readonly StringBuilder _messages = new StringBuilder();

        private string _log;
        public string Log
        {
            get => _log;
            private set
            {
                _log = value;
                OnPropertyChanged();
            }
        }

        public void CreateStatusMessage(string message)
        {
            string timestamp = $"[{System.DateTime.Now:HH:mm:ss}] ";
            _messages.AppendLine(timestamp + message);
            Log = _messages.ToString(); // Atualiza o binding
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
