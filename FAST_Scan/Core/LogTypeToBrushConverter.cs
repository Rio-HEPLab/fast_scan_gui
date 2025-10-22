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
    internal class LogTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var type = value as LogType?;
            if (type == null)
            {
                return Brushes.White;
            }

            switch (type.Value)
            {
                case LogType.Info:
                    return Brushes.Green;
                case LogType.Warning:
                    return Brushes.Yellow;
                case LogType.Error:
                    return Brushes.Red;
                case LogType.Data:
                    return Brushes.White;
                default:
                    return Brushes.White;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
