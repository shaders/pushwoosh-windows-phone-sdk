using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Notification;
using Microsoft.Phone.Tasks;
using PushSDK.Classes;
using PushSDK.Controls;
using Newtonsoft.Json;
using Microsoft.Phone.Info;
using Newtonsoft.Json.Linq;

namespace PushSDK
{
    public class NotificationService
    {
        #region private fields
        private readonly string _pushPage;

        private readonly Collection<Uri> _tileTrustedServers;

        private HttpNotificationChannel _notificationChannel;
        #endregion

        #region public properties

        static internal ToastPush StartPush { get; set; }

        /// <summary>
        /// Get content of last push notification
        /// </summary>
        public string LastPushContent
        {
            get
            {
                return LastPush != null ? LastPush.Content : string.Empty;
            }
        }

        /// <summary>
        /// Get user data from the last push came
        /// </summary>
        public string UserData { get{return LastPush != null ? LastPush.UserData : string.Empty;}}

        /// <summary>
        /// Get push token
        /// </summary>
        public string PushToken { get; private set; }

        /// <summary>
        /// Get unique hardware ID, used in communication with Pushwoosh Remote API
        /// </summary>
        public string DeviceUniqueID { get { return SDKHelpers.GetDeviceUniqueId(); } }
        #endregion

        #region internal properties

        /// <summary>
        /// Get a service to manage Geozone
        /// </summary>
        internal GeozoneService GeoZone { get; private set; }

        private string AppID { get; set; }

        internal ToastPush LastPush { get; set; }

        #endregion

        #region public events

        /// <summary>
        /// User wants to see push
        /// </summary>
        public event EventHandler<ToastPush> OnPushAccepted;

        /// <summary>
        /// Push registration succeeded
        /// </summary>
        public event EventHandler<string> OnPushTokenReceived;

        /// <summary>
        /// Push registration failed
        /// </summary>
        public event EventHandler<string> OnPushTokenFailed;

        #endregion

        #region Singleton

        private static NotificationService _instance;

        // may return null if no instance present
        public static NotificationService GetCurrent()
        {
            return _instance;
        }

        public static NotificationService GetCurrent(string appID, string pushPage, IEnumerable<string> tileTrustedServers)
        {
            return _instance ?? (_instance = tileTrustedServers == null ? new NotificationService(appID, pushPage) : new NotificationService(appID, pushPage, tileTrustedServers));
        }

        #endregion

        /// <param name="appID">PushWoosh application id</param>
        /// <param name="pushPage">Page on which the navigation is when receiving toast push notification </param>
        private NotificationService(string appID, string pushPage)
        {
            _pushPage = pushPage;
            AppID = appID;
            PushToken = "";

            GeoZone = new GeozoneService(appID);

            AppOpenRequest request = new AppOpenRequest { AppId = appID };
            PushwooshAPIServiceBase.InternalSendRequestAsync(request, null, null);
        }

        /// <param name="appID">PushWoosh application id</param>
        /// <param name="pushPage">Page on which the navigation is when receiving toast push notification </param>
        /// <param name="tileTrustedServers">Uris of trusted servers for tile images</param>
        private NotificationService(string appID, string pushPage, IEnumerable<string> tileTrustedServers)
            : this(appID, pushPage)
        {
            _tileTrustedServers = new Collection<Uri>(tileTrustedServers.Select(s => new Uri(s, UriKind.Absolute)).ToList());
        }

        #region public methods

        /// <summary>
        /// Creates push channel and regestrate it at pushwoosh server to send unauthenticated pushes
        /// </summary>
        public void SubscribeToPushService()
        {
            SubscribeToPushService(string.Empty);
        }

        /// <summary>
        /// Creates push channel and regestrite it at pushwoosh server
        /// <param name="serviceName">
        /// The name that the web service uses to associate itself with the Push Notification Service.
        /// </param>
        /// </summary>        
        public void SubscribeToPushService(string serviceName)
        {
            //Dispatch start push it happened
            if (StartPush != null)
            {
                FireAcceptedPush(StartPush);
                StartPush = null;
            }

            //do nothing if already subscribed
            if (_notificationChannel != null)
                return;

            //First, try to pick up existing channel
            _notificationChannel = HttpNotificationChannel.Find(Constants.ChannelName);
            if (_notificationChannel != null)
            {
                Debug.WriteLine("Channel Exists - no need to create a new one");
                SubscribeToChannelEvents();
            }
            else
            {
                Debug.WriteLine("Trying to create a new channel...");
                _notificationChannel = string.IsNullOrEmpty(serviceName)
                                            ? new HttpNotificationChannel(Constants.ChannelName)
                                            : new HttpNotificationChannel(Constants.ChannelName, serviceName);

                Debug.WriteLine("New Push Notification channel created successfully");
                SubscribeToChannelEvents();

                Debug.WriteLine("Trying to open the channel");
                _notificationChannel.Open();
            }

            if (_notificationChannel.ChannelUri != null)
                ChannelChannelUriUpdated(this, null);
        }

        /// <summary>
        ///  send Tag
        /// </summary>
        public void SendTag(List<KeyValuePair<string, object>> tagList, EventHandler<JObject> OnTagSendSuccess, EventHandler<string> OnError)
        {
            SetTagsRequest request = new SetTagsRequest { AppId = AppID };
            request.BuildTags(tagList);
            PushwooshAPIServiceBase.InternalSendRequestAsync(request, OnTagSendSuccess, OnError);
        }

        public void GetTags(EventHandler<JObject> OnTagsSuccess, EventHandler<string> OnError)
        {
            GetTagsRequest request = new GetTagsRequest { AppId = AppID };
            PushwooshAPIServiceBase.InternalSendRequestAsync(request, (obj, arg) => { if(OnTagsSuccess != null) OnTagsSuccess(this, request.Tags); }, OnError);
        }

        public void StartGeoLocation()
        {
            GeoZone.Start();
        }

        public void StopGeoLocation()
        {
            GeoZone.Stop();
        }

        private void DetachChannelEvents()
        {
            if(_notificationChannel == null)
                return;

            try
            {
                _notificationChannel.ChannelUriUpdated -= ChannelChannelUriUpdated;
                _notificationChannel.ErrorOccurred -= Channel_ErrorOccurred;
                _notificationChannel.ShellToastNotificationReceived -= ChannelShellToastNotificationReceived;
            }
            catch {}

        }

        /// <summary>
        /// Unsubscribe from push notifications
        /// </summary>
        public void UnsubscribeFromPushes(EventHandler<JObject> success, EventHandler<string> failure)
        {
            if (_notificationChannel != null)
                DetachChannelEvents();
            else
                _notificationChannel = HttpNotificationChannel.Find(Constants.ChannelName);

            if (_notificationChannel != null)
            {
                _notificationChannel.UnbindToShellTile();
                _notificationChannel.UnbindToShellToast();
                _notificationChannel.Close();
                _notificationChannel = null;
            }

            PushToken = "";
            UnregisterRequest request = new UnregisterRequest { AppId = AppID };
            PushwooshAPIServiceBase.InternalSendRequestAsync(request, success, failure);
        }

        #endregion

        #region private methods

        private void SubscribeToChannelEvents()
        {
            //Register to UriUpdated event - occurs when channel successfully opens
            _notificationChannel.ChannelUriUpdated += ChannelChannelUriUpdated;

            //general error handling for push channel
            _notificationChannel.ErrorOccurred += Channel_ErrorOccurred;
            _notificationChannel.ShellToastNotificationReceived += ChannelShellToastNotificationReceived;
        }

        private void SubscribeToPushwoosh(string appID)
        {
            string token = _notificationChannel.ChannelUri.ToString();
            RegistrationRequest request = new RegistrationRequest {AppId = appID, PushToken = token};

            PushwooshAPIServiceBase.InternalSendRequestAsync(request,
                (obj, args) =>
                {
                    if (OnPushTokenReceived != null)
                        Deployment.Current.Dispatcher.BeginInvoke(() => OnPushTokenReceived(this, _notificationChannel.ChannelUri.ToString()));
                },
                (obj, args) =>
                {
                    if (OnPushTokenFailed != null)
                        Deployment.Current.Dispatcher.BeginInvoke(() => OnPushTokenFailed(this, request.ErrorMessage));
                });
        }

        private void ChannelShellToastNotificationReceived(object sender, NotificationEventArgs e)
        {
            Debug.WriteLine("/********************************************************/");
            Debug.WriteLine("Received Toast: " + DateTime.Now.ToShortTimeString());

            foreach (string key in e.Collection.Keys)
            {
                Debug.WriteLine("{0}: {1}", key, e.Collection[key]);
                if (key == "wp:Param")
                {
                    LastPush = SDKHelpers.ParsePushData(e.Collection[key]);
                    LastPush.OnStart = false;
                }
            }
            Debug.WriteLine("/********************************************************/");

            Deployment.Current.Dispatcher.BeginInvoke(() =>
            {
                FireAcceptedPush(LastPush);
            });
        }

        internal void FireAcceptedPush(ToastPush push)
        {
            StatisticRequest request = new StatisticRequest { AppId = AppID, Hash = push.Hash };
            PushwooshAPIServiceBase.InternalSendRequestAsync(request, null, null);

            if (push.Url != null || push.HtmlId != -1)
            {
                WebBrowserTask webBrowserTask = new WebBrowserTask();

                if (push.Url != null)
                    webBrowserTask.Uri = push.Url;
                else if (push.HtmlId != -1)
                    webBrowserTask.Uri = new Uri(Constants.HtmlPageUrl + push.HtmlId, UriKind.Absolute);

                webBrowserTask.Show();
            }

            PushAccepted(push);
        }

        private void OnNavigated(object sender, NavigationEventArgs navigationEventArgs)
        {
            ((PhoneApplicationFrame) Application.Current.RootVisual).Navigated -= OnNavigated;
        }

        private void PushAccepted(ToastPush push)
        {
            if (OnPushAccepted != null)
                OnPushAccepted(this, push);
        }

        private void Channel_ErrorOccurred(object sender, NotificationChannelErrorEventArgs e)
        {
            if (OnPushTokenFailed != null)
                OnPushTokenFailed(this, e.Message);

            Debug.WriteLine("/********************************************************/");
            Debug.WriteLine("A push notification {0} error occurred.  {1} ({2}) {3}", e.ErrorType, e.Message, e.ErrorCode, e.ErrorAdditionalData);
        }

        private void SubscribeToNotifications()
        {
            try
            {
                BindToastNotification();
                BindTileNotification();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error notification subscription\n" + e.Message);
            }
        }

        private void BindTileNotification()
        {
            if (_notificationChannel.IsShellTileBound)
            {
                Debug.WriteLine("Already bounded (register) to Tile Notifications");
                return;
            }
                
            Debug.WriteLine("Registering to Tile Notifications");
            // you can register the phone application to receive tile images from remote servers [this is optional]
            if (_tileTrustedServers == null)
                _notificationChannel.BindToShellTile();
            else
                _notificationChannel.BindToShellTile(_tileTrustedServers);
        }

        private void BindToastNotification()
        {
            if (_notificationChannel.IsShellToastBound)
            {
                Debug.WriteLine("Already bounded (register) to to Toast notification");
                return;
            }
                
            Debug.WriteLine("Registering to Toast Notifications");
            _notificationChannel.BindToShellToast();
        }

        private void ChannelChannelUriUpdated(object sender, NotificationChannelUriEventArgs e)
        {
            Debug.WriteLine("Channel opened. Got Uri:\n" + _notificationChannel.ChannelUri);
            Debug.WriteLine("Subscribing to channel events");

            PushToken = _notificationChannel.ChannelUri.ToString();

            Debug.WriteLine("Registering the token with Pushwoosh. URI is:" + _notificationChannel.ChannelUri);
            SubscribeToPushwoosh(AppID);

            Debug.WriteLine("Subscribing the channel to Tile and Toast notifications");
            SubscribeToNotifications();
        }

        #endregion
    }
}