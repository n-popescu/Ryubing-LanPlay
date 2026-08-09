
using Avalonia.Data;
using Avalonia.Platform.Storage;
using Ryujinx.Ava.Utilities;
using Ryujinx.Common.Logging;
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
                if (File.Exists($"{dir}{filename}"))
                {
                    File.Delete($"{dir}{filename}");
                    Logger.Notice.Print(LogClass.Application, "Removed pre-existing file");
                }

                // NOTE: Copy commands built into stream in C# are limited to 2GB per file. Using OS commands gets around this.
                if (OperatingSystem.IsWindows())
                {
                    Console.WriteLine("Beginning merge using windows CMD");

                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = "CMD.exe";
                    processStartInfo.WorkingDirectory = dir;
                    processStartInfo.Arguments = $"/C copy /B * \"{filename}\"";
                    
                    using var process = Process.Start(processStartInfo);
                    process?.WaitForExit();
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Console.WriteLine("Beginning merge using whatever mac uses");

                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = "/bin/sh";
                    processStartInfo.WorkingDirectory = dir;
                    processStartInfo.Arguments = $"-c \"cat * > \'{filename}\'\"";
                    
                    using var process = Process.Start(processStartInfo);
                    process?.WaitForExit();
                }
                else if (OperatingSystem.IsLinux())
                {
                    Console.WriteLine("Beginning merge using linux bash commands");

                    var processStartInfo = new ProcessStartInfo();
                    processStartInfo.FileName = "/bin/bash";
                    processStartInfo.WorkingDirectory = dir;
                    processStartInfo.Arguments = $"-c \"cat * > \'{filename}\'\"";
                    
                    using var process = Process.Start(processStartInfo);
                    process?.WaitForExit();
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
