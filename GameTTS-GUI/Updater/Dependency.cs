using System;
using Newtonsoft.Json;

namespace GameTTS_GUI.Updater
{
    /// <summary>
    /// Basically a JSON-object to communicate dependencies with the app. <br></br>
    /// Dependencies are listed in a json which is downloaded from the server and
    /// then parsed into an object of this class.
    /// </summary>
    public class Dependency
    {
        /// <summary>
        /// The command or file name to be used.
        /// <example>For example:
        /// <code>
        ///     python 
        /// </code>
        /// for the cmd python command or 
        /// <code>
        ///     install.ps1 
        /// </code> for the file name
        /// </example>
        /// </summary>
        /// 
        public string Name { get; set; }

        /// <summary>
        /// The necessary major version number, if any.
        /// </summary>
        [JsonProperty]
        private int versionMajor { get; set; }

        /// <summary>
        /// The necessary minor version number, if any
        /// </summary>
        [JsonProperty]
        private int versionMinor { get; set; }

        /// <summary>
        /// The URL where the dependency can be downloaded from.
        /// </summary>
        public string URL { get; set; }

        /// <summary>
        /// A shorthand property to be used within the app instead of the single
        /// version integers. 
        /// </summary>
        [JsonIgnore]
        public Version Version { get => new Version(versionMajor, versionMinor); }

        /// <summary>
        /// The SHA1 sum to be checked for after downloading, if needed.
        /// </summary>
        public string Checksum { get; set; }
    }
}
