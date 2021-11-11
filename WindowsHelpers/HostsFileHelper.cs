using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WindowsLibrary;

public class HostsFileHelper
{
    private string _hostsFile;

    public HostsFileHelper()
    {
        string systemPath = Environment.GetEnvironmentVariable("SystemRoot");
        _hostsFile = Path.Combine(systemPath, @"system32\drivers\etc\hosts");

        if (File.Exists(_hostsFile) == false)
        {
            throw new FileNotFoundException($"Hosts file not found [{_hostsFile}]");
        }
    }

    public void CreateEntry(string address, string hostsEntry)
    {
        if (address == null || ValidateIPv4(address) == false)
        {
            throw new ArgumentException($"Invalid IPv4 address [{address ?? "<Null>"}]");
        }

        if (string.IsNullOrWhiteSpace(hostsEntry))
        {
            throw new ArgumentException($"Invalid host entry [{hostsEntry}]");
        }

        if (IsAddressMapped(address))
        {
            throw new ArgumentException(
                $"Hosts entry already exists for [{address}], use UpdateEntry() method to update the record");
        }

        if (IsHostMapped(hostsEntry))
        {
            throw new ArgumentException(
                $"Hosts entry already exists for [{hostsEntry}], use UpdateEntry() method to update the record");
        }

        var encoding = GetEncoding(_hostsFile);
        List<string> contents = File.ReadAllLines(_hostsFile).ToList();

        if (hostsEntry.Contains(","))
        {
            List<string> splitValues = hostsEntry.Split(',').ToList();
            contents.Add($"{address}\t\t{string.Join("\t\t", splitValues)}");
        }
        else if (hostsEntry.Contains(" ") || hostsEntry.Contains("\t"))
        {
            List<string> splitValues = hostsEntry.Split().ToList();

            for (int j = 0; j < splitValues.Count; j++)
            {
                if (string.IsNullOrWhiteSpace(splitValues[j]))
                {
                    splitValues.RemoveAt(j);
                    j -= 1;
                }
            }

            contents.Add($"{address}\t\t{string.Join("\t\t", splitValues)}");
        }
        else
        {
            contents.Add($"{address}\t\t{hostsEntry}");
        }

        File.WriteAllLines(_hostsFile, contents, encoding);
    }

    public void CreateEntry(HostsFileEntry newEntry)
    {
        if (IsAddressMapped(newEntry.Address))
        {
            throw new ArgumentException(
                $"Hosts entry already exists for [{newEntry.Address}], use UpdateEntry() method to update the record");
        }

        if (IsHostMapped(string.Join(",", newEntry.Hosts)))
        {
            throw new ArgumentException(
                $"Hosts entry already exists for [{string.Join(",", newEntry.Hosts)}], use UpdateEntry() method to update the record");
        }

        var encoding = GetEncoding(_hostsFile);
        List<string> contents = File.ReadAllLines(_hostsFile).ToList();
        contents.Add($"{newEntry.Address}\t\t{string.Join("\t\t", newEntry.Hosts)}");
        File.WriteAllLines(_hostsFile, contents, encoding);
    }

    public List<HostsFileEntry> ReadHostsFile()
    {
        List<HostsFileEntry> result = new List<HostsFileEntry>();
        string[] contents = File.ReadAllLines(_hostsFile);

        foreach (string line in contents)
        {
            if (IsEntry(line))
            {
                HostsFileEntry entry = ParseEntry(line);

                if (entry != null)
                {
                    result.Add(entry);
                }
            }
        }

        return result;
    }

    public void UpdateEntry(string address, string hostsEntry)
    {
        if (address == null || ValidateIPv4(address) == false)
        {
            throw new ArgumentException($"Invalid IPv4 address [{address ?? "<Null>"}]");
        }

        if (string.IsNullOrWhiteSpace(hostsEntry))
        {
            throw new ArgumentException($"Invalid host entry [{hostsEntry}]");
        }

        if (IsAddressMapped(address) == false && IsHostMapped(hostsEntry) == false)
        {
            throw new ArgumentException(
                $"Provided address {address} nor entry {hostsEntry} exists in hosts file, use CreateEntry() to create a new record");
        }

        var encoding = GetEncoding(_hostsFile);
        List<string> contents = File.ReadAllLines(_hostsFile).ToList();

        for (int i = 0; i < contents.Count; i++)
        {
            if (IsEntry(contents[i]))
            {
                HostsFileEntry entry = ParseEntry(contents[i]);

                if (entry == null)
                {
                    continue;
                }

                if (entry.Address.Equals(address))
                {
                    contents[i] = $"{entry.Address}\t\t{hostsEntry}";
                    break;
                }

                List<string> splitValues = null;

                if (hostsEntry.Contains(","))
                {
                    splitValues = hostsEntry.Split(',').ToList();
                }
                else if (hostsEntry.Contains(" ") || hostsEntry.Contains("\t"))
                {
                    splitValues = hostsEntry.Split().ToList();

                    for (int j = 0; j < splitValues.Count; j++)
                    {
                        if (string.IsNullOrWhiteSpace(splitValues[j]))
                        {
                            splitValues.RemoveAt(j);
                            j -= 1;
                        }
                    }
                }
                else
                {
                    splitValues = new List<string> { hostsEntry };
                }

                if (entry.Hosts.Intersect(splitValues).Any())
                {
                    contents[i] = $"{address}\t\t{string.Join("\t\t", entry.Hosts)}";
                    break;
                }
            }
        }

        File.WriteAllLines(_hostsFile, contents, encoding);
    }

    public void UpsertEntry(string address, string hostsEntry)
    {
        if (ExistsEntry(address, out _) || ExistsEntry(hostsEntry, out _))
        {
            UpdateEntry(address, hostsEntry);
        }
        else
        {
            CreateEntry(address, hostsEntry);
        }
    }

    public void DeleteEntry(string addressOrHost)
    {
        var encoding = GetEncoding(_hostsFile);
        List<string> contents = File.ReadAllLines(_hostsFile).ToList();

        for (int i = 0; i < contents.Count; i++)
        {
            if (IsEntry(contents[i]))
            {
                HostsFileEntry entry = ParseEntry(contents[i]);

                if (entry != null && (
                    entry.Address.Equals(addressOrHost) ||
                    entry.Hosts.Any(e => e.ToLower().Equals(addressOrHost.ToLower()))))
                {
                    contents.RemoveAt(i);
                    break;
                }
            }
        }

        File.WriteAllLines(_hostsFile, contents, encoding);
    }

    public bool ExistsEntry(string addressOrHost, out HostsFileEntry hostsEntry)
    {
        hostsEntry = null;
        List<string> contents = File.ReadAllLines(_hostsFile).ToList();

        for (int i = 0; i < contents.Count; i++)
        {
            if (IsEntry(contents[i]))
            {
                HostsFileEntry entry = ParseEntry(contents[i]);

                if (entry != null && (
                    entry.Address.Equals(addressOrHost) ||
                    entry.Hosts.Any(e => e.ToLower().Equals(addressOrHost.ToLower()))))
                {
                    hostsEntry = entry;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsEntry(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }
        else if (line.TrimStart().StartsWith("#"))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    private HostsFileEntry ParseEntry(string line)
    {
        List<string> splitValues = line.Split().ToList();

        for (int i = 0; i < splitValues.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(splitValues[i]))
            {
                splitValues.RemoveAt(i);
                i -= 1;
            }
        }

        if (splitValues.Count >= 2 && ValidateIPv4(splitValues[0]))
        {
            return new HostsFileEntry(splitValues[0], splitValues.Skip(1).ToList());
        }

        return null;
    }

    private bool IsAddressMapped(string address)
    {
        if (address == null || ValidateIPv4(address) == false)
        {
            throw new ArgumentException($"Invalid IPv4 address [{address}]");
        }

        if (ReadHostsFile().Any(e => e.Address.Equals(address)))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool IsHostMapped(string hostEntry)
    {
        List<string> splitValues = hostEntry.Split().ToList();

        for (int i = 0; i < splitValues.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(splitValues[i]))
            {
                splitValues.RemoveAt(i);
                i -= 1;
            }
        }

        foreach (string host in splitValues)
        {
            if (ReadHostsFile().Any(e => e.Hosts.Any(h => h.ToLower().Equals(host.ToLower()))))
            {
                return true;
            }
        }

        return false;
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

    private Encoding GetEncoding(string file)
    {
        using (StreamReader reader = new(file))
        {
            reader.Peek();
            return reader.CurrentEncoding;
        }
    }
}
