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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ryujinx.Ava.UI.Views.Dialog
{
    public partial class Fat32MergeView : RyujinxControl<Fat32MergeViewModel>
    {
        public List<string> SplitPaths; // Store the strings for the split dump files here so we can mess around lol.

        public string Dir; // Save the dir itself.

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
            ViewModel.MergeDump(Dir, FileName);
        }

        private void SetProgress(object sender, RoutedEventArgs e)
        {
            ViewModel.SetProgress(); //Test commit contents
        }

        private async void OpenFolderPicker(object sender, RoutedEventArgs e)
        {
            try
            {
                Optional<IStorageFolder> folder = await RyujinxApp.MainWindow.ViewModel.StorageProvider.OpenSingleFolderPickerAsync();
                Dir = folder.Value.Path.LocalPath; 
                SplitPaths = Directory.EnumerateFiles(Dir, "*").ToList();
                
                // Begin the name reading stuff here
                
                FileName = Path.GetFileName(Dir);
                if (SplitPaths.Contains($"{Dir}{FileName}")) // This thing made me use lists instead of arrays. Array stinky.
                {
                    SplitPaths.Remove($"{Dir}{FileName}");
                }
                Console.WriteLine($"Filename is {FileName}");
            }
            catch (Exception exception)
            {
                Console.Write(exception.ToString()); // TODO: Replace this with proper logging in the final product
            }
            
        }
        
        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)Parent!).Hide();
        }
    }
}
