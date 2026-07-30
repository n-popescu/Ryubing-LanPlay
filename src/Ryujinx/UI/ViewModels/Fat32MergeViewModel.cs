
using Avalonia.Data;
using Avalonia.Platform.Storage;
using Ryujinx.Ava.Utilities;
using System;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class Fat32MergeViewModel : BaseModel
    {
        public void BeStinky()
        {
            Console.WriteLine("Deez Nuts");
        }

        public void MergeDump()
        {
            RyujinxApp.MainWindow.ViewModel.StorageProvider.OpenSingleFolderPickerAsync();
            Console.WriteLine("Not implemented yet you silly person!"); // You know what this does. Make some command stuff that merges the files or something
        }

        public void SetProgress()
        {
            Console.WriteLine("Updating not implemented"); // Related to XCITrimmerViewModel.cs > SetProgress()
        }
    }
}
