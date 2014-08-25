using System;
using System.Threading;
using Newtonsoft.Json;
using Microsoft.Phone.Info;

namespace PushSDK.Classes
{
    [JsonObject]
    internal class RegistrationRequest : BaseRequest
    {
        [JsonProperty("device_type")]
        public int DeviceType
        {
            get { return Constants.DeviceType; }
        }

        [JsonProperty("push_token")]
        public string PushToken { get; set; }

        [JsonProperty("language")]
        public string Language
        {
            get { return Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName; }
        }

        [JsonProperty("timezone")]
        public double Timezone
        {
            get { return TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds; }
        }

        [JsonProperty("os_version")]
        public string OSVersion
        {
            get { return System.Environment.OSVersion.Version.Major + "." + System.Environment.OSVersion.Version.Minor; }
        }

        [JsonProperty("device_model")]
        public string DeviceModel
        {
            get {
                var phone = Ailon.WP.Utils.PhoneNameResolver.Resolve(DeviceStatus.DeviceManufacturer, DeviceStatus.DeviceName);
                return phone.FullCanonicalName;
            }
        }


        public override string GetMethodName() { return "registerDevice"; }
    }
}