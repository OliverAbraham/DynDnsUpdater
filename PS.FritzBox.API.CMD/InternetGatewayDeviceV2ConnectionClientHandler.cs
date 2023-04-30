using PS.FritzBox.API.WANDevice.WANConnectionDevice;
using System;
using System.Net;
using System.Threading.Tasks;

namespace PS.FritzBox.API.CMD
{
    public class InternetGatewayDeviceV2ConnectionClientHandler : ClientHandler
    {
        InternetGatewayDeviceV2 _client;

        public InternetGatewayDeviceV2ConnectionClientHandler(FritzDevice device, Action<string> printOutput, Func<string> getInput, Action wait, Action clearOutput) : base(device, printOutput, getInput, wait, clearOutput)
        {
            _client = device.GetServiceClient<InternetGatewayDeviceV2>();
        }

        public override async Task Handle()
        {
            string input = string.Empty;

            do
            {
                this.ClearOutputAction();
                this.PrintOutputAction($"InternetGatewayDeviceV2ConnectionClient{Environment.NewLine}########################");
                this.PrintOutputAction("1 - GetExternalIPv6Address");
                this.PrintOutputAction("2 - X_AVM_DE_GetIPv6Prefix");
                this.PrintOutputAction("r - Return");

                input = this.GetInputFunc();

                try
                {
                    switch (input)
                    {
                        case "1":
                            await this.GetExternalIPAddress();
                            break;
                        case "2":
                            await this.GetIPv6Prefix();
                            break;
                        default:
                            this.PrintOutputAction("invalid choice");
                            break;
                    }

                    if (input != "r")
                        this.WaitAction();
                }
                catch (Exception ex)
                {
                    this.PrintOutputAction(ex.ToString());
                    this.WaitAction();
                }

            } while (input != "r");
        }

        private async Task GetExternalIPAddress()
        {
            this.ClearOutputAction();
            this.PrintEntry();
            var ip = await this._client.GetExternalIPAddressAsync();
            this.PrintOutputAction($"external IPv6 Address: {ip}");
        }

        private async Task GetIPv6Prefix()
        {
            this.ClearOutputAction();
            this.PrintEntry();
            var ip = await this._client.GetIPv6Prefix();
            this.PrintOutputAction($"external IPv6Prefix: {ip}");
        }
    }
}