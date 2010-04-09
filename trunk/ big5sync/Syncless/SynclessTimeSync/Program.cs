﻿// Icon source: http://www.iconfinder.net/icondetails/10407/24/
// Icon source: http://www.webdesignerdepot.com/2009/03/200-free-exclusive-icons-siena/

using DaveyM69.Components;

namespace SynclessTimeSync
{
    public class Program
    {
        private static int? _returnValue;

        public static int Main()
        {
            SNTPClient sntpClient = new SNTPClient();
            sntpClient.UpdateLocalDateTime = true;
            sntpClient.Timeout = 3000;
            sntpClient.QueryServerCompleted += sntpClient_QueryServerCompleted;
            sntpClient.QueryServerAsync();

            while (!_returnValue.HasValue)
            {
            }

            return (int)_returnValue;
        }

        public static void sntpClient_QueryServerCompleted(object sender, DaveyM69.Components.SNTP.QueryServerCompletedEventArgs e)
        {
            _returnValue = e.Succeeded && e.LocalDateTimeUpdated ? 0 : -1;
        }
    }
}
