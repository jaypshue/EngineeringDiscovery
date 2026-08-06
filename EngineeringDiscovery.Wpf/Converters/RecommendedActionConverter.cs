using System;
using System.Globalization;
using System.Windows.Data;

namespace EngineeringDiscovery.Wpf.Converters
{
    public class RecommendedActionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string ?? string.Empty;
            return status switch
            {
                ViewModels.EngineeringPackageViewModel.StatusNeedsReview => "Regenerate Package",
                ViewModels.EngineeringPackageViewModel.StatusReadyForReview => "Review Package",
                ViewModels.EngineeringPackageViewModel.StatusReadyForImplementation => "Send to Copilot",
                ViewModels.EngineeringPackageViewModel.StatusDraft => "Generate Package",
                ViewModels.EngineeringPackageViewModel.StatusCollecting => "Collect Context",
                _ => "Generate Package",
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
