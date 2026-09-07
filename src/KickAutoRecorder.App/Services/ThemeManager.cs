using System.Windows;
using System.Windows.Media;

namespace KickAutoRecorder.App.Services;

public static class ThemeManager
{
    public static void ApplyTheme(bool isDark)
    {
        var res = Application.Current.Resources;

        if (isDark)
        {
            res["WindowBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            res["CardBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
            res["CardBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            res["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            res["InputBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            res["ComboBoxItemBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
            res["ComboBoxItemForegroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
        }
        else
        {
            res["WindowBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9"));
            res["CardBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["CardBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            res["TextPrimaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
            res["TextSecondaryBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569"));
            res["TextMutedBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
            res["InputBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));
            res["ComboBoxItemBackgroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            res["ComboBoxItemForegroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
        }
    }
}
