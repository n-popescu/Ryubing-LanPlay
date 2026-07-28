
using Avalonia.Data;
using Avalonia.Platform.Storage;
using System;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class Fat32MergeViewModel : BaseModel
    {
        public void BeStinky()
        {
            Console.WriteLine("Deez Nuts");
        }

        public void OpenFolderPicker() // Does not work right now. :angry_cat:
        {
            //Optional<IStorageFolder> folder = StorageProviderExtensions.Extension.OpenSingleFolderPickerAsync();
        }

        public void MergeDump()
        {
            Console.WriteLine("Not implemented yet you silly person!"); // You know what this does. Make some command stuff that merges the files or something
        }

        public void SetProgress()
        {
            Console.WriteLine("Not implemented yet you silly person!"); // Related to XCITrimmerViewModel.cs > SetProgress()
        }
    }
}
