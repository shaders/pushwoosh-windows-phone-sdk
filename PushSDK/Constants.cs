using System;

namespace PushSDK
{
    internal static class Constants
    {
        // request consts
        public const int DeviceType = 5;
        private static string Host = "https://cp.pushwoosh.com/";

        //channel consts
        public const string ChannelName = "PW-WP";

        public static void setHost(string newHost)
        {
            Host = newHost;
        }
        public static string RequestDomain
        {
            get { return Host + "json/1.3/"; }
        }

        public static string HtmlPageUrl
        {
            get { return Host + "content/"; }
        }
    }
}
