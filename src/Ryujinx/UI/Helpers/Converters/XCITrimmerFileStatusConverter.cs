using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Common.Models;
using System;
using System.Globalization;
using static Ryujinx.Common.Utilities.XCIFileTrimmer;

namespace Ryujinx.Ava.UI.Helpers
{
    internal class XCITrimmerFileStatusConverter : IValueConverter
    {
        public static XCITrimmerFileStatusConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UnsetValueType)
            {
                return BindingOperations.DoNothing;
            }

            if (!targetType.IsAssignableFrom(typeof(string)))
            {
                return null;
            }

            if (value is not XCITrimmerFileModel app)
            {
                return null;
            }

            return app.PercentageProgress != null ? String.Empty :
                app.ProcessingOutcome is not OperationOutcome.Successful and not OperationOutcome.Undetermined ? LocaleManager.Instance[LocaleKeys.XCITrimmer_FailedLabel] :
                app.Trimmable & app.Untrimmable ? LocaleManager.Instance[LocaleKeys.XCITrimmer_PartialLabel] :
                app.Trimmable ? LocaleManager.Instance[LocaleKeys.XCITrimmer_UntrimmedLabel] :
                app.Untrimmable ? LocaleManager.Instance[LocaleKeys.XCITrimmer_TrimmedLabel] :
                String.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
