using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsLibrary.HostFile
{
    public class HostsFileEntry
    {
        public string Address { get; set; }
        public List<string> Hosts { get; set; }

        public HostsFileEntry(string address, List<string> hosts)
        {
            Address = address;
            Hosts = hosts;
        }

        public override string ToString()
        {
            return string.Format("{0}={1})", Address, string.Join(",", Hosts));
        }
    }
}
