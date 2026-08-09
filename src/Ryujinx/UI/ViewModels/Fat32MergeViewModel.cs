
using Avalonia.Data;
using Avalonia.Platform.Storage;
using Ryujinx.Ava.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace Ryujinx.Ava.UI.ViewModels
{
    public class Fat32MergeViewModel : BaseModel
    {
        public void MergeDump(string dir, string filename)
        {
            try
            {
                string commandName;
                
                Console.WriteLine("Beginning merge");
                if (File.Exists($"{dir}{filename}"))
                {
                    File.Delete($"{dir}{filename}");
                }
                Console.WriteLine("Old file purged");
                Console.WriteLine($"{dir}{filename}");
                
                // Replace this garbage 2GB limited code with OS commands NOW! - Ghost of awesomeanchovies past
                
                //xcopy (windows)
                //??? (mac idk what they use)
                
                
                // NOTE: Copy commands built into stream in C# are limited to 2GB per file. Using OS commands gets around this.
                if (OperatingSystem.IsWindows())
                {
                    
                }
                else if (OperatingSystem.IsMacOS())
                {
                    
                }
                else if (OperatingSystem.IsLinux())
                {
                    Console.WriteLine("Beginning merge using linux bash commands");

                    string thething = $"-c \"cat \'{dir}*\' > \'{dir}{filename}test.xci\'\"";
                    
                    Console.WriteLine(thething);
                    Process.Start("/bin/bash", thething); // Thank you bash for auto ordering by name.
                }
                else
                {
                    Console.WriteLine("Unsupported OS!");
                }
                
                
                /*
                using (Stream mergedFile = File.Open($"{dir}dingus{filename}", FileMode.Create)) // SWEET LIBERTY WHAT IS THIS ABOMINATION I MADE???
                {
                    foreach (string file in files)
                    {
                        mergedFile.Write(File.ReadAllBytes(file));
                    }
                } */
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
