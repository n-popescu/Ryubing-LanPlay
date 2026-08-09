
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
                    Console.WriteLine("Beginning merge using windows CMD"); // Thank you stack random ~~citizen~~ stack exchange post

                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = "CMD.exe";
                    processStartInfo.WorkingDirectory = dir;
                    processStartInfo.Arguments = $"/C copy /B * \"{filename}\"";
                    
                    Console.WriteLine(processStartInfo.Arguments);
                    Process.Start(processStartInfo); // Thank you cmd for auto ordering by name.
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Console.WriteLine("Beginning merge using whatever mac uses"); // Thank you stack random ~~citizen~~ stack exchange post

                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = $"\"cat * > \'{filename}\'\"";
                    processStartInfo.WorkingDirectory = dir;
                    
                    Console.WriteLine(processStartInfo.Arguments);
                    Process.Start(processStartInfo); // Thank you cmd for auto ordering by name.
                }
                else if (OperatingSystem.IsLinux())
                {
                    Console.WriteLine("Beginning merge using linux bash commands"); // Thank you stack random ~~citizen~~ stack exchange post

                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = "/bin/bash";
                    processStartInfo.WorkingDirectory = dir;
                    processStartInfo.Arguments = $"-c \"cat * > \'{filename}\'\"";
                    
                    Console.WriteLine(processStartInfo.Arguments);
                    Process.Start(processStartInfo); // Thank you bash for auto ordering by name.
                }
                else if (OperatingSystem.IsFreeBSD())
                {
                    Console.WriteLine("Unsupported OS!");
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
