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
        public static void InternalSendRequestAsync(BaseRequest request, EventHandler<JObject> successEvent, EventHandler<string> errorEvent)
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
                        request.ParseResponse(jRoot);
                        if (successEvent != null)
                        {
                            successEvent(null, jRoot);
                        }
                    }
                    else
                    {
                        errorMessage = JsonHelpers.GetStatusMessage(jRoot);
                        request.ErrorMessage = errorMessage;
                    }
                }

                if (!String.IsNullOrEmpty(errorMessage))
                {
                    Debug.WriteLine("Error: " + errorMessage);
                    if (errorEvent != null)
                        errorEvent(null, errorMessage);
                }
            };

            string requestString = String.Format("{{ \"request\":{0}}}", JsonConvert.SerializeObject(request));
            Debug.WriteLine("Sending request: " + requestString);

            Uri url = new Uri(Constants.RequestDomain + request.GetMethodName(), UriKind.Absolute);
            webClient.UploadStringAsync(url, requestString);
        }
    }
}