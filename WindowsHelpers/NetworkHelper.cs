using LoggerLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WindowsLibrary;

public class NetworkHelper
{
    private readonly IFileLogger _logger;

    public NetworkHelper(IFileLogger logger)
    {
        _logger = logger.CreateFileLogger(GetType().Name);
    }

    public IPAddress GetCurrentIpAddress()
    {
        try
        {
            IPAddress[] ip = Dns.GetHostAddresses(Dns.GetHostName());

            if (ip.Length > 0)
            {
                return ip.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            }
            else
            {
                return null;
            }
        }
        catch (Exception e)
        {
            _logger.Log(e, "Failed to resolve current IP address");
            return null;
        }
    }

    public IPAddress GetSubnetMask(IPAddress inputAddress)
    {
        if (inputAddress == null)
        {
            throw new ArgumentException("IP address cannot be null");
        }

        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
            .First(i => i.GetIPProperties().UnicastAddresses
                .Any(x => x.Address.Equals(inputAddress)))
            .GetIPProperties()
            .UnicastAddresses
            .Where(a => a.Address.Equals(inputAddress))
            .FirstOrDefault()?
            .IPv4Mask;
        }
        catch (Exception e)
        {
            _logger.Log(e, "Failed to resolve subnet mask for specified IP address");
            return null;
        }
    }

    public IPAddress GetDefaultGateway(IPAddress inputAddress)
    {
        if (inputAddress == null)
        {
            throw new ArgumentException("IP address cannot be null");
        }

        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
            .First(i => i.GetIPProperties().UnicastAddresses
                .Any(x => x.Address.Equals(inputAddress)))
            .GetIPProperties()
            .GatewayAddresses
            .FirstOrDefault()?
            .Address;
        }
        catch (Exception e)
        {
            _logger.Log(e, "Failed to resolve default gateway for specified IP address");
            return null;
        }
    }

    public Tuple<bool, List<string>> ResolveHostToIP(string hostAddress, bool hideOutput = false)
    {
        try
        {
            if (Uri.CheckHostName(hostAddress).Equals(UriHostNameType.IPv4))
            {
                if (hideOutput == false)
                {
                    _logger.Log(hostAddress + " resolved to: " + hostAddress);
                }

                return new Tuple<bool, List<string>>(true, new List<string> { hostAddress });
            }

            bool successFlag = false;
            List<string> addresses = new();
            IPHostEntry hostEntry = Dns.GetHostEntry(hostAddress);

            if (hostEntry.AddressList.Length > 0)
            {
                successFlag = true;

                foreach (IPAddress addr in hostEntry.AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(addr.ToString());

                        if (hideOutput == false)
                        {
                            _logger.Log(hostAddress + " resolved to: " + addr.ToString());
                        }
                    }
                }
            }

            return new Tuple<bool, List<string>>(successFlag, addresses);
        }
        catch (SocketException)
        {
            if (hideOutput == false)
            {
                _logger.Log("Unable to resolve: " + hostAddress);
            }

            return new Tuple<bool, List<string>>(false, null);
        }
        catch (Exception e)
        {
            if (hideOutput == false)
            {
                _logger.Log(e, "Address resolution failure");
            }

            return new Tuple<bool, List<string>>(false, null);
        }
    }

    public string ResolveIPtoHost(string inputAddress, bool hideOutput = false)
    {
        try
        {
            if (Uri.CheckHostName(inputAddress).Equals(UriHostNameType.Dns))
            {
                if (hideOutput == false)
                {
                    _logger.Log(inputAddress + " reversed to: " + inputAddress);
                }

                return inputAddress;
            }

            IPHostEntry HostEntry = Dns.GetHostEntry(IPAddress.Parse(inputAddress));

            if (HostEntry != null)
            {
                if (hideOutput == false)
                {
                    _logger.Log(inputAddress + " reversed to: " + HostEntry.HostName);
                }

                return HostEntry.HostName;
            }
            else
            {
                return null;
            }
        }
        catch (SocketException)
        {
            if (hideOutput == false)
            {
                _logger.Log("Unable to reverse [" + inputAddress + "] to hostname");
            }

            return null;
        }
        catch (Exception e)
        {
            if (hideOutput == false)
            {
                _logger.Log(e, "Reverse name lookup exception");
            }

            return null;
        }
    }

    public bool ValidateIPv4(string ipString)
    {
        if (string.IsNullOrWhiteSpace(ipString))
        {
            return false;
        }

        string[] splitValues = ipString.Split('.');

        if (splitValues.Length != 4)
        {
            return false;
        }

        // Return true, only if each octet can be successfully parsed as an 8-bit unsigned integer (byte).
        byte tempForParsing;
        return splitValues.All(r => byte.TryParse(r, out tempForParsing));
    }

    public bool TestURL(string url, TimeSpan timeout)
    {
        _logger.Log("Test URL: " + url + " [Timeout=" + timeout.TotalSeconds + "s]");

        try
        {
            HttpClient client = new();
            Task<HttpResponseMessage> result = client.GetAsync(url);
            result.Wait(timeout);

            if (result.Status == TaskStatus.RanToCompletion)
            {
                _logger.Log("HTTP Response: " + result.Result.StatusCode.ToString());

                if (result.Result.StatusCode == HttpStatusCode.OK)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                _logger.Log("HTTP Response: TIMEOUT ERROR [" + result.Status.ToString() + "]");
                return false;
            }
        }
        catch (Exception e)
        {
            _logger.Log(e, "Test connection failed to [" + url + "]");
            return false;
        }
    }
}
