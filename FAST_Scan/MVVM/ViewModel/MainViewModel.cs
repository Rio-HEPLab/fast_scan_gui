using FAST_Scan.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FAST_Scan.MVVM.ViewModel
{
    internal class MainViewModel : ObservebleObject
    {
        public RelayCommand Scan1DCommand { get; set; }
        public RelayCommand Scan2DCommand { get; set; }


        public Scan2DViewModel Scan2DVM { get; set; }
        public Scan1DViewModel Scan1DVM { get; set; }


        private object _currentView;

        public object CurrentView
        {
            get { return _currentView; }
            set 
            { 
                _currentView = value;
                OnPropertyChanged();  
            }
        }

        public MainViewModel() 
        {
            Scan2DVM = new Scan2DViewModel();
            Scan1DVM = new Scan1DViewModel();

            //CurrentView = Scan2DVM;

            Scan1DCommand = new RelayCommand(o =>
            {
                if(ScanStateManager.ScanRunning == true)
                {
                    MessageBox.Show("You cannot change views while scan is running.", "SCAN RUNNING", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
                else
                {
                    CurrentView = Scan1DVM;
                }
            });

            Scan2DCommand = new RelayCommand(o =>
            {
                if (ScanStateManager.ScanRunning == true)
                {
                    MessageBox.Show("You cannot change views while scan is running.","SCAN RUNNING", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
                else
                {
                    CurrentView = Scan2DVM;
                }
            });
        }
    }
}
