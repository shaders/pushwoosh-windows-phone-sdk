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
    internal class GeozoneService : PushwooshAPIServiceBase
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
                }
                return _watcher;
            }
        }

        private readonly GeozoneRequest _geozoneRequest = new GeozoneRequest();

        public event EventHandler<string> OnError;

        private TimeSpan _lastTimeSend;

        public GeozoneService(string appId)
        {
            _geozoneRequest.AppId = appId;
        }

        public void Start()
        {
            LazyWatcher.MovementThreshold = MovementThreshold;
            LazyWatcher.PositionChanged += WatcherOnPositionChanged;
            LazyWatcher.Start();
        }

        public void Stop()
        {
            LazyWatcher.PositionChanged -= WatcherOnPositionChanged;
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

                    InternalSendRequestAsync(_geozoneRequest,
                        (obj, arg) => {
                            double dist = arg["response"].Value<double>("distance");
                            if (dist > 0)
                                LazyWatcher.MovementThreshold = dist / 2;
                        }, OnError);

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
