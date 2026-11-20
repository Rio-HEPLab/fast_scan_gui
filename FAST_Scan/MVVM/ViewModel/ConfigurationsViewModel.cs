using FAST_Scan.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FAST_Scan.MVVM.ViewModel
{
    internal class ConfigurationsViewModel
    {
        RelayCommand SaveCommand { get; set; }
        RelayCommand CancelCommand { get; set; }

        public ConfigurationsViewModel()
        {
            SaveCommand = new RelayCommand(o =>
            {
                //Salvar alterações no json e fechar a jjanela
            });
        }
    }
}
