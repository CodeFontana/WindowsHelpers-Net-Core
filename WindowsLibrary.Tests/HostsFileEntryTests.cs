using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace WindowsLibrary.Tests
{
    public class HostsFileEntryTests
    {
        [Fact]
        public void HostsFileEntryInitialization()
        {
            // Assert -- All of these should result in ArgumentException.
            Assert.Throws<ArgumentException>(() => new HostsFileEntry(null, null));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry(null, new List<string> { "fqdn.example.com" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("38.38.38.38", null));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("38.38.38.38", new List<string> { }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("38.38.38.38", new List<string> { "" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("38.38.38.38", new List<string> { " " }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("444.38.38.38", new List<string> { "fqdn.example.com" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("", new List<string> { "fqdn.example.com" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("1.1", new List<string> { "fqdn.example.com" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("1.1.1.1.1", new List<string> { "fqdn.example.com" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry(" ", new List<string> { "fqdn.example.com" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("fqdn.example.com", new List<string> { "fqdn.example.com" }));
            Assert.Throws<ArgumentException>(() => new HostsFileEntry("fqdn.example.com", new List<string> { "38.38.38.38" }));

            // Assert -- These should all succeed, without any excpetion.
            new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com" });
            new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "someOthername" });
            new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "fqdn.other.net", "shortname1", "shortname2" });
            new HostsFileEntry("38.38.38.38", new List<string> { "x" });
            new HostsFileEntry("111.111.111.111", new List<string> { "x", "y" });
            new HostsFileEntry("1.1.1.1", new List<string> { "x", "y" });
            new HostsFileEntry("255.255.255.255", new List<string> { "x", "y" });
        }

        [Fact]
        public void HostsFileEntryEquals_0()
        {
            // Arrange -- Two matching entries (FQDN only).
            var entryA = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com " });
            var entryB = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com " });
            bool expected = true;

            // Act -- They should be equal.
            bool actual = entryA.Equals(entryB);

            // Assert -- Please be equal.
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HostsFileEntryEquals_1()
        {
            // Arrange -- Two matching entries (FQDN and shortname)
            var entryA = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            var entryB = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            bool expected = true;

            // Act -- They should be equal.
            bool actual = entryA.Equals(entryB);

            // Assert -- Please be equal.
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HostsFileEntryEquals_2()
        {
            // Arrange -- Two non-matching entries (different address).
            var entryA = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            var entryB = new HostsFileEntry("38.38.38.39", new List<string> { "fqdn.example.com", "shortname" });
            bool expected = false;

            // Act -- They should not be equal.
            bool actual = entryA.Equals(entryB);

            // Assert -- Please don't be equal.
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HostsFileEntryEquals_3()
        {
            // Arrange -- Two non-matching entries (different FQDN)
            var entryA = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            var entryB = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.sample.com", "shortname" });
            bool expected = false;

            // Act -- They should not be equal.
            bool actual = entryA.Equals(entryB);

            // Assert -- Please don't be equal.
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HostsFileEntryEquals_4()
        {
            // Arrange -- Two non-matching entries (different shortname)
            var entryA = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "short" });
            var entryB = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            bool expected = false;

            // Act -- They should not be equal.
            bool actual = entryA.Equals(entryB);

            // Assert -- Please don't be equal.
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HostsFileEntryEquals_5()
        {
            // Arrange -- Two non-matching entries (different number of hosts)
            var entryA = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com" });
            var entryB = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            bool expected = false;

            // Act -- They should not be equal.
            bool actual = entryA.Equals(entryB);

            // Assert -- Please don't be equal.
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HostsFileEntryEquals_6()
        {
            // Arrange -- Two non-matching entries (different number of hosts)
            var entryA = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com", "shortname" });
            var entryB = new HostsFileEntry("38.38.38.38", new List<string> { "fqdn.example.com" });
            bool expected = false;

            // Act -- They should not be equal.
            bool actual = entryA.Equals(entryB);

            // Assert -- Please don't be equal.
            Assert.Equal(expected, actual);
        }
    }
}
