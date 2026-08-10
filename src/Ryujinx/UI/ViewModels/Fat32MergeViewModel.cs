using Avalonia.Controls;
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

        public string FileName = "None Selected";
        
        
        public string MergedName
        {
            get
            {
                return $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_FileNamePrefix]}{FileName}";
            }
        }

        public async void OpenFolderPicker()
        {
            
            try
            {
                Optional<IStorageFolder> folder = await RyujinxApp.MainWindow.ViewModel.StorageProvider.OpenSingleFolderPickerAsync();
                Dir = folder.Value.Path.LocalPath; 
                SplitPaths = Directory.EnumerateFiles(Dir, "*").ToList();
                FileName = Path.GetDirectoryName(Dir);
                FileName = FileName.Remove(0, FileName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                SplitPaths.Sort();
                if (SplitPaths.Contains($"{Dir}{FileName}"))
                {
                    SplitPaths.Remove($"{Dir}{FileName}");
                }
            }
            catch (Exception exception)
            {
                Logger.Error?.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeEndFailed]}");
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
                    Logger.Notice.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_RemoveExistingFile]}");
                }
                Logger.Notice.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeBegin]}");
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
                Logger.Notice.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeEnd]}");
            }
            catch (Exception e)
            {
                Logger.Error?.Print(LogClass.Application, $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_MergeEndFailed]}");
                Logger.Error?.Print(LogClass.Application, e.ToString());
            }
        }
    }
}
