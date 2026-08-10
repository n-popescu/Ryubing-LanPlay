using Avalonia.Platform.Storage;
using Gommon;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Utilities;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class Fat32MergeViewModel : BaseModel
    {
        public List<string> SplitPaths = new List<string>();

        public string Dir;

        public string FileName;
        
        
        public string MergedName
        {
            get
            {
                return $"{LocaleKeys.Fat32Merge_FileNamePrefix}{Dir}";
            }
        }

        public async void OpenFolderPicker()
        {
            try
            {
                List<string> unorderedSplitPaths;
                
                Optional<IStorageFolder> folder = await RyujinxApp.MainWindow.ViewModel.StorageProvider.OpenSingleFolderPickerAsync();
                Dir = folder.Value.Path.LocalPath; 
                unorderedSplitPaths = Directory.EnumerateFiles(Dir, "*").ToList();
                FileName = Path.GetDirectoryName(Dir);
                FileName = FileName.Remove(0, FileName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                

                if (unorderedSplitPaths.Contains($"{Dir}{FileName}"))
                {
                    unorderedSplitPaths.Remove($"{Dir}{FileName}");
                }

                SplitPaths.Clear();
                for (int fileindex = 0; fileindex < unorderedSplitPaths.Count; fileindex++)
                {
                    SplitPaths.Add($"{Dir}0{fileindex}");
                }
            }
            catch (Exception exception)
            {
                Logger.Error?.Print(LogClass.Application, $"{LocaleKeys.Fat32Merge_MergeEndFailed}");
                Logger.Error?.Print(LogClass.Application, exception.ToString());
            }
        }

        public void MergeDump()
        {
            
            try
            {
                if (File.Exists($"{Dir}{FileName}"))
                {
                    File.Delete($"{Dir}{FileName}");
                    Logger.Notice.Print(LogClass.Application, $"{LocaleKeys.Fat32Merge_RemoveExistingFile}");
                }
                Logger.Notice.Print(LogClass.Application, $"{LocaleKeys.Fat32Merge_MergeBegin}");
                using (FileStream output = File.Create($"{Dir}{FileName}"))
                {
                    foreach (string file in SplitPaths)
                    {
                        using (FileStream input = File.OpenRead(file))
                        {
                            input.CopyTo(output);
                        }
                    }
                }
                Logger.Notice.Print(LogClass.Application, $"{LocaleKeys.Fat32Merge_MergeEnd}");
            }
            catch (Exception e)
            {
                Logger.Error?.Print(LogClass.Application, $"{LocaleKeys.Fat32Merge_MergeEndFailed}");
                Logger.Error?.Print(LogClass.Application, e.ToString());
            }
        }
    }
}
