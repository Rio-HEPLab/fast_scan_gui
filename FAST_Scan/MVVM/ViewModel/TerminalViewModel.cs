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
        public ObservableCollection<LogMessage> Logs { get; private set; }
        public RelayCommand ClearLogsCommand { get; set; }

        public TerminalViewModel()
        {
            Logs = new ObservableCollection<LogMessage>();
            ClearLogsCommand = new RelayCommand(o => {
                Logs.Clear();
            });

            // Registra esta instância como o destino do Logger global
            Logger.Instance.SetTarget(this);
        }        

        //Adiciona mensagem de log a coleção
        //função chamada pelo Logger
        public void AddLog(LogMessage log)
        {
            // Garante que a atualização ocorra na thread da interface (UI Thread)
            App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add(log);
            });
        }
    }
}
