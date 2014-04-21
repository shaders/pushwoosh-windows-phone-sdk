using System;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using PushSDK.Classes;

namespace PushSDK.Controls
{
    public partial class PushPage
    {
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ToastPush push = SDKHelpers.ParsePushData(e.Uri.ToString());
            NotificationService instance = NotificationService.GetCurrent();
            if(instance != null)
            {
                instance.FireAcceptedPush(push);
            }
            else
            {
                NotificationService.StartPush = push;
            }

            string startPage = "/" + WMAppManifestReader.GetInstance().NavigationPage;
            ((PhoneApplicationFrame) Application.Current.RootVisual).Navigate(new Uri(startPage, UriKind.RelativeOrAbsolute));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            NavigationService.RemoveBackEntry();
            base.OnNavigatedFrom(e);
        }
    }
}