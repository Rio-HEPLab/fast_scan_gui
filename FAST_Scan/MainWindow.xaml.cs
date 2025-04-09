using FAST_Scan.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FAST_Scan
{
    /// <summary>
    /// Interação lógica para MainWindow.xam
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            HommingStateManager.SetIsHomed(false);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //NÃO PERMITE FECHAR OPROGRAMA SE O SCAN ESTIVER RODANDO
            if(ScanStateManager.ScanRunning == true )
            {
                MessageBox.Show("Stop Scan before closing the program", "SCAN RUNNING", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                e.Cancel = true;
            }
        }
    }
}
