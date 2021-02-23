using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsLibrary.HostFile
{
    public class HostsFile
    {
        private static readonly HostsFile _instance = new HostsFile();
        public List<HostsFileEntry> Hosts { get; private set; } = new List<HostsFileEntry>();

        private HostsFile()
        {
            string systemPath = Environment.GetEnvironmentVariable("SystemRoot");
            string hostsFile = Path.Combine(systemPath, @"system32\drivers\etc\hosts");

            if (File.Exists(hostsFile) == false)
            {
                throw new FileNotFoundException($"Hosts file not found [{hostsFile}].");
            }

            ReadHostsFile(hostsFile);
        }

        public static HostsFile GetInstance()
        {
            return _instance;
        }

        private void ReadHostsFile(string hostsFile)
        {
            var contents = File.ReadAllLines(hostsFile);

            foreach (string line in contents)
            {
                if (IsEntry(line))
                {
                    HostsFileEntry entry = ParseEntry(line);

                    if (entry != null)
                    {
                        Hosts.Add(entry);
                    }
                }
            }
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

        public HostsFileEntry ParseEntry(string line)
        {
            string[] splitValues = line.Split();

            if (splitValues.Length >= 2 && IsValidIPv4(splitValues[0]))
            {
                return new HostsFileEntry(splitValues[0], splitValues.Skip(1).ToList());
            }

            return null;
        }

        public bool IsValidIPv4(string ipString)
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
    }
}
