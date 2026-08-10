using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using DynamicData;
using ExCSS;
using FluentAvalonia.UI.Controls;
using Gommon;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Common.Models;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;
using Ryujinx.Ava.Utilities;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Dialog
{
    public partial class Fat32MergeView : RyujinxControl<Fat32MergeViewModel>
    {
        public List<string> SplitPaths = new List<string>();

        public string Dir;

        public string FileName;
        
        public Fat32MergeView()
        {
            InitializeComponent();
        }
        public static async Task Show()
        {
            ContentDialog contentDialog = new()
            {
                CloseButtonText = string.Empty,
                Content = new Fat32MergeView
                {
                    ViewModel = new Fat32MergeViewModel()
                },
                Title = LocaleManager.Instance[LocaleKeys.MenuBar_Actions_MergeDumpButton]
            };

            Style bottomBorder = new(x => x.OfType<Grid>().Name("DialogSpace").Child().OfType<Border>());
            bottomBorder.Setters.Add(new Setter(IsVisibleProperty, false));

            contentDialog.Styles.Add(bottomBorder);

            await contentDialog.ShowAsync();
        }

        private void Merge(object sender, RoutedEventArgs e)
        {
            ViewModel.MergeDump(Dir, FileName, SplitPaths);
        }

        private async void OpenFolderPicker(object sender, RoutedEventArgs e)
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
                Logger.Error?.Print(LogClass.Application, exception.ToString());
                Logger.Error?.Print(LogClass.Application, "Merge failed!");
            }
            
        }
        
        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)Parent!).Hide();
        }
    }
}
