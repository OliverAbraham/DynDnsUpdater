﻿using System.Net;
using System.Net.Sockets;

namespace Abraham.Internet
{
	public class InterfaceInformation
	{
		public static string GetOwnIpAddress()
		{
			IPHostEntry host;
			string localIP = "?";
			host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					localIP = ip.ToString();
				}
			}
			return localIP;
		}
	}
}
