
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
                if (File.Exists($"{dir}{filename}"))
                {
                    File.Delete($"{dir}{filename}");
                }
                Console.WriteLine("Old file purged");
                
                // Replace this garbage 2GB limited code with OS commands NOW! - Ghost of awesomeanchovies past
                
                //xcopy (windows)
                //??? (mac idk what they use)
                //cat (linux?)
                
                using (Stream mergedFile = File.Open($"{dir}dingus{filename}", FileMode.Create)) // SWEET LIBERTY WHAT IS THIS ABOMINATION I MADE???
                {
                    foreach (string file in files)
                    {
                        mergedFile.Write(File.ReadAllBytes(file));
                    }
                }
                Console.WriteLine("It has been done.");
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("used by another process"))
                {
                    Console.WriteLine("File is in use by another process!");
                }
                Console.WriteLine(e.ToString());
            }
        }

        public void SetProgress()
        {
            Console.WriteLine("Updating not implemented"); // Related to XCITrimmerViewModel.cs > SetProgress()
        }
    }
}
