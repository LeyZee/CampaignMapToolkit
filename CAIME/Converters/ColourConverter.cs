using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CAIME.Converters
{
    class ColourConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(Brush))
            {
                throw new InvalidOperationException("Target type must be System.Windows.Media.Brush");
            }

            int colour = (int)value;
            try
            {
                byte[] bytes = BitConverter.GetBytes(colour);
                return new SolidColorBrush(Color.FromArgb(bytes[3], bytes[0], bytes[1], bytes[2]));
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
