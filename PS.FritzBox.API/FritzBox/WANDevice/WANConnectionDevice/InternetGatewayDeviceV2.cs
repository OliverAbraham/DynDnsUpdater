using System;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;
using PS.FritzBox.API.Base;
using PS.FritzBox.API.SOAP;
using System.Net;
using System.Collections.Generic;

namespace PS.FritzBox.API.WANDevice.WANConnectionDevice
{
    /// <summary>
    /// client for wan ppp connection service
    /// </summary>
    public class InternetGatewayDeviceV2 : FritzTR64Client
    {
        #region Construction / Destruction
        
        public InternetGatewayDeviceV2(string url, int timeout) : base(url, timeout)
        {
        }
        
        public InternetGatewayDeviceV2(string url, int timeout, string username) : base(url, timeout, username)
        {
        }
        
        public InternetGatewayDeviceV2(string url, int timeout, string username, string password) : base(url, timeout, username, password)
        {
        }
        
        public InternetGatewayDeviceV2(ConnectionSettings connectionSettings) : base(connectionSettings)
        {
        }

        #endregion

        /// <summary>
        /// Gets the control url
        /// </summary>
        protected override string ControlUrl => "/igdupnp/control/WANIPConn1";

        /// <summary>
        /// Gets the request namespace
        /// </summary>                                 
        protected override string RequestNameSpace => "urn:schemas-upnp-org:service:WANIPConnection:1";

        /// <summary>
        /// Method to get the external ip v6 address
        /// </summary>
        /// <returns>the external ip address</returns>
        public async Task<string> GetExternalIPAddressAsync()
        {
            XDocument document = await this.InvokeAsync("X_AVM_DE_GetExternalIPv6Address", null);
            return document.Descendants("NewExternalIPv6Address").First().Value;
        }

        /// <summary>
        /// Method to get the external ipv6 prefix
        /// </summary>
        /// <returns>the external ipv6 prefix</returns>
        public async Task<string> GetIPv6Prefix()
        {
            XDocument document = await this.InvokeAsync("X_AVM_DE_GetIPv6Prefix", null);
            return document.Descendants("NewIPv6Prefix").First().Value;
        }
    }
}
