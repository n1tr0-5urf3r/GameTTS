using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GameTTS_GUI
{
    /// <summary>
    /// This class is merely an interface to the underlying python process.<br></br>
    /// </summary>
    class Synthesizer
    {
        private Task task;
        private Process process;
        private ProcessStartInfo processInfo;
        private readonly string scriptPath = Environment.CurrentDirectory + @"\GameTTS\run.ps1";
        private StringBuilder pyOut = new StringBuilder();
        private int exitCode;

        public string Output => pyOut.ToString();

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

        public void SendInput(string input)
        {
            if(process != null)
            {
                process.StandardInput.WriteLine(input);
            }
        }

        public void SendInput(SynthesizerTask input) => SendInput(JsonConvert.SerializeObject(input));

        public void StartProcess()
        {
            if (task != null)
                return;

            task = Task.Factory.StartNew(() => 
            {
                Console.WriteLine("Starting python process...");
                using (process = Process.Start(processInfo))
                {
                    process.ErrorDataReceived += StoreProcessOutput;
                    process.OutputDataReceived += StoreProcessOutput;
                    process.Exited += (s, a) =>
                    {
                        exitCode = process.ExitCode;
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    Console.WriteLine("Waiting...");
                    process.WaitForExit();
                }

                //probably not necessary, stays here for testing only
                while(!process.HasExited)
                {
                    //wait
                }
            }).ContinueWith(result =>
            {
                Console.WriteLine($"Synthesizer task exited with code {exitCode}.");
                Console.WriteLine(Output);
            });
        }

        private void StoreProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if(e.Data != null)
                pyOut.AppendLine(e.Data);
        }
    }
}
