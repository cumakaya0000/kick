using System;
using System.Globalization;
using System.Windows.Data;
using KickAutoRecorder.Core.Enums;

namespace KickAutoRecorder.App.Converters;

public class StatusToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StreamStatus status)
        {
            return status switch
            {
                StreamStatus.Live => "CANLI YAYINDA",
                StreamStatus.Offline => "OFFLINE",
                StreamStatus.Error => "HATA",
                _ => "BEKLENİYOR"
            };
        }

        return "BİLİNMİYOR";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
