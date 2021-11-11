using System;
using System.Collections.Generic;
using System.Linq;

namespace WindowsLibrary;

public class HostsFileEntry
{
    public string Address { get; set; }
    public List<string> Hosts { get; set; }

    public HostsFileEntry(string address, List<string> hostEntry)
    {
        if (address == null || ValidateIPv4(address) == false)
        {
            throw new ArgumentException($"Invalid IPv4 address [{address}]");
        }

        if (hostEntry == null ||
            hostEntry.Count == 0 ||
            hostEntry.Any(e => e == null || string.IsNullOrWhiteSpace(e)))
        {
            throw new ArgumentException($"Invalid host entry [{ string.Join(",", hostEntry ?? new List<string> { "null" }) }]");
        }

        Address = address;
        Hosts = hostEntry;
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        var item = obj as HostsFileEntry;

        if (Address.Equals(item.Address) == false)
        {
            return false;
        }

        if (Hosts.SequenceEqual(item.Hosts) == false)
        {
            return false;
        }

        return true;
    }

    private bool ValidateIPv4(string ipString)
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

        byte tempForParsing;
        return splitValues.All(r => byte.TryParse(r, out tempForParsing));
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public override string ToString()
    {
        return string.Format("{0} = {1}", Address, string.Join(",", Hosts));
    }
}
