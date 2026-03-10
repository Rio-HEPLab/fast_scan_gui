using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
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
using System.Windows.Threading;


namespace FAST_Scan.MVVM.View
{
    /// <summary>
    /// Interação lógica para TerminalView.xam
    /// </summary>
    public partial class TerminalView : UserControl
    {
        private ScrollViewer _scrollViewer;
        private bool _autoScroll = true;

        GraphWindow graphWindow;

        public TerminalView()
        {
            InitializeComponent();

        }

        //implementa autoscroll
        private void LogsListBox_Loaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = GetScrollViewer(LogsListBox);
            if(_scrollViewer != null )
            {
                _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }

            var vm = DataContext as ViewModel.TerminalViewModel;
            if(vm != null)
            {
                vm.Logs.CollectionChanged += (s, ev) =>
                {
                    if (_autoScroll)
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            _scrollViewer?.ScrollToEnd();
                        }, DispatcherPriority.Background);
                    }
                };
            }
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if(_scrollViewer == null)
            {
                return;
            }
            //se o scroll esta no fim
            if(_scrollViewer.VerticalOffset >= _scrollViewer.ScrollableHeight)
            {
                _autoScroll = true;
            }
            else
            {
                _autoScroll = false;
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject dep)
        {
            if(dep is ScrollViewer)
            {
                return dep as ScrollViewer;
            }

            for(int i = 0;  i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                var result = GetScrollViewer(child);
                if(result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void OpenGraphWindowClick(object sender, RoutedEventArgs e)
        {
            if (graphWindow == null || !graphWindow.IsVisible)
            {
                graphWindow = new GraphWindow();
                graphWindow.Show();
            }
            
        }
    }
}
