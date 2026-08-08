
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

                using (Stream mergedFile = File.Open($"{dir}{filename}", FileMode.Create)) // SWEET LIBERTY WHAT IS THIS ABOMINATION I MADE???
                {
                    foreach (string file in files)
                    {
                        mergedFile.Write(File.ReadAllBytes(file));
                    }
                }
                Console.WriteLine("It has been done.");
            }
            catch
            {
                Console.WriteLine("Merge failed!");
            }
        }

        public string IsXciOrNsp(string file)
        {
            Stream file01 = File.Open(file, FileMode.Open); // Totally not trying to adapt tkmm code frfr (thanks bubbles)
            Span<byte> buffer = stackalloc byte[4]; file01.ReadExactly(buffer);
            if (buffer.SequenceEqual("PFS0"u8))
            { 
                return ".nsp";
            }
            return ".xci";
        }

        public void SetProgress()
        {
            Console.WriteLine("Updating not implemented"); // Related to XCITrimmerViewModel.cs > SetProgress()
        }
    }
}
