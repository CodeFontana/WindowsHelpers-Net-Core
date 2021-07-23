using LoggerLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace WindowsLibrary.Tests
{
    public class HostsFileHelperTests
    {
        [Fact]
        public void CreateNewHostFileEntries()
        {
            // Arrange.
            var hostsFile = new HostsFileHelper();
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");

            // Act.
            hostsFile.CreateEntry("38.38.38.38", "fqdn.example.com");
            hostsFile.CreateEntry("38.38.38.40", "fqdn.sample.com\t\tshortname");
            hostsFile.CreateEntry("38.38.38.42", "eggs.sample.com,bacon,toast");
            hostsFile.CreateEntry("38.38.38.44", "onetab.sample.com\tonetab");
            hostsFile.CreateEntry("38.38.38.46", "onespace.sample.com onespace");
            hostsFile.CreateEntry("38.38.38.48", "manyspace.sample.com         manyspace");

            // Assert.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "fqdn.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "fqdn.sample.com" && e.Hosts[1] == "shortname");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.42") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "eggs.sample.com" && e.Hosts[1] == "bacon" && e.Hosts[2] == "toast");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.44") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "onetab.sample.com" && e.Hosts[1] == "onetab");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.46") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "onespace.sample.com" && e.Hosts[1] == "onespace");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.48") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "manyspace.sample.com" && e.Hosts[1] == "manyspace");

            // Cleanup.
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
            hostsFile.DeleteEntry("38.38.38.42");
            hostsFile.DeleteEntry("38.38.38.44");
            hostsFile.DeleteEntry("38.38.38.46");
            hostsFile.DeleteEntry("38.38.38.48");
        }

        [Fact]
        public void CreateNewHostFileEntryFailures()
        {
            // Arrange.
            var hostsFile = new HostsFileHelper();

            // Act.
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.CreateEntry("38.38.38.38", "fqdn.example.com\t\tshortname");

            // Assert -- Failures due to bad input.
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry("38.38.38.38.38", "fqdn.example.com"));
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry("38.38.38.40", ""));
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry("38.38.38.42", null));
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry(null, null));
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry(null, "fqdn.example.com"));

            // Assert -- Failures due to address or host already defined.
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry("38.38.38.38", "fqdn.different.com"));
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry("38.38.38.40", "fqdn.example.com"));
            Assert.Throws<ArgumentException>(() => hostsFile.CreateEntry("38.38.38.42", "shortname"));

            // Cleanup.
            hostsFile.DeleteEntry("38.38.38.38");
        }

        [Fact]
        public void ReadHostsFileEntries()
        {
            // Arrange.
            var logger = new SimpleLogger("HostsFileHelperTests");
            var hostsFile = new HostsFileHelper();

            // Act.
            foreach (HostsFileEntry entry in hostsFile.ReadHostsFile())
            {
                // Write debug.
                logger.Log(entry.ToString());
            }

            // Assert.
            Assert.NotNull(hostsFile);
        }

        [Fact]
        public void UpdateHostsFileEntries_UpdateHost()
        {
            // Arrange -- Delete and recreate test entries.
            var hostsFile = new HostsFileHelper();
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
            hostsFile.DeleteEntry("38.38.38.50");
            hostsFile.DeleteEntry("38.38.38.60");
            hostsFile.CreateEntry("38.38.38.38", "red.example.com");
            hostsFile.CreateEntry("38.38.38.40", "orange.example.com\t\torange");
            hostsFile.CreateEntry("38.38.38.50", "green.example.com\t\tgreen");
            hostsFile.CreateEntry("38.38.38.60", "blue.example.com\t\tblue\t\tteal");

            // Assert -- Validate initial entries are created, as expected.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "red.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "orange.example.com" && e.Hosts[1] == "orange");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.50") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "green.example.com" && e.Hosts[1] == "green");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.60") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "blue.example.com" && e.Hosts[1] == "blue" && e.Hosts[2] == "teal");

            // Act -- Update defined hosts, for test addresses.
            hostsFile.UpdateEntry("38.38.38.38", "purple.example.com");
            hostsFile.UpdateEntry("38.38.38.40", "yellow.example.com\t\tyellow");
            hostsFile.UpdateEntry("38.38.38.50", "olive.example.com\t\tolive");
            hostsFile.UpdateEntry("38.38.38.60", "grey.example.com\t\tgrey\t\tdarkgrey");

            // Assert -- Validate updates, as expected.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "purple.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "yellow.example.com" && e.Hosts[1] == "yellow");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.50") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "olive.example.com" && e.Hosts[1] == "olive");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.60") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "grey.example.com" && e.Hosts[1] == "grey" && e.Hosts[2] == "darkgrey");

            // Cleanup.
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
            hostsFile.DeleteEntry("38.38.38.50");
            hostsFile.DeleteEntry("38.38.38.60");
        }

        [Fact]
        public void UpdateHostsFileEntries_UpdateAddress()
        {
            // Arrange.
            var hostsFile = new HostsFileHelper();
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
            hostsFile.DeleteEntry("38.38.38.50");
            hostsFile.DeleteEntry("38.38.38.60");
            hostsFile.CreateEntry("38.38.38.38", "red.example.com");
            hostsFile.CreateEntry("38.38.38.40", "orange.example.com\t\torange");
            hostsFile.CreateEntry("38.38.38.50", "green.example.com\t\tgreen");
            hostsFile.CreateEntry("38.38.38.60", "blue.example.com\t\tblue\t\tteal");

            // Assert -- Validate initial entries are created, as expected.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "red.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "orange.example.com" && e.Hosts[1] == "orange");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.50") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "green.example.com" && e.Hosts[1] == "green");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.60") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "blue.example.com" && e.Hosts[1] == "blue" && e.Hosts[2] == "teal");

            // Act -- Update defined addresses, based on matching host.
            hostsFile.UpdateEntry("38.38.38.68", "red.example.com");
            hostsFile.UpdateEntry("38.38.38.70", "orange.example.com\t\torange");
            hostsFile.UpdateEntry("38.38.38.80", "green");
            hostsFile.UpdateEntry("38.38.38.90", "blue");

            // Assert -- Validate updates, as expected.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.68") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "red.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.70") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "orange.example.com" && e.Hosts[1] == "orange");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.80") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "green.example.com" && e.Hosts[1] == "green");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.90") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "blue.example.com" && e.Hosts[1] == "blue" && e.Hosts[2] == "teal");

            // Cleanup.
            hostsFile.DeleteEntry("38.38.38.68");
            hostsFile.DeleteEntry("38.38.38.70");
            hostsFile.DeleteEntry("38.38.38.80");
            hostsFile.DeleteEntry("38.38.38.90");
        }

        [Fact]
        public void UpdateHostsFileEntriesFailures()
        {
            // Arrange.
            var hostsFile = new HostsFileHelper();

            // Act.
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
            hostsFile.DeleteEntry("38.38.38.42");

            // Assert -- Failures due to bad input.
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry("38.38.38.38.38", "fqdn.example.com"));
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry("38.38.38.40", ""));
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry("38.38.38.42", null));
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry(null, null));
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry(null, "fqdn.example.com"));

            // Assert -- Failures due to address or host NOT defined.
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry("38.38.38.40", "fqdn.different.com"));
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry("38.38.38.40", "fqdn.example.com"));
            Assert.Throws<ArgumentException>(() => hostsFile.UpdateEntry("38.38.38.42", "shortname"));
        }

        [Fact]
        public void UpsertHostsFileEntries()
        {
            // Arrange -- Delete and recreate test entries.
            var hostsFile = new HostsFileHelper();
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
            hostsFile.DeleteEntry("38.38.38.50");
            hostsFile.DeleteEntry("38.38.38.60");
            hostsFile.CreateEntry("38.38.38.38", "red.example.com");
            hostsFile.CreateEntry("38.38.38.40", "orange.example.com\t\torange");
            hostsFile.CreateEntry("38.38.38.50", "green.example.com\t\tgreen");
            hostsFile.CreateEntry("38.38.38.60", "blue.example.com\t\tblue\t\tteal");

            // Assert -- Validate initial entries are created, as expected.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "red.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "orange.example.com" && e.Hosts[1] == "orange");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.50") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "green.example.com" && e.Hosts[1] == "green");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.60") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "blue.example.com" && e.Hosts[1] == "blue" && e.Hosts[2] == "teal");

            // Act -- Upsert entries -- Update entries created above
            hostsFile.UpsertEntry("38.38.38.38", "purple.example.com");
            hostsFile.UpsertEntry("38.38.38.40", "yellow.example.com\t\tyellow");
            hostsFile.UpsertEntry("38.38.38.50", "olive.example.com\t\tolive");
            hostsFile.UpsertEntry("38.38.38.60", "grey.example.com\t\tgrey\t\tdarkgrey");

            // Assert -- Validate updates, as expected.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "purple.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "yellow.example.com" && e.Hosts[1] == "yellow");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.50") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "olive.example.com" && e.Hosts[1] == "olive");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.60") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "grey.example.com" && e.Hosts[1] == "grey" && e.Hosts[2] == "darkgrey");

            // Act -- Upsert entries -- Create new entries to be inserted
            hostsFile.CreateEntry("38.38.38.10", "black.example.com");
            hostsFile.CreateEntry("38.38.38.11", "gray.example.com\t\tgray");
            hostsFile.CreateEntry("38.38.38.12", "lightGray.example.com\t\tlightGray");
            hostsFile.CreateEntry("38.38.38.13", "darkGray.example.com\t\tdarkGray\t\tcharcoal");

            // Assert -- Validate entries created, as expected.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.10") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "black.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.11") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "gray.example.com" && e.Hosts[1] == "gray");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.12") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "lightGray.example.com" && e.Hosts[1] == "lightGray");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.13") &&
                e.Hosts.Count == 3 && e.Hosts[0] == "darkGray.example.com" && e.Hosts[1] == "darkGray" && e.Hosts[2] == "charcoal");

            // Cleanup.
            hostsFile.DeleteEntry("38.38.38.10");
            hostsFile.DeleteEntry("38.38.38.11");
            hostsFile.DeleteEntry("38.38.38.12");
            hostsFile.DeleteEntry("38.38.38.13");
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
            hostsFile.DeleteEntry("38.38.38.50");
            hostsFile.DeleteEntry("38.38.38.60");
        }

        [Fact]
        public void DeleteHostFileEntries()
        {
            // Arrange.
            var hostsFile = new HostsFileHelper();
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");

            // Act.
            hostsFile.CreateEntry("38.38.38.38", "fqdn.example.com");
            hostsFile.CreateEntry("38.38.38.40", "fqdn.sample.com\t\tshortname");

            // Assert.
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "fqdn.example.com");
            Assert.Contains(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "fqdn.sample.com" && e.Hosts[1] == "shortname");

            // Cleanup.
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");

            // Assert.
            Assert.DoesNotContain(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.38") &&
                e.Hosts.Count == 1 && e.Hosts[0] == "fqdn.example.com");
            Assert.DoesNotContain(hostsFile.ReadHostsFile(),
                e => e.Address.Equals("38.38.38.40") &&
                e.Hosts.Count == 2 && e.Hosts[0] == "fqdn.sample.com" && e.Hosts[1] == "shortname");
        }

        [Fact]
        public void ExistsInHostFileEntries()
        {
            // Arrange.
            var hostsFile = new HostsFileHelper();
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");

            // Act.
            hostsFile.CreateEntry("38.38.38.38", "fqdn.example.com");
            hostsFile.CreateEntry("38.38.38.40", "fqdn.sample.com\t\tshortname");

            // Assert.
            Assert.True(hostsFile.ExistsEntry("38.38.38.38"));
            Assert.True(hostsFile.ExistsEntry("fqdn.example.com"));
            Assert.False(hostsFile.ExistsEntry("38.38.38.42"));
            Assert.False(hostsFile.ExistsEntry("purple.monkey.dishwasher"));

            // Cleanup.
            hostsFile.DeleteEntry("38.38.38.38");
            hostsFile.DeleteEntry("38.38.38.40");
        }
    }
}
