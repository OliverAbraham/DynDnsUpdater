# DynDNS Updater

![](https://img.shields.io/github/downloads/oliverabraham/DynDNSUpdater/total) ![](https://img.shields.io/github/license/oliverabraham/DynDNSUpdater) ![](https://img.shields.io/github/languages/count/oliverabraham/DynDNSUpdater) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham/DynDNSUpdater?label=repo%20stars) ![GitHub Repo stars](https://img.shields.io/github/stars/oliverabraham?label=user%20stars)



DynDNS updater updates multiple external DNS services using a TR064 Fritzbox client 
and REST client, with build-in NLOG logging, pushbullet notifications and IPV6 capability.

I needed a tool to update several external DNS servers, to reflect my current external IP address.
And I have a Fritzbox router.
I wanted a solution that queries firstly my fritzbox for the external IP address, rather than 
reaching out to external services.

I've build an application that queries firstly the local Fritzbox, and secondly trying external 
"WhatsMyIP" services when the Fritzbox cannot be reached.

I wrote a second tool in my homeautomation setup that resets the Fritzbox and Fiber router 
using a line switch. Maybe I'll re-implement it into this project some time. 
(I would then use an MQTT client to drive a shelly plug)



## Features
- configurable using an appsettings file (JSON format)
- built-in logging using NLOG
- IpV6 capable
- Can send notifications to your pushbullet app on the mobile.
- You can set appsettings and nlog parameters location by command line options



## About DynDNS Updater

The app uses the FritzBox TR064 interface to find a fritzbox 
and query its external IP addresses(IPv4 and IPv6).
The complete behaviour is controlled by appsettings.hjson. 


## How to use
- Build the project or download a release
- Configure appsettings.json with a text editor.
- Start the app


## Configuration
This section describes the parameters needed to setup.

- **LocalIPV6Address**: 
This is needed for IPV6 update. Enter the network part of the address. 
The node part of your ip address will be taken from your router.
 
- **TimeoutSeconds**:
Applied when using curl to query external services.

- **WaitTimeSeconds**: 
Sets the query period.

- **Sources**:
Add your fritzbox credentials You can add a separate user account in your Fritzbox.
As of now, only Fritzboxes are supported. Other routers using TR064 are not tested.

- **Targets**: Setup the DNS servers you want to update.
You can use the variables in curly braces to send the appropriate values.
Available ms are GET, GET_WITH_CURL ad POST.
Currently, Strato and Avernis are supported.

- **Notifications**:
The app can send pushbullet notifications to you mobile. You have to sign up for the pushbullet
service to use this.

- **InterProcessCommunication**:
The app stores the actual IP addresses into a text file, if you need it.
Whenever an update occurs, it creates an indicator file with 0 size.
An external program can monitor this indicator, read the new IPs and delete the indicator file.
  

## License
Licensed under Apache licence.
https://www.apache.org/licenses/LICENSE-2.0


## Compatibility
The application was build with DotNET 6.


## Source code
The source code is hosted at:
https://github.com/OliverAbraham/DynDNSUpdater


## Author
- Oliver Abraham
- EMail: mail@oliver-abraham.de
- Web:  https://www.oliver-abraham.de
- Linkedin: https://www.linkedin.com/in/oliver-abraham-5345941ab/

Please feel free to comment and suggest improvements!


# MAKE A DONATION !

If you find this application useful, buy me a coffee!
I would appreciate a small donation on https://www.buymeacoffee.com/oliverabraham

<a href="https://www.buymeacoffee.com/app/oliverabraham" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/v2/default-yellow.png" alt="Buy Me A Coffee" style="height: 60px !important;width: 217px !important;" ></a>

