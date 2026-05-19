using Ryujinx.Ava.Systems.AppLibrary;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;

namespace Ryujinx.Ava.Common.Models
{
    public record XCITrimmerFileModel(
        string Name,
        string Path,
        bool Trimmable,
        bool Untrimmable,
        long PotentialSavingsB,
        long CurrentSavingsB,
        long OriginalSizeB,
        int? PercentageProgress,
        XCIFileTrimmer.OperationOutcome ProcessingOutcome)
    {
        public static XCITrimmerFileModel FromApplicationData(ApplicationData applicationData, XCIFileTrimmerLog logger)
        {
            XCIFileTrimmer trimmer = new(applicationData.Path, logger);

            return new XCITrimmerFileModel(
                applicationData.Name,
                applicationData.Path,
                trimmer.CanBeTrimmed,
                trimmer.CanBeUntrimmed,
                trimmer.DiskSpaceSavingsB,
                trimmer.DiskSpaceSavedB,
                applicationData.FileSize,
                null,
                XCIFileTrimmer.OperationOutcome.Undetermined
            );
        }

        public bool IsFailed
        {
            get
            {
                return ProcessingOutcome is not XCIFileTrimmer.OperationOutcome.Undetermined 
                    and not XCIFileTrimmer.OperationOutcome.Successful;
            }
        }

        public string StatusText
{
    get
    {
        if (IsFailed)
            return "Failed";

        return ProcessingOutcome switch
        {
            XCIFileTrimmer.OperationOutcome.Successful => 
                CurrentSavingsB > 0 ? "Trimmed" : "Untrimmed",

            XCIFileTrimmer.OperationOutcome.Undetermined =>
                Trimmable ? "Untrimmed" :
                Untrimmable ? "Trimmed" :
                "Unknown",

            _ => "Unknown"
        };
    }
}

public bool HasStatusDetail =>
    ProcessingOutcome != XCIFileTrimmer.OperationOutcome.Undetermined;



        public virtual bool Equals(XCITrimmerFileModel obj)
        {
            if (obj == null) return false;
            return this.Path == obj.Path;
        }

        public override int GetHashCode()
        {
            return this.Path.GetHashCode();
        }
    }
}