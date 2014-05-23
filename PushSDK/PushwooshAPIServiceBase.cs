using System;
using System.Diagnostics;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PushSDK.Classes;
using System.IO;
using System.Threading.Tasks;

namespace PushSDK
{
    internal abstract class PushwooshAPIServiceBase
    {
        protected void InternalSendRequestAsync(object request, Uri url, EventHandler<JObject> successEvent, EventHandler<string> errorEvent)
        {
            var webClient = new WebClient();
            webClient.UploadStringCompleted += (sender, args) =>
            {
                string errorMessage = String.Empty;

                if (args.Error != null)
                {
                    errorMessage = args.Error.Message;
                }
                else
                {
                    Debug.WriteLine("Response: " + args.Result);

                    JObject jRoot = JObject.Parse(args.Result);
                    int code = JsonHelpers.GetStatusCode(jRoot);
                    if (code == 200 || code == 103)
                    {
                        if (successEvent != null)
                        {
                            successEvent(this, jRoot);
                        }
                    }
                    else
                    {
                        errorMessage = JsonHelpers.GetStatusMessage(jRoot);
                    }
                }

                if (!String.IsNullOrEmpty(errorMessage))
                {
                    Debug.WriteLine("Error: " + errorMessage);

                    if (errorEvent != null)
                    {
                        errorEvent(this, errorMessage);
                    }
                }
            };

            string requestString = String.Format("{{ \"request\":{0}}}", JsonConvert.SerializeObject(request));
            Debug.WriteLine("Sending request: " + request);

            webClient.UploadStringAsync(url, requestString);
        }
    }
}