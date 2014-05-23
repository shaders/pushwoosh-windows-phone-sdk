using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PushSDK.Classes;

namespace PushSDK
{

    internal class TagsService : PushwooshAPIServiceBase
    {
        private readonly string _appId;

        public event EventHandler<List<KeyValuePair<string, string> > > OnSuccess;
        public event EventHandler<string> OnError;

        public TagsService(string appId)
        {
            _appId = appId;
        }

        /// <summary>
        /// Sending tag to server
        /// </summary>
        /// <param name="tagList">Tags list</param>
        public void SendRequest(List<KeyValuePair<string,object>> tagList)
        {
            JObject requestJson = BuildRequest(tagList);
            InternalSendRequestAsync(requestJson, Constants.TagsUrl, (sender, arg) => { UploadStringCompleted(arg); }, OnError);
        }

        /// <summary>
        /// Sending tag to server
        /// </summary>
        /// <param name="tagList">Tags list</param>
        public void SendRequest(String[] key, object[] values)
        {
            JObject requestJson = BuildRequest(key, values);
            InternalSendRequestAsync(requestJson, Constants.TagsUrl, (sender, arg) => { UploadStringCompleted(arg); }, OnError);
        }

        private JObject BuildRequest(IEnumerable<KeyValuePair<string, object>> tagList)
        {
            JObject tags = new JObject();
            foreach (var tag in tagList)
            {
                tags.Add(new JProperty(tag.Key, tag.Value));
            }

            return BuildRequest(tags);
        }

        private JObject BuildRequest(String[] key, object[] values)
        {
            JObject tags = new JObject();

            int lenght = key.Length >= values.Length ? values.Length : key.Length;
            for (int i = 0; i < lenght; i++)
            {
                tags.Add(new JProperty(key[i], values[i]));
            }
            return BuildRequest(tags);
        }

        private JObject BuildRequest(JObject tags)
        {
            return new JObject(
                                 new JProperty("application", _appId),
                                 new JProperty("hwid", SDKHelpers.GetDeviceUniqueId()),
                        new JProperty("tags", tags));
        }

        private void UploadStringCompleted(JObject jRoot)
        {
                if (JsonHelpers.GetStatusCode(jRoot) == 200)
                {
                    var skippedTags = new List<KeyValuePair<string, string>>();

                    if (jRoot["response"].HasValues)
                    { 
                        JArray jItems = jRoot["response"]["skipped"] as JArray;
                        skippedTags = jItems.Select(jItem => new KeyValuePair<string, string>(jItem.Value<string>("tag"), jItem.Value<string>("reason"))).ToList();
                    }

                if(OnSuccess != null)
                    OnSuccess(this, skippedTags);
                }
                else
            {
                if(OnError != null)
                    OnError(this, JsonHelpers.GetStatusMessage(jRoot));
            }
        }
    }

}
