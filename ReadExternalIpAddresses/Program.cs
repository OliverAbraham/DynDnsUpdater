using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using PS.FritzBox.API;
using PS.FritzBox.API.WANDevice.WANConnectionDevice;

namespace ReadExternalIpAddresses
{
	class Program
	{
		#region ------------- Command line options ------------------------------------------------
		class Options
		{
			[Option('u', "user", Required = true, HelpText = "fritzbox user name")]
			public string UserName { get; set; }

			[Option('p', "password", Required = true, HelpText = "fritzbox password")]
			public string Password { get; set; }

			[Option('f', "fritzbox number", Required = false, HelpText = "The number to use, if you have more than one fritzbox (default 1)")]
			public int DeviceNumber { get; set; } = 1;
		}

		#endregion



		#region ------------- Fields --------------------------------------------------------------
		private static Options _options;
		private static FritzDevice _device;
		private static ConnectionSettings _settings;
		private static WANPPPConnectionClient  _client1;
		private static InternetGatewayDeviceV2 _client2;
		#endregion



		#region ------------- Init ----------------------------------------------------------------
		static async Task Main(string[] args)
		{
			Console.WriteLine("FritzBox Read external IP addresses");
			ParseCommandLineArguments();

            Console.WriteLine("Searching for devices...");
            IEnumerable<FritzDevice> devices = await FritzDevice.LocateDevicesAsync();

            if (devices.Count() > 0)
            {
                Console.WriteLine($"Found {devices.Count()} devices.");
                string input = string.Empty;
                int deviceIndex = -1;
                int counter = 1;
                foreach (FritzDevice device in devices)
                    Console.WriteLine($"{counter++} - {device.ModelName}");

				Console.WriteLine($"Selecting device {_options.DeviceNumber}");
            }
			else
			{
				_options.DeviceNumber = 1;
			}

            _device = devices.Skip(_options.DeviceNumber-1).First();

			Console.WriteLine($"Device ID : {_device.UDN}");
			Console.WriteLine($"Model name: {_device.ModelName}");

            _settings = new ConnectionSettings();
			_settings.UserName = _options.UserName;
			_settings.Password = _options.Password;

            _client1 = await _device.GetServiceClient<WANPPPConnectionClient>(_settings);
            _client2 = await _device.GetServiceClient<InternetGatewayDeviceV2>(_settings);

            var ipv4 = await _client1.GetExternalIPAddressAsync();
            Console.WriteLine($"external IPv4 address: {ipv4}");

            var ipv6 = await _client2.GetIPv6Prefix();
            Console.WriteLine($"external IPv6Prefix  : {ipv6}");
		}
		#endregion



		#region ------------- Implementation ------------------------------------------------------
		private static void ParseCommandLineArguments()
		{
			string[] args = Environment.GetCommandLineArgs();
			CommandLine.Parser.Default.ParseArguments<Options>(args)
				.WithParsed<Options>(o =>
				{
					_options = o;
				})
				.WithNotParsed<Options>(errs =>
				{
				});
		}
		#endregion
	}
}
