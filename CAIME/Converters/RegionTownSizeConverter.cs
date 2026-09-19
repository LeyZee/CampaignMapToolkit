using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CAIME.Converters
{
    class RegionTownSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(int))
            {
                throw new InvalidOperationException("CAIME.Converters.RegionTownSizeConverter.Convert(): Target type must be int.");
            }

            try
            {
                var isMajorRegion = (bool)value;
                int index = isMajorRegion ? 1 : 0;
                return index;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool))
            {
                throw new InvalidOperationException("CAIME.Converters.RegionTownSizeConverter.ConvertBack(): Target type must be bool.");
            }

            try
            {
                var isMajorRegion = (int)value == 1;
                return isMajorRegion;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}
