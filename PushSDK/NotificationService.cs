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

namespace PushSDK
{
    public class NotificationService
    {
        #region private fields
        private readonly string _pushPage;

        private readonly Collection<Uri> _tileTrustedServers;

        private HttpNotificationChannel _notificationChannel;

        private RegistrationService _registrationService;
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
        /// Get services for sending tags
        /// </summary>
        internal TagsService Tags { get; private set; }

        /// <summary>
        /// Get a service to manage Geozone
        /// </summary>
        internal GeozoneService GeoZone { get; private set; }

        private string AppID { get; set; }

        private StatisticService Statistic { get; set; }

        internal ToastPush LastPush { get; set; }

        #endregion

        #region public events

        /// <summary>
        /// User wants to see push
        /// </summary>
        public event EventHandler<string> OnPushAccepted;

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

            Statistic = new StatisticService(appID);
            Tags = new TagsService(appID);
            GeoZone = new GeozoneService(appID);
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
        public void SendTag(List<KeyValuePair<string, object>> tagList, EventHandler<List<KeyValuePair<string, string>>> OnTagSendSuccess, EventHandler<string> OnError)
        {
            TagsService Tags = new TagsService(AppID);
            Tags.OnSuccess += OnTagSendSuccess;
            Tags.OnError += OnError;
            Tags.SendRequest(tagList);
        }

        public void StartGeoLocation()
        {
            GeoZone.Start();
        }

        public void StopGeoLocation()
        {
            GeoZone.Stop();
        }

        private void SubscribeToChannelEvents()
        {
            //Register to UriUpdated event - occurs when channel successfully opens
            _notificationChannel.ChannelUriUpdated += ChannelChannelUriUpdated;

            //general error handling for push channel
            _notificationChannel.ErrorOccurred += Channel_ErrorOccurred;
            _notificationChannel.ShellToastNotificationReceived += ChannelShellToastNotificationReceived;
        }

        /// <summary>
        /// Unsubscribe from pushes at pushwoosh server
        /// </summary>
        public void UnsubscribeFromPushes()
        {
            if (_registrationService == null || _notificationChannel == null)
                return;

            _notificationChannel.UnbindToShellTile();
            _notificationChannel.UnbindToShellToast();

            PushToken = "";
            _notificationChannel.Close();
            _notificationChannel = null;
            _registrationService.Unregister();
        }

        #endregion

        #region private methods

        private void SubscribeToPushwoosh(string appID)
        {
            if (_registrationService == null)
                _registrationService = new RegistrationService();

            _registrationService.Register(appID, _notificationChannel.ChannelUri.ToString());
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
/*                var message = new PushNotificationMessage(e.Collection);
                message.Completed += (o, args) =>
                {
                    if (args.PopUpResult == PopUpResult.Ok)
                        FireAcceptedPush(LastPush);
                };

                message.Show();
 */
            });
        }

        internal void FireAcceptedPush(ToastPush push)
        {
            Statistic.SendRequest(push.Hash);

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
            string pushString = JsonConvert.SerializeObject(push);
            if (OnPushAccepted != null)
                OnPushAccepted(this, pushString);
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

            if (OnPushTokenReceived != null)
                Deployment.Current.Dispatcher.BeginInvoke(() => OnPushTokenReceived(this, _notificationChannel.ChannelUri.ToString()));

            Debug.WriteLine("Register the URI with 3rd party web service. URI is:" + _notificationChannel.ChannelUri);
            SubscribeToPushwoosh(AppID);

            Debug.WriteLine("Subscribe to the channel to Tile and Toast notifications");
            SubscribeToNotifications();
        }

        #endregion
    }
}