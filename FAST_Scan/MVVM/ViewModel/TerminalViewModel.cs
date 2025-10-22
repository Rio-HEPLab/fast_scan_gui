using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using FAST_Scan.Core;
using System.Windows.Input;
using static System.Net.Mime.MediaTypeNames;


namespace FAST_Scan.MVVM.ViewModel
{
    internal class TerminalViewModel
    {
        public ObservableCollection<string> Logs { get; private set; }
        public RelayCommand ClearLogsCommand { get; set; }

        public TerminalViewModel()
        {
            Logs = new ObservableCollection<string>();
            ClearLogsCommand = new RelayCommand(o => {
                Logs.Clear();
            });

            // Registra esta instância como o destino do Logger global
            Logger.Instance.SetTarget(this);
        }        

        //Adiciona mensagem de log a coleção
        //função chamada pelo Logger
        public void AddLog(LogMessage _log)
        {
            string log = $"[{_log.Type.ToString()}]\t{_log.Timestamp}\t{_log.Text}";

            // Garante que a atualização ocorra na thread da interface (UI Thread)
            App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add(log);
            });
        }
    }
}
