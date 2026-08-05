using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
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
            ViewModel.MergeDump();
        }

        private void SetProgress(object sender, RoutedEventArgs e)
        {
            ViewModel.SetProgress(); //Test commit contents
        }

        private async void OpenFolderPicker(object sender, RoutedEventArgs e)
        {
            try
            {
                // Note for future me: Hey you! Look at the keys install function in MainWindowViewModel.cs!
                // This section does the silly folder opening doodad. Oh and also actually gets the files.
                Optional<IStorageFolder> folder = await RyujinxApp.MainWindow.ViewModel.StorageProvider.OpenSingleFolderPickerAsync();
                string dir = folder.Value.Path.LocalPath; 
                string[] files = Directory.EnumerateFiles(dir, "*").ToArray();
                
                // Debugging stuff 1
                Console.WriteLine(dir);
                foreach (string file in files)
                {
                    Console.WriteLine(file);
                }
                
                // Ok future me, here, you are going to want to borrow bubbles xci / nsp reading code from tkmm
                string gamedata = "Stuff from the tkmm code"; // Wait does the tkmm code also allow you to view the filename in its header?
                string extension = "XCI or NSP"; // This also comes from the tkmm code btw.
                string combinedname = $"{gamedata}.{extension}"; // This will look better ok?
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
