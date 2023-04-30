using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog.Web;
using CommandLine;
using PS.FritzBox.API;
using PS.FritzBox.API.WANDevice.WANConnectionDevice;
using Abraham.ProcessIO;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Text;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.RegularExpressions;
using Abraham.ProgramSettingsManager;

namespace DynDnsUpdater
{
    partial class Program
    {
        #region ------------- Command line options ------------------------------------------------
        class Options
        {
            [Option('a', "appsettingsfile", Required = false, HelpText = "filename of the file containing the appsettings (appsettings.hjson by default)")]
            public string AppsettingsFilename { get; set; }
            
            [Option('l', "logsettingsfile", Required = false, HelpText = "filename of the file containing the NLOG logging settings (NLog.config by default)")]
            public string LogsettingsFilename { get; set; }
        }
        #endregion



        #region ------------- Sub classes ---------------------------------------------------------
        private class IPAdresses
        {
            public string IPv4 { get; set; }
            public string IPv6 { get; set; }
            public string IPv6inclLocalIP { get; set; }

            public IPAdresses()
            {
                IPv4 = "";
                IPv6 = "";
            }

            public IPAdresses(string ipv4, string ipv6)
            {
                IPv4 = ipv4;
                IPv6 = ipv6;
            }

            public bool IsEqualTo(IPAdresses addresses)
            {
                return IPv4 == addresses.IPv4 && IPv6 == addresses.IPv6;
            }

            public IPAdresses Clone()
            {
                return new IPAdresses(IPv4, IPv6);
            }
        }
        #endregion



        #region ------------- Fields --------------------------------------------------------------
        private static Options                               _commandLineOptions;
        private static Configuration		                 _config;
        private static ProgramSettingsManager<Configuration> _configurationManager;
        private static FritzDevice                           _device;
        private static ConnectionSettings                    _settings;
        private static WANPPPConnectionClient                _client1;
        private static InternetGatewayDeviceV2               _client2;
        private static IPAdresses                            _old_addresses = new IPAdresses();
        private static NLog.Logger                           _logger;
        private static DateTime								 _lastEmailSent = new DateTime(1,1,1,0,0,0);
        #endregion



        #region ------------- Init ----------------------------------------------------------------
        static async Task Main(string[] args)
        {
            PrintGreetingOnConsole();
            ParseCommandLineArguments();
            InitLogger();
            LogGreeting();

            ReadConfiguration();
            PrintConfiguration();
            //SendPushbulletMessage("Test");

            await SearchForFritzboxes();
            await SetupFritzboxConnection();

            await Run();
        }
        #endregion



        #region ------------- Implementation ------------------------------------------------------
        #region ------------- Setup ---------------------------------------------------------------
        private static void PrintGreetingOnConsole()
        {
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("");
            Console.WriteLine("----------------------------------------------------------------------------------");
            Console.WriteLine($"DynDnsUpdater for Fritzbox started. Version {AppVersion.Version.VERSION}");
            Console.WriteLine("----------------------------------------------------------------------------------");
        }

        private static void LogGreeting()
        {
            LogDebug("");
            LogDebug("");
            LogDebug("");
            LogDebug("----------------------------------------------------------------------------------");
            LogDebug($"DynDnsUpdater for Fritzbox started. Version {AppVersion.Version.VERSION}");
            LogDebug("----------------------------------------------------------------------------------");
        }

        private static void ParseCommandLineArguments()
        {
            string[] args = Environment.GetCommandLineArgs();
            CommandLine.Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    _commandLineOptions = o;
                })
                .WithNotParsed<Options>(errs =>
                {
                });

            if (_commandLineOptions == null)
                throw new Exception("Program ended");
        }
        #endregion

        #region ------------- Scheduler -----------------------------------------------------------
        private static async Task Run()
        {
            LogDebug("Updating DynDns targets periodically. Press e to exit.");
            while (true)
            {
                await UpdateTargets();
                Wait();
                if (Console.KeyAvailable && Console.ReadKey().KeyChar == 'e')
                {
                    LogDebug("Exiting because user pressed the e key.");
                    break;
                }
            }
            LogDebug("DynDnsUpdater ended");
        }

        private static void Wait()
        {
            for (int i = 0; i < _config.WaitTimeSeconds; i++)
            {
                Thread.Sleep(1000);
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey().KeyChar;
                    if (key == 'c')
                    {
                        LogDebug("Updating because user pressed the c key.");
                        _old_addresses = new IPAdresses(); // force update!
                        return;
                    }
                    if (key == 'e')
                        throw new Exception("Program exited");
                }
            }
        }
        #endregion

        #region ------------- Updating DNS servers (targets) --------------------------------------
        private static async Task UpdateTargets()
        {
            try
            {
                var addresses = await GetCurrentAddresses();
                await UpdateDnsTargets(addresses);
            }
            catch (Exception ex)
            {
                LogError($"Error getting current addresses: {ex.ToString()}");
            }
        }

        private static async Task UpdateDnsTargets(IPAdresses addresses)
        {
            try
            {
                await UpdateDnsTargets_internal(addresses);
            }
            catch (Exception ex)
            {
                LogError($"Error updating the target DNS server(s): {ex.ToString()}");
            }
        }

        private static async Task UpdateDnsTargets_internal(IPAdresses addresses)
        {
            if (!string.IsNullOrWhiteSpace(addresses.IPv4) && !string.IsNullOrWhiteSpace(addresses.IPv6))
            {
                if (addresses.IsEqualTo(_old_addresses))
                {
                    if (_config.LogUnchangedAddress)
                        LogInfo($"IP addresses  unchanged:  {addresses.IPv4}  {addresses.IPv6}");
                }
                else
                {
                    LogInfo($"IP addresses  changed  :  {addresses.IPv4}  {addresses.IPv6}    updating DNS server now");
                    UpdateAllTargets(addresses);
                    _old_addresses = addresses.Clone();

                    UpdateIPCFiles(addresses);
                    LogDebug($"Targets were updated.");
                }
            }
        }

        private static void UpdateAllTargets(IPAdresses addresses)
        {
            LogDebug($"");
            LogDebug($"Updating all targets");
            foreach (var target in _config.Targets)
                UpdateOneTarget(target, addresses);
        }

        private static void UpdateOneTarget(Target target, IPAdresses addresses)
        {
            if (string.IsNullOrWhiteSpace(target.SubdomainlistFile))
                UpdateSimpleTarget(target, addresses, "");
            else
                UpdateMultiSubdomainTarget(target, addresses);
        }

        private static void UpdateMultiSubdomainTarget(Target target, IPAdresses addresses)
        {
            var subdomains = ReadFile(target);

            foreach (var subdomain in subdomains)
                UpdateSimpleTarget(target, addresses, subdomain);
        }

        private static List<string> ReadFile(Target target)
        {
            try
            {
                if (!File.Exists(target.SubdomainlistFile))
                {
                    LogError($"Error updating the target! File with subdomain listing doesn't exist: '{target.SubdomainlistFile}'");
                    return new List<string>();
                }

                var fileContents = File.ReadAllText(target.SubdomainlistFile);
                var subdomains = JsonConvert.DeserializeObject<List<string>>(fileContents);
                if (subdomains is null)
                {
                    LogError($"Error updating the target! File with subdomain listing cannot be read: '{target.SubdomainlistFile}'");
                    return new List<string>();
                }
                if (!subdomains.Any())
                {
                    LogError($"Error updating the target! File with subdomain listing is empty: '{target.SubdomainlistFile}'");
                    return new List<string>();
                }
                return subdomains;
            }
            catch (Exception ex)
            {
                LogError($"Error updating the target! Cannot read the File: '{target.SubdomainlistFile}'. More info: {ex}");
                return new List<string>();
            }
        }

        private static void UpdateSimpleTarget(Target target, IPAdresses addresses, string currentSubdomain)
        {
            AddLocalIpv6Part(addresses);
            
            (var host, var request) = BuildRequest(target, addresses, currentSubdomain);
            SendRequest(target, host, request);
        }

        private static void AddLocalIpv6Part(IPAdresses addresses)
        {
            //    // Von der Fritzbox Verwendete IPv6 Präfixe:
            //    //Heimnetz	: 2a04:4540:ac01:7801::
            //    //Gastnetz	: 2a04:4540:ac01:7802::
            //    //WAN		: 2a04:4540:ac01:7800::
            //    // die ersten 3 Gruppen werden vom Provider zugeteilt
            //    // Wenn wir einen externen Dienst abfragen, sagt der uns 7801, 
            //    // wir müssen aber 7800 als öffentliche IPv6-Adresse nehmen.
            //    // wir haben ein /56 Präfix, also sind die letzten 8 Bits fest von der Fritzbox gesetzt.
            //    var ipv6_2 = addresses.IPv6;
            //    if (ipv6_2[18] != '0')
            //    {
            //    	ipv6_2 = ipv6_2.Remove(18, 1).Insert(18, "0");
            //    	LogInfo($"changed ipv6      to: {ipv6_2}");
            //    }

            if (!string.IsNullOrWhiteSpace(_config.LocalIPV6Address))
            {
                addresses.IPv6inclLocalIP = addresses.IPv6 + _config.LocalIPV6Address;
            }
        }

        private static (string,string) BuildRequest(Target target, IPAdresses addresses, string currentSubdomain)
        {
            var request = target.Request
                .Replace("{ipv4}"            , addresses.IPv4)
                .Replace("{ipv6}"            , addresses.IPv6)
                .Replace("{IPv6inclLocalIP}" , addresses.IPv6inclLocalIP)
                .Replace("{Subdomainlist}"   , currentSubdomain)
                .Replace("{DynDnsPassword}"  , target.DynDnsPassword)
                .Replace("{DynDnsUsername}"  , target.DynDnsUsername)
                .Replace("{LocalIPV6Address}", _config.LocalIPV6Address);
            
            var host = target.Host
                .Replace("{ipv4}"            , addresses.IPv4)
                .Replace("{ipv6}"            , addresses.IPv6)
                .Replace("{IPv6inclLocalIP}" , addresses.IPv6inclLocalIP)
                .Replace("{Subdomainlist}"   , currentSubdomain)
                .Replace("{DynDnsPassword}"  , target.DynDnsPassword)
                .Replace("{DynDnsUsername}"  , target.DynDnsUsername)
                .Replace("{LocalIPV6Address}", _config.LocalIPV6Address);
            
            return (host,request);
        }

        private static void SendRequest(Target target, string host, string request)
        {
            string result = "";

            if (target.Method == "GET_WITH_CURL")
            {
                result = SendGetWithCurl(host, request);
            }
            if (target.Method == "GET")
            {
                result = SendGet(target, host, request);
            }
            else if (target.Method == "POST")
            {
                result = SendPost(target, host, request);
            }

            var targetDesc = $"{target.Host}{request}";
            LogDebug($"    ---- Updated target '{target.Name,-15}': {targetDesc,-130}: {result.Replace("\r", "").Replace("\n", " ")}");
        }
        #endregion
        
        #region ------------- HTTP client ---------------------------------------------------------
        private static string SendGetWithCurl(string host, string request)
        {
            try
            {
                var starter = new ProcessStarter();
                var result = starter.CallProcessAndReturnConsoleOutput("curl", $"--silent \"{host+request}\"", _config.TimeoutSeconds);

                result = result.TrimEnd(new char[] { '\n', '\r' });
                if (result.StartsWith("good"))
                {
                    return $"Target updated successfully";
                }
                else
                {
                    if (result.StartsWith("nochg"))
                        return $"No change: {result}";
                }
                return $"Error updating the target: {result.Replace("\n", " ")}";
            }
            catch (Exception ex)
            {
                return $"Error updating the target: {ex}";
            }
        }

        private static string SendGet(Target target, string host, string request)
        {
			try
			{
				var client = new RestClient(host);
                var requestObj = new RestRequest(request);

				requestObj.AddHeader("Content-Type", "application/json");
                if (target.Authentication == "Basic")
                    client.Authenticator = new HttpBasicAuthenticator(target.Username, target.Password);

                var response = client.Get(requestObj);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return $"Target updated successfully, Message: {response.Content.Replace("\n", " ")}";
                }
                return $"Error updating the target: Code {response.StatusCode}, {response.Content.Replace("\n", " ")}";
			}
			catch (Exception ex)
			{
				return $"Error updating the target: {ex}";
			}
        }

        private static string SendPost(Target target, string host, string request)
        {
			try
			{
				var client = new RestClient(host);
                var requestObj = new RestRequest(request);

				requestObj.AddHeader("Content-Type", "application/json");

                if (target.Authentication == "Basic")
                    client.Authenticator = new HttpBasicAuthenticator(target.Username, target.Password);

                var response = client.Post(requestObj);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return $"Target updated successfully, Message: {response.Content.Replace("\n", " ")}";
                }
                return $"Error updating the target: Code {response.StatusCode}, {response.Content.Replace("\n", " ")}";
			}
			catch (Exception ex)
			{
				return $"Error updating the target: {ex}";
			}
		}

        private static string CreateBasicAuthenticationHeader(string authUsername, string authPassword)
        {
            var authValue = $"{authUsername}:{authPassword}";
            return "Basic" + " " + Convert.ToBase64String(Encoding.UTF8.GetBytes(authValue));
        }
        #endregion

        #region ------------- Inter Process Communication -----------------------------------------
        private static void UpdateIPCFiles(IPAdresses addresses)
        {
            if (_config.InterProcessCommunication is null || string.IsNullOrWhiteSpace(_config.InterProcessCommunication.CurrentIpAddressesFile))
                return;

            for (int retry=0; retry < 10; retry++) // retry pattern, retry 9 times on error, wait 1 second in between
            {
                if (UpdateIPCFiles_internal(addresses))
                    return;
                Thread.Sleep(1000);
            }
        }

        private static bool UpdateIPCFiles_internal(IPAdresses addresses)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_config.InterProcessCommunication.CurrentIpAddressesFile))
                {
                    File.WriteAllText(_config.InterProcessCommunication.CurrentIpAddressesFile, $"{addresses.IPv4} {addresses.IPv6}{_config.LocalIPV6Address}");
                    LogDebug($"Updated CurrentIpAddressesFile");
                }

                if (!string.IsNullOrWhiteSpace(_config.InterProcessCommunication.CurrentIpAddressesChangedFile))
                {
                    File.WriteAllText(_config.InterProcessCommunication.CurrentIpAddressesChangedFile, $"External IP address has changed");
                    LogDebug($"Updated CurrentIpAddressesChangedFile");
                }
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Error updating CurrentIpAddressesFiles. Exception: {ex}");
                return false;
            }
        }
        #endregion

        #region ------------- Obtaining own IP address from external websites ---------------------
        private static async Task<IPAdresses> GetCurrentAddresses()
        {
            try
            {
                return await GetExternalIPAddressFromFritzbox();
            }
            catch (Exception)
            {
                var message = "Cannot get current ip address from Fritzbox, ";

                var ipv4 = GetExternalIPv4AddressFromExternalService();
                if (string.IsNullOrEmpty(ipv4))
                    message += $"ciridata.eu query also failed.";
                else
                    message += $"but the second target 'ciridata.eu' did work, IP is {ipv4}.";

                var ipv6 = GetExternalIPv6AddressFromExternalService();

                LogError(message);
                NotifyUser(message);
                return new IPAdresses(ipv4, _old_addresses.IPv6);
            }
        }

        private static string GetExternalIPv4AddressFromExternalService()
        {
			try
			{
				var client = new RestClient("https://www.ciridata.eu");
                var requestObj = new RestRequest("/api/dyndns/");
				requestObj.AddHeader("Content-Type", "application/json");
                var response = client.Get(requestObj);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var ip = response.Content.Trim('"');
                    
                    if (ip.StartsWith("IpV4 ") && ip.Length > "IpV4 ".Length)
                        ip = ip.Substring("IpV4 ".Length);

                    LogDebug($"External IPv4 address from ciridata.eu: {ip}");
                    return ip;
                }

                LogDebug($"Invalid answer: HTTP status code {response.StatusCode}, {response.ErrorMessage}");
                return "";
            }
            catch (Exception ex)
            {
                LogError($"Error getting the external IPv4 adress: {ex.ToString()}");
                return "";
            }
        }

        private static string GetExternalIPv6AddressFromExternalService()
        {
            try
            {
				var client = new RestClient("http://ip6only.me");
                var requestObj = new RestRequest("/");
				requestObj.AddHeader("Content-Type", "application/json");
                var response = client.Get(requestObj);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var html = response.Content.Trim('"');
                    var pattern = @"(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))";
                    var regex = new Regex(pattern);
                    var match = regex.Match(html);
                    var ip = match.ValueSpan.ToString();

                    LogDebug($"External IPv6 address: {ip}");
                    return ip;
                }

                LogDebug($"Invalid answer: HTTP status code {response.StatusCode}, {response.ErrorMessage}");
                return "";
            }
            catch (Exception ex)
            {
                LogError($"Error getting the external IPv6 adress: {ex}");
                return "";
            }
        }
        #endregion

        #region ------------- Obtaining own IP addresses from Fritzbox (TR064) --------------------
        private static async Task SearchForFritzboxes()
        {
            LogDebug("");
            LogDebug("Searching for Fritzboxes...");
            IEnumerable<FritzDevice> devices = await FritzDevice.LocateDevicesAsync();

            if (devices.Count() > 0)
            {
                LogDebug($"Found {devices.Count()} devices.");
                int counter = 1;
                foreach (FritzDevice device in devices)
                    LogDebug($"{counter++} - {device.ModelName}");

                LogDebug($"Selecting device {_config.Sources[0].FritzboxDeviceNumber}");
            }
            else
            {
                _config.Sources[0].FritzboxDeviceNumber = 1;
            }

            _device = devices.Skip(_config.Sources[0].FritzboxDeviceNumber - 1).First();

            LogDebug($"Device ID : {_device.UDN}");
            LogDebug($"Model name: {_device.ModelName}");
        }

        private static async Task SetupFritzboxConnection()
        {
            _settings = new ConnectionSettings();
            _settings.UserName = _config.Sources[0].FritzboxUsername;
            _settings.Password = _config.Sources[0].FritzboxPassword;

            _client1 = await _device.GetServiceClient<WANPPPConnectionClient>(_settings);
            _client2 = await _device.GetServiceClient<InternetGatewayDeviceV2>(_settings);
        }

        private static async Task<IPAdresses> GetExternalIPAddressFromFritzbox()
        {
            var lastException = "";

            // retry pattern (without polly lib)
            int maxRetries = 3;
            for(int retry=0; retry <= maxRetries; retry++)
            {
                try
                {
                    var ipaddress = await TryGetExternalIPAddressFromFritzbox();
                    if (retry > 0)
                        LogDebug("finally communication was ok.");
                    return ipaddress;
                }
                catch (Exception ex)
                {
                    lastException = ex.ToString();
                    LogDebug("could not communicate with fritzbox. reinitializing...");
                    await SearchForFritzboxes();
                    await SetupFritzboxConnection();
                }
                LogDebug($"Retry No.{retry} of {maxRetries}");
            }
            LogDebug($"last Exception with fritzbox: {lastException}");
            throw new Exception("unable to communicate with fritzbox");
        }

        private static async Task<IPAdresses> TryGetExternalIPAddressFromFritzbox()
        {
            var ipv4 = await _client1.GetExternalIPAddressAsync();
            var ipv6 = await _client2.GetIPv6Prefix();
            ipv6 = ipv6.TrimEnd(':'); // remove the ::
            return new IPAdresses(ipv4,ipv6);
        }
        #endregion

        #region ------------- Logging -------------------------------------------------------------
        private static void InitLogger()
        {
            try
            {
                var filename = "";
                if (!string.IsNullOrWhiteSpace(_commandLineOptions.LogsettingsFilename) && 
                    File.Exists(_commandLineOptions.LogsettingsFilename))
                {
                    filename = _commandLineOptions.LogsettingsFilename;
                    Console.WriteLine($"using filename '{filename}' for NLOG settings given via command line option");
                }
                else
                {
                    filename = "nlog.config";
                    Console.WriteLine($"using standard filename '{filename}' for NLOGsettings");
                }
                
                _logger = NLogBuilder.ConfigureNLog(filename).GetCurrentClassLogger();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error initializing NLOG logger: {ex.ToString()}");
                throw new Exception("Program ended");
            }
        }

        private static void LogError(string message)
        {
            _logger.Log(NLog.LogLevel.Error, message);
        }

        private static void LogWarn(string message)
        {
            _logger.Log(NLog.LogLevel.Warn, message);
        }

        private static void LogInfo(string message)
        {
            _logger.Log(NLog.LogLevel.Info, message);
        }

        private static void LogDebug(string message)
        {
            _logger.Log(NLog.LogLevel.Debug, message);
        }
        #endregion

        #region ------------- Configuration -------------------------------------------------------
        private static void ReadConfiguration()
        {
            try
            {
                var filename = "";
                if (!string.IsNullOrWhiteSpace(_commandLineOptions.AppsettingsFilename) && 
                    File.Exists(_commandLineOptions.AppsettingsFilename))
                {
                    filename = _commandLineOptions.AppsettingsFilename;
                    Console.WriteLine($"using filename '{filename}' for appsettings given via command line option");
                }
                else
                {
                    filename = "appsettings.hjson";
                    Console.WriteLine($"using standard filename '{filename}' for appsettings");
                }

                _configurationManager = new ProgramSettingsManager<Configuration>()
                    .UseFilename(filename)
                    .Load();
                _config = _configurationManager.Data;

                if (_config == null)
                {
                    Console.WriteLine($"No valid configuration found!\nExpecting file '{filename}'");
                    throw new Exception("Program ended");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem reading the configuration data.\n" + ex.ToString());
                throw new Exception("Program ended");
            }
        }

        private static void PrintConfiguration()
        {
            LogDebug($"Configuration:");
            LogDebug($"LocalIPV6Address                 : {_config.LocalIPV6Address                 }");
            LogDebug($"TimeoutSeconds                   : {_config.TimeoutSeconds                   }");
            LogDebug($"WaitTimeSeconds                  : {_config.WaitTimeSeconds                  }");
            LogDebug($"LogUnchangedAddress              : {_config.LogUnchangedAddress              }");
            LogDebug($"Sources:");                                                                  
            foreach(var source in _config.Sources)                                                  
            {                                                                                       
                LogDebug($"    Type                         : {source.Type                          }");
                LogDebug($"    FritzboxUsername             : {source.FritzboxUsername              }");
                LogDebug($"    FritzboxPassword             : *************************              ");
                LogDebug($"    FritzboxDeviceNumber         : {source.FritzboxDeviceNumber          }");
            }
            LogDebug($"Targets:");                                                                  
            foreach(var target in _config.Targets)                                                  
            {                                                                                       
                LogDebug($"    Name                         : {target.Name                          }");
                LogDebug($"    Method                       : {target.Method                        }");
                LogDebug($"    Request                      : {target.Request                       }");
                LogDebug($"    Authentication               : {target.Authentication                }");
                LogDebug($"    Username                     : {target.Username                      }");
                LogDebug($"    Password                     : *************************              ");
                LogDebug($"    SubdomainlistFile            : {target.SubdomainlistFile             }");
            }
            LogDebug($"Notifications:");
            LogDebug($"    Method                       : {_config.Notifications.Method             }");
            LogDebug($"    PushbulletApiKey             : *************************                  ");
            LogDebug($"    PushbulletDevice             : {_config.Notifications.PushbulletDevice   }");
            LogDebug($"    Subject                      : {_config.Notifications.Subject            }");
            LogDebug($"Inter process communication:");
            LogDebug($"    CurrentIpAddressesFile       : {_config.InterProcessCommunication.CurrentIpAddressesFile       }");
            LogDebug($"    CurrentIpAddressesChangedFile: {_config.InterProcessCommunication.CurrentIpAddressesChangedFile}");
        }
        #endregion

        #region ------------- Notifications -------------------------------------------------------
        private static void NotifyUser(string message)
        {
            if (_config.Notifications.Method == "Pushbullet")
                SendPushbulletMessage1TimePerHour(message);
        }
        #endregion

        #region ------------- Pushbullet messaging ------------------------------------------------
        private static void SendPushbulletMessage1TimePerHour(string message)
        {
            if (DateTime.Now > _lastEmailSent.AddHours(1))
            {
                _lastEmailSent = DateTime.Now;
                SendPushbulletMessage(message);
            }
        }

        private static void SendPushbulletMessage(string message)
        {
            string apiKey = _config.Notifications.PushbulletApiKey;
            string device = _config.Notifications.PushbulletDevice;
            SendPushbulletApiCommand(apiKey, device, _config.Notifications.Subject, message);
        }

        private static void SendPushbulletApiCommand(string apiKey, string device, string title, string body)
        {
            string host    = "https://api.pushbullet.com";
            string request = "/v2/pushes";

            string HttpBinaryData = "";
            HttpBinaryData += $"\\\"type\\\": \\\"note\\\", ";
            HttpBinaryData += $"\\\"title\\\": \\\"{title}\\\", ";
            HttpBinaryData += $"\\\"body\\\": \\\"{body}\\\", ";
            HttpBinaryData += $"\\\"device_iden\\\":\\\"{device}\\\"";


			try
			{
				var client = new RestClient(host);
                var requestObj = new RestRequest(request);

				requestObj.AddHeader("Content-Type", "application/json");
                client.Authenticator = new HttpBasicAuthenticator(apiKey, "");

                var response = client.Post(requestObj);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    LogDebug($"Message sent to pushbullet device");
                    return;
                }
                LogError($"Error in pushbullet communication: Code {response.StatusCode}, {response.Content.Replace("\n", " ")}");
			}
			catch (Exception ex)
			{
				LogError($"Error in pushbullet communication: {ex}");
			}



            //string URL = "https://api.pushbullet.com/v2/pushes";

            //string HttpBinaryData = "";
            //HttpBinaryData += $"\\\"type\\\": \\\"note\\\", ";
            //HttpBinaryData += $"\\\"title\\\": \\\"{title}\\\", ";
            //HttpBinaryData += $"\\\"body\\\": \\\"{body}\\\", ";
            //HttpBinaryData += $"\\\"device_iden\\\":\\\"{device}\\\"";

            //// Build HTTP POST command and execute it with curl
            //string dosCommand = "curl";
            //string arguments = "";
            //arguments += $"-u {apiKey}: -X POST {URL} ";
            //arguments += $"--header \"Content-Type: application/json\" ";
            //arguments += $"--data-binary \"{{{HttpBinaryData}}}\"";

            //string output = "";
            //try
            //{
            //    output = CallProcessAndReturnConsoleOutput(dosCommand, arguments);
            //}
            //catch (Exception ex)
            //{
            //    LogError($"Error calling dos command 'curl' to send a message to pushbullet API! \nMore info:\n{ex.ToString()}");
            //}

            ////var jsonOutput = System.Text.Json.JsonSerializer.Deserialize<dynamic>(output);
            //if (output.Contains("error"))
            //    LogError($"Error calling dos command 'curl' to send a message to pushbullet API! invalid access token!");
        }

        private static int _WaitTimeoutInSeconds = 30;

        private static string CallProcessAndReturnConsoleOutput(string filename, string arguments)
        {
            string Output = "";
            using (Process p = new Process())
            {
                p.StartInfo.FileName = filename;
                p.StartInfo.Arguments = arguments;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.Start();

                int MaxLineCount = 1000;
                DateTime Timeout = DateTime.Now.AddSeconds(_WaitTimeoutInSeconds);
                while (!p.StandardOutput.EndOfStream)
                {
                    Output += p.StandardOutput.ReadLine();
                    MaxLineCount--;
                    if (MaxLineCount <= 0 || DateTime.Now > Timeout)
                        break; // prevent from looping endless
                }
                if (DateTime.Now > Timeout)
                {
                    p.Kill();
                    throw new Exception($"Error, possible endless loop! killing the subprocess after {_WaitTimeoutInSeconds} seconds");
                }
                if (MaxLineCount <= 0)
                {
                    p.Kill();
                    throw new Exception("Error, possible endless loop! killing the subprocess after reading 1000 lines");
                }

                bool ProcessHasExited = p.WaitForExit(_WaitTimeoutInSeconds * 1000); // at max 5 seconds!
                if (!ProcessHasExited)
                    throw new Exception($"Error in Method CallProcessAndReturnConsoleOutput! Process hasn't exited after {_WaitTimeoutInSeconds} seconds!");
            }

            return Output;
        }
        #endregion
        #endregion
    }
}
