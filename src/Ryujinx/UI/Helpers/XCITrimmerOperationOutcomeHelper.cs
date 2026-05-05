using Ryujinx.Ava.Common.Locale;
using static Ryujinx.Common.Utilities.XCIFileTrimmer;

namespace Ryujinx.Ava.UI.Helpers
{
    public static class XCIFileTrimmerOperationOutcomeExtensions
    {
        extension(OperationOutcome opOutcome)
        {
            public string LocalizedText => opOutcome switch
            {
                OperationOutcome.NoTrimNecessary => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_NoTrimNecessaryMessage],
                OperationOutcome.NoUntrimPossible => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_NoUntrimPossibleMessage],
                OperationOutcome.ReadOnlyFileCannotFix => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_ReadOnlyXCICannotFixMessage],
                OperationOutcome.FreeSpaceCheckFailed => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_FreeSpaceCheckFailedMessage],
                OperationOutcome.InvalidXCIFile => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_InvalidXCIMessage],
                OperationOutcome.FileIOWriteError => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_XCIWriteErrorMessage],
                OperationOutcome.FileSizeChanged => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_XCISizeChangedMessage],
                OperationOutcome.Cancelled => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_XCITrimCancelled],
                OperationOutcome.Undetermined => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_NoOperationPerformedMessage],
                _ => null
            };
        }
    }
}
