using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;
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
                return BindingOperations.DoNothing;

            if (value is not XCITrimmerFileModel app)
                return default(Symbol); // no icon
            
            bool isProcessing = app.PercentageProgress != null;
            
            if (isProcessing)
            {
                if (app.ProcessingOutcome is not OperationOutcome.Successful
                    and not OperationOutcome.Undetermined)
                {
                    return Symbol.Important;
                }

                return Symbol.Sync;
            }

            if (app.ProcessingOutcome is not OperationOutcome.Successful
                and not OperationOutcome.Undetermined)
                return Symbol.Important;

            if (app.Trimmable && app.Untrimmable)
                return Symbol.Repair;

            if (app.Trimmable)
                return Symbol.Cut;

            if (app.Untrimmable)
                return Symbol.Checkmark;

            return default(Symbol);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
