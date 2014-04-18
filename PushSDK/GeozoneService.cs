using System;
using System.Device.Location;
using System.Net;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PushSDK.Classes;

namespace PushSDK
{
    public class GeozoneService
    {
        private const int MovementThreshold = 100;
        private readonly TimeSpan _minSendTime = TimeSpan.FromMinutes(10);

        private GeoCoordinateWatcher _watcher;
        private GeoCoordinateWatcher LazyWatcher
        {
            get 
            {
                if (_watcher == null)
                {
                    _watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
                    _watcher.MovementThreshold = MovementThreshold;
                    _watcher.PositionChanged += WatcherOnPositionChanged;
                }
                return _watcher;
            }
        }
        private readonly GeozoneRequest _geozoneRequest = new GeozoneRequest();

        private TimeSpan _lastTimeSend;

        public GeozoneService(string appId)
        {
            _geozoneRequest.AppId = appId;
        }

        public void Start()
        {
            LazyWatcher.Start();
        }

        public void Stop()
        {
            LazyWatcher.Stop();
        }

        private void WatcherOnPositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
			try
			{
				if (DateTime.Now.TimeOfDay.Subtract(_lastTimeSend) >= _minSendTime)
				{
					_geozoneRequest.Lat = e.Position.Location.Latitude;
					_geozoneRequest.Lon = e.Position.Location.Longitude;

					WebClient webClient = new WebClient();
					webClient.UploadStringCompleted += (o, args) =>
														   {
																   if (args.Error == null)
																   {
																	   JObject jRoot = JObject.Parse(args.Result);

																	   if (JsonHelpers.GetStatusCode(jRoot) == 200)
																	   {
																		   double dist = jRoot["response"].Value<double>("distance");
																		   if (dist > 0)
																			   LazyWatcher.MovementThreshold = dist / 2;
																	   }
																   }
														   };
					string request = string.Format("{{\"request\":{0}}}", JsonConvert.SerializeObject(_geozoneRequest));
					webClient.UploadStringAsync(Constants.GeozoneUrl, request);

					_lastTimeSend = DateTime.Now.TimeOfDay;
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("Error when handling position change: " + ex.ToString());
			}
		}
    }
}
