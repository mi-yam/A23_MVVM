// LeftMarginConverter.cs (新しいファイル)
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace A23_MVVM
{
  // LeftMarginConverter.cs (新しいファイル)
  public class LeftMarginConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
      if (value is double left)
      {
        return new Thickness(left, 0, 0, 0);
      }
      return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
  }
}