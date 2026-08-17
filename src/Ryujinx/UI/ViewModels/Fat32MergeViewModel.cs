using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Gommon;
using LibHac.Common.Keys;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Systems.AppLibrary;
using Ryujinx.Ava.UI.Windows;
using Ryujinx.Ava.Utilities;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
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

        public string keydir = AppDataManager.KeysDirPath;

        private KeySet _KeySet = KeySet.CreateDefaultKeySet(); // Check VirtualFileSystem.ReloadKeySet() perhaps
        
        public string MergedName
        {
            get
            {
                return $"{LocaleManager.Instance[LocaleKeys.Fat32Merge_FileNamePrefix]}{FileName}";
            }
        }

        private void GetKeySet()
        {
            string prodKeyFile = null;
            string titleKeyFile = null;
            string consoleKeyFile = null;
            string devKeyFile = null;
            
            if (AppDataManager.Mode == AppDataManager.LaunchMode.UserProfile)
            {
                LoadSetAtPath(AppDataManager.KeysDirPathUser);
            }

            LoadSetAtPath(AppDataManager.KeysDirPath);
            
            void LoadSetAtPath(string basePath)
            {
                string localProdKeyFile = Path.Combine(basePath, "prod.keys");
                string localTitleKeyFile = Path.Combine(basePath, "title.keys");
                string localConsoleKeyFile = Path.Combine(basePath, "console.keys");
                string localDevKeyFile = Path.Combine(basePath, "dev.keys");

                if (File.Exists(localProdKeyFile))
                {
                    prodKeyFile = localProdKeyFile;
                }

                if (File.Exists(localTitleKeyFile))
                {
                    titleKeyFile = localTitleKeyFile;
                }

                if (File.Exists(localConsoleKeyFile))
                {
                    consoleKeyFile = localConsoleKeyFile;
                }

                if (File.Exists(localDevKeyFile))
                {
                    devKeyFile = localDevKeyFile;
                }
            }
            
            ExternalKeyReader.ReadKeyFile(_KeySet, prodKeyFile, devKeyFile, titleKeyFile, consoleKeyFile, null);
        }

        public async void OpenFolderPicker()
        {
            try
            {
                Optional<IStorageFolder> folder = await RyujinxApp.MainWindow.ViewModel.StorageProvider.OpenSingleFolderPickerAsync();
                Dir = folder.Value.Path.LocalPath; 
                SplitPaths = Directory.EnumerateFiles(Dir, "*").ToList();
                SplitPaths.Sort();

                
                bool IsXci = true;
                // Check ApplicationLibrary.TryGetApplicationsFromFile() for pointers.
                using FileStream file = new(SplitPaths[0], FileMode.Open, FileAccess.Read);
                // NOTE: Either the 00 or the merged dump will be first, so this is probably fine! (I sure hope so me)
                

                if (IsXci)
                {
                    // For XCI games
                    LibHac.Tools.Fs.Xci xci = new(_KeySet, file.AsStorage());
                    //applications = GetApplicationsFromPfs(xci.OpenPartition(XciPartitionType.Secure), applicationPath);
                }
                else
                {
                    // For NSP games
                    PartitionFileSystem pfs = new();
                    pfs.Initialize(file.AsStorage());
                }

                
                
                

                
                //ApplicationData result = GetApplicationFromNsp(pfs, applicationPath);
                // In this struct there exists the title name from the NSP. Go figure it out future me!
                
                
                // Replace this with libhac or something idk
                FileName = Path.GetDirectoryName(Dir);
                FileName = FileName.Remove(0, FileName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                
                
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
