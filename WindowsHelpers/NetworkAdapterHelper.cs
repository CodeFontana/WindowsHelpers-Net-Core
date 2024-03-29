﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Management;

namespace WindowsLibrary;

public class NetworkAdapterHelper
{
    private readonly ILogger<NetworkAdapterHelper> _logger;
    public List<NetworkAdapter> adapterList;

    public NetworkAdapterHelper(ILogger<NetworkAdapterHelper> logger)
    {
        _logger = logger;
        QueryNetworkAdapters();
    }

    public Tuple<bool, NetworkAdapter> In(List<NetworkAdapter> adapterList)
    {
        Tuple<bool, NetworkAdapter> returnTuple = new(false, null);

        foreach (NetworkAdapter nic in adapterList)
        {
            if (Equals(nic))
            {
                returnTuple = new Tuple<bool, NetworkAdapter>(true, nic);
                break;
            }
        }

        return returnTuple;
    }

    public List<NetworkAdapter> QueryNetworkAdapters()
    {
        adapterList = new List<NetworkAdapter>();
        ManagementObjectSearcher adapterQuery = new("SELECT NetConnectionId,Index,Name,NetEnabled,NetConnectionStatus FROM Win32_NetworkAdapter WHERE NetConnectionId != NULL");

        foreach (ManagementObject adapterResult in adapterQuery.Get())
        {
            var netConnectionId = adapterResult["NetConnectionId"];

            if (netConnectionId != null && netConnectionId.ToString().Equals("") == false)
            {
                int adapterIndex = int.Parse(adapterResult["Index"].ToString());
                string adapterName = adapterResult["Name"].ToString();
                bool adapterEnabled = bool.Parse(adapterResult["NetEnabled"].ToString());
                int adapterStatus = int.Parse(adapterResult["NetConnectionStatus"].ToString());
                NetworkAdapter newAdapter = new(adapterIndex, adapterName, adapterEnabled, adapterStatus);
                ManagementObjectSearcher configQuery = new(
                    $"SELECT DHCPEnabled,IPAddress,IPSubnet,DefaultIPGateway FROM Win32_NetworkAdapterConfiguration WHERE Index = {newAdapter.AdapterIndex}");

                foreach (ManagementObject configResult in configQuery.Get())
                {
                    try
                    {
                        var rawIsDHCPEnabled = configResult["DHCPEnabled"];
                        var rawCurrentIPAddr = configResult["IPAddress"];
                        var rawCurrentSubnet = configResult["IPSubnet"];
                        var rawCurrentGatewayAddr = configResult["DefaultIPGateway"];

                        if (bool.TryParse(rawIsDHCPEnabled.ToString(), out bool isEnabled) == false)
                        {
                            newAdapter.IsDHCPEnabled = true;
                        }
                        else
                        {
                            newAdapter.IsDHCPEnabled = isEnabled;
                        }

                        if (rawCurrentIPAddr != null)
                        {
                            newAdapter.IPAddress = ((string[])rawCurrentIPAddr)[0].ToString();
                        }
                        else
                        {
                            newAdapter.IPAddress = "<Not configured>";
                        }

                        if (rawCurrentSubnet != null)
                        {
                            newAdapter.SubnetMask = ((string[])rawCurrentSubnet)[0].ToString();
                        }
                        else
                        {
                            newAdapter.SubnetMask = "<Not configured>";
                        }

                        if (rawCurrentGatewayAddr != null)
                        {
                            newAdapter.DefaultGateway = ((string[])rawCurrentGatewayAddr)[0].ToString();
                        }
                        else
                        {
                            newAdapter.DefaultGateway = "<Not configured>";
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, $"Failed to query adapter current configuration for [{ newAdapter.AdapterName }]");
                    }
                }

                adapterList.Add(newAdapter);
            }
        }

        return adapterList;
    }
}
