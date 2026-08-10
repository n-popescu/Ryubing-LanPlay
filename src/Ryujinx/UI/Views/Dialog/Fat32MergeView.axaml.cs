using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.UI.Controls;
using Ryujinx.Ava.UI.ViewModels;
using System.Collections.Generic;
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
            ViewModel.MergeDump();
        }

        private async void OpenFolderPicker(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenFolderPicker();
        }
        
        private void Close(object sender, RoutedEventArgs e)
        {
            ((ContentDialog)Parent!).Hide();
        }
    }
}
