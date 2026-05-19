using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Ryujinx.Ava.Common.Models;
using System;
using System.Globalization;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class XCITrimmerFileItalicsConverter : IValueConverter
    {
        public static readonly XCITrimmerFileItalicsConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == AvaloniaProperty.UnsetValue)
                return FontStyle.Normal;

            if (value is not XCITrimmerFileModel app)
                return FontStyle.Normal;

            // Untrimmed files → Italic
            if (app.Trimmable && !app.Untrimmable)
                return FontStyle.Italic;

            return FontStyle.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}