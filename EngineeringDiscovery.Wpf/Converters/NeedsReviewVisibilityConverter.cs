using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EngineeringDiscovery.Wpf.Converters
{
    public class NeedsReviewVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string ?? string.Empty;
            return status == ViewModels.EngineeringPackageViewModel.StatusNeedsReview ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
