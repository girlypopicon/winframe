using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WinFrame.Models;

namespace WinFrame.Converters;

[ValueConversion(typeof(MessageRole), typeof(HorizontalAlignment))]
public class MessageAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isUser)
            return isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        if (value is MessageRole role)
            return role == MessageRole.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
