using System;
using System.Net.NetworkInformation;
using System.Threading;

namespace GameTTS_GUI.Updater
{
    /// <summary>
    /// Enumeration to provide useful state information about the connection.
    /// </summary>
    public enum ConnectionStatus
    {
        Undefined,
        Disconnected,
        Connected,
        ConnectionLost,
        ConnectionReestablished
    }

    /// <summary>
    /// Simple static class that serves as a simple monitor for the users internet connection.<br></br>
    /// It can send a ping every x milliseconds and updates it's status property accordingly (see <see cref="RunWatcher(long)"/>). 
    /// Also provides a simple event that can be hooked into after each ping has been evaluated (see <see cref="OnChecked"/>).
    /// </summary>
    public static class Connection
    {
        #region -- ping settings

        private const string URL = "google.com";
        private const int TIMEOUT = 2000;

        #endregion

        static Timer connectionChecker;

        /// <summary>
        /// Simple event that triggers after each connection test and carries the status result.
        /// </summary>
        public static event Action<ConnectionStatus> OnChecked;

        /// <summary>
        /// Connection status after the last ping. 
        /// </summary>
        public static ConnectionStatus Status { get; private set; } = ConnectionStatus.Undefined;

        /// <summary>
        /// Runs a timer (thread) in the background that calls <see cref="CheckConnection"/> method
        /// periodically with the given intervall. This will fire the <see cref="OnChecked"/> event.
        /// </summary>
        /// <param name="checkIntervalMillis"></param>
        public static void RunWatcher(long checkIntervalMillis)
        {
            if (connectionChecker != null)
                return;

            connectionChecker = new Timer((a) =>
            {
                OnChecked?.Invoke(CheckConnection());
            }, null, 0, checkIntervalMillis);
        }


        /// <summary>
        /// Used to stop and dispose the timer and unregister any events.
        /// </summary>
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
        /// Sends a ping to Google to check internet connection.
        /// </summary>
        /// <returns>The current connection status as <see cref="ConnectionStatus"/></returns>
        public static ConnectionStatus CheckConnection()
        {
            Ping myPing = new Ping();
            byte[] buffer = new byte[32];
            PingOptions pingOptions = new PingOptions();

            try
            {
                PingReply reply = myPing.Send(URL, TIMEOUT, buffer, pingOptions);

                if (reply.Status == IPStatus.Success)
                {
                    SetLastStatus(true);
                }
            }
            catch (PingException) 
            {
                SetLastStatus(false);
            }

            return Status;

            //nested function to just use here to determine the current status
            //based on the outcome of the ping sent
            void SetLastStatus(bool pingSucceeded)
            {
                switch (Status)
                {
                    case ConnectionStatus.Undefined:
                        if (pingSucceeded)
                            Status = ConnectionStatus.Connected;
                        else
                            Status = ConnectionStatus.Disconnected;
                        break;
                    case ConnectionStatus.ConnectionReestablished:
                        if (pingSucceeded)
                            Status = ConnectionStatus.Connected;
                        else
                            Status = ConnectionStatus.ConnectionLost;
                        break;
                    case ConnectionStatus.ConnectionLost:
                        if (pingSucceeded)
                            Status = ConnectionStatus.ConnectionReestablished;
                        else
                            Status = ConnectionStatus.Disconnected;
                        break;
                    case ConnectionStatus.Disconnected:
                        if (pingSucceeded)
                            Status = ConnectionStatus.ConnectionReestablished;
                        break;
                    case ConnectionStatus.Connected:
                        if (!pingSucceeded)
                            Status = ConnectionStatus.ConnectionLost;
                        break;
                }
            }
        }
    }
}
