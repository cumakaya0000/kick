using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.App.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StreamStatus status)
        {
            return status switch
            {
                StreamStatus.Live => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E")),    // Green
                StreamStatus.Offline => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")), // Gray
                StreamStatus.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444")),   // Red
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))                    // Yellow
            };
        }

        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
