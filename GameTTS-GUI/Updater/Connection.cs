using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameTTS_GUI.Updater
{
    public enum ConnectionStatus
    {
        Undefined,
        Disconnected,
        Connected,
        ConnectionLost,
        ConnectionReestablished
    }

    public static class Connection
    {
        static Timer connectionChecker;

        public static Action<ConnectionStatus> OnChecked;
        public static ConnectionStatus LastStatus { get; private set; } = ConnectionStatus.Undefined;

        public static void RunWatcher(long checkIntervalMillis)
        {
            if (connectionChecker != null)
                return;

            connectionChecker = new Timer((a) =>
            {
                OnChecked?.Invoke(CheckConnection());
            }, null, 0, checkIntervalMillis);
        }

        public static void StopWatcher()
        {
            if(connectionChecker != null)
            {
                connectionChecker.Dispose();
                connectionChecker = null;
                OnChecked = null;
            }
        }

        /// <summary>
        /// Sends a ping to Google to check internet connectin. Returns true if ping was successful.
        /// </summary>
        /// <returns></returns>
        public static ConnectionStatus CheckConnection()
        {
            Ping myPing = new Ping();
            string host = "google.com";
            byte[] buffer = new byte[32];
            int timeout = 2000;
            PingOptions pingOptions = new PingOptions();

            try
            {
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);

                if (reply.Status == IPStatus.Success)
                {
                    SetLastStatus(true);
                }
            }
            catch (PingException) 
            {
                SetLastStatus(false);
            }

            return LastStatus;
        }

        private static void SetLastStatus(bool pingSucceeded)
        {
            switch(LastStatus)
            {
                case ConnectionStatus.Undefined:
                    if (pingSucceeded)
                        LastStatus = ConnectionStatus.Connected;
                    else
                        LastStatus = ConnectionStatus.Disconnected;
                    break;
                case ConnectionStatus.ConnectionReestablished:
                    if (pingSucceeded)
                        LastStatus = ConnectionStatus.Connected;
                    else
                        LastStatus = ConnectionStatus.ConnectionLost;
                    break;
                case ConnectionStatus.ConnectionLost:
                    if (pingSucceeded)
                        LastStatus = ConnectionStatus.ConnectionReestablished;
                    else
                        LastStatus = ConnectionStatus.Disconnected;
                    break;
                case ConnectionStatus.Disconnected:
                    if (pingSucceeded)
                        LastStatus = ConnectionStatus.ConnectionReestablished;
                    break;
                case ConnectionStatus.Connected:
                    if (!pingSucceeded)
                        LastStatus = ConnectionStatus.ConnectionLost;
                    break;
            }
        }
    }
}
