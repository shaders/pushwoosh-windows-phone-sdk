using System.Net;
using Newtonsoft.Json;
using PushSDK.Classes;

namespace PushSDK
{
    internal class StatisticService : PushwooshAPIServiceBase
    {
        private readonly StatisticRequest _request;

        public StatisticService(string appId)
        {
            _request = new StatisticRequest {AppId = appId};
        }

        public void SendPushOpen(string hash)
        {
            _request.Hash = hash;
            InternalSendRequestAsync(_request, Constants.StatisticUrl, null, null);
        }

        public void SendAppOpen()
        {
            _request.Hash = null;
            InternalSendRequestAsync(_request, Constants.AppOpenUrl, null, null);
        }
    }
}
