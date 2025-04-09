using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FAST_Scan.Core
{
    internal class ObservebleObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(name));
        }
    }
}
