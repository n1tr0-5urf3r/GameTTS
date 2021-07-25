using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameTTS_GUI
{
    /// <summary>
    /// This class is merely an interface to the underlying python process.<br></br>
    /// </summary>
    class Synthesizer
    {
        private Process process;
        private ProcessStartInfo processInfo;
        private readonly string scriptPath = Environment.CurrentDirectory + @"\GameTTS\run.ps1";

        public Synthesizer()
        {
            processInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted \"{scriptPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        public void StartProcess()
        {
            using (process = Process.Start(processInfo))
            {
                using (StreamReader myStreamReader = process.StandardOutput)
                {
                    var outputString = myStreamReader.ReadLine();
                    process.WaitForExit();

                    Console.WriteLine(outputString);
                }
            }
        }
    }
}
