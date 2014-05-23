using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PushSDK.Classes
{
    internal class WMAppManifestReader
    {
        private static WMAppManifestReader instance = null;
        private string navigationPage = string.Empty;

        private WMAppManifestReader()
        {
            this.ReadAppManifest();
        }

        public static WMAppManifestReader GetInstance()
        {
            if (instance == null)
            {
                instance = new WMAppManifestReader();
            }

            return instance;
        }

        private void ReadAppManifest()
        {
            string wmData = string.Empty;
            System.Xml.Linq.XElement appxml = System.Xml.Linq.XElement.Load("WMAppManifest.xml");
            var appElement = (from manifestData in appxml.Descendants("DefaultTask") select manifestData).SingleOrDefault();

            if (appElement != null)
            {
                navigationPage = appElement.Attribute("NavigationPage").Value;
            }

            appElement = (from manifestData in appxml.Descendants("PrimaryToken") select manifestData).SingleOrDefault();
        }

        public string NavigationPage
        {
            get { return this.navigationPage; }
        }
    }
}