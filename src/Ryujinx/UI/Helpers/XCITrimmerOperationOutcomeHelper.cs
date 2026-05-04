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
                OperationOutcome.NoTrimNecessary => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_TrimXCIFileNoTrimNecessary],
                OperationOutcome.NoUntrimPossible => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_TrimXCIFileNoUntrimPossible],
                OperationOutcome.ReadOnlyFileCannotFix => LocaleManager.Instance[
                    LocaleKeys.Dialog_XCITrimmer_TrimXCIFileReadOnlyFileCannotFix],
                OperationOutcome.FreeSpaceCheckFailed => LocaleManager.Instance[
                    LocaleKeys.Dialog_XCITrimmer_TrimXCIFileFreeSpaceCheckFailed],
                OperationOutcome.InvalidXCIFile => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_TrimXCIFileInvalidXCIFile],
                OperationOutcome.FileIOWriteError => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_TrimXCIFileFileIOWriteError],
                OperationOutcome.FileSizeChanged => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_TrimXCIFileFileSizeChanged],
                OperationOutcome.Cancelled => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_TrimXCIFileCancelled],
                OperationOutcome.Undetermined => LocaleManager.Instance[LocaleKeys.Dialog_XCITrimmer_TrimXCIFileFileUndertermined],
                _ => null
            };
        }
    }
}
