using System;
using System.Globalization;
using System.Windows.Data;

namespace KickAutoRecorder.App.Converters;

public class TrackingTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isTracking)
        {
            return isTracking ? "Takip Ediliyor" : "Takip Kapalı";
        }
        return "Takip Et";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
