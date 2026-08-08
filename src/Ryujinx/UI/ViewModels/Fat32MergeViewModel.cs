
using Avalonia.Data;
using Avalonia.Platform.Storage;
using Ryujinx.Ava.Utilities;
using System;
using System.IO;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class Fat32MergeViewModel : BaseModel
    {
        public void MergeDump(string[] files, string dir, string filename)
        {
            try
            {
                Console.WriteLine("Beginning merge");
                if (File.Exists($"{dir}mergedfile"))
                {
                    File.Delete($"{dir}mergedfile");
                }

                using (Stream MergedFile = File.Open($"{dir}mergedfile", FileMode.Create)) // SWEET LIBERTY WHAT IS THIS ABOMINATION I MADE???
                {
                    foreach (string file in files)
                    {
                        MergedFile.Write(File.ReadAllBytes(file));
                    }
                }
                Console.WriteLine("It has been done.");

            }
            catch
            {
                Console.WriteLine("Something blew up ya goober!");
            }
        }

        public void SetProgress()
        {
            Console.WriteLine("Updating not implemented"); // Related to XCITrimmerViewModel.cs > SetProgress()
        }
    }
}
