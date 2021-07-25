using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GameTTS_GUI
{
    static class Dependencies
    {
        public static void InstallDependency(string url, string fileName)
        {

            using (var client = new WebClient())
            {
                //"https://www.python.org/ftp/python/3.9.6/python-3.9.6-amd64.exe", "pythonInstall.exe"
                client.DownloadFile(url, fileName);
            }

            //execute setup
            using (Process process = Process.Start(new ProcessStartInfo(fileName)))
            {
                process.WaitForExit();
            }
        }

        public static string GetVersion(string execName, string versionPrefix)
        {
            string output = GetProgramVersionOutput(execName);

            output = output.Replace(versionPrefix, "").TrimStart(' ');

            if (output.Contains(" "))
                output = output.Substring(0, output.IndexOf(' '));

            return output;
        }

        public static bool IsInstalled(string execName, string versionPrefix, int? major = null, int? minor = null)
        {
            string output = GetProgramVersionOutput(execName);

            if (output == null)
                return false;

            //this line also starty with "Python ", so we'd have a false positive here
            if (output.Contains("was not found"))
                return false;

            var version = Version.Parse(GetVersion(execName, versionPrefix));

            if (output.Contains(versionPrefix))
            {
                if (major != null)
                    if (version.Major < major.Value)
                        return false;

                if (minor != null)
                    if (version.Minor < minor.Value)
                        return false;
            }
            else
                return false;

            return true;
        }

        public static string GetProgramVersionOutput(string execName)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo()
            {
                FileName = execName,
                Arguments = "--version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string output = null;

            try
            {
                using (Process process = Process.Start(processInfo))
                {
                    using (StreamReader myStreamReader = process.StandardOutput)
                    {
                        output = myStreamReader.ReadLine();
                        process.WaitForExit();
                    }
                }
            }
            catch (Win32Exception)
            {
                //file not found, can return null
                return null;
            }

            return output;
        }
    }
}
