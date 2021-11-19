using LoggerLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace WindowsLibrary;

public class WmiHelper
{
    private readonly IFileLogger _logger;

    public WmiHelper(IFileLogger logger)
    {
        _logger = logger.CreateFileLogger(GetType().Name);
    }

    /// <summary>
    /// Quick sample usages for the WMI helpers to obtain system information.
    /// </summary>
    public static void SampleUsage()
    {
        var logger = new FileLoggerProvider("WmiHelper");
        WmiHelper wmi = new(logger.CreateFileLogger("Sample"));

        // EXAMPLE: Get Manufacturer + Model info.
        logger.Log("Example: Win32_ComputerSystem [Manufacturer and Model]\n");
        List<string[]> wmiData = wmi.GetWMIData("root\\cimv2", "Win32_ComputerSystem", new List<string> { "Manufacturer", "Model" });
        logger.Log($"Manufacturer: {wmiData[1][0]}");
        logger.Log($"Model: {wmiData[1][1]}");

        // EXAMPLE: Get all columns.
        logger.Log("Example: Win32_QuickFixEngineering [ALL COLUMNS]\n" +
            wmi.GetFormattedWMIData(
                "root\\cimv2",
                "Win32_QuickFixEngineering",
                null));

        // EXAMPLE: Get specified columns.
        logger.Log("Example: Win32_QuickFixEngineering [SPECIFIC COLUMNS]\n" +
            wmi.GetFormattedWMIData(
                "root\\cimv2",
                "Win32_QuickFixEngineering",
                new List<string> { "HotFixID", "Description", "InstalledOn", "Caption" },
                4));

        logger.Close();
    }

    /// <summary>
    /// Gets the available WMI namespaces for the specified management scope.
    /// </summary>
    /// <param name="rootPath">WMI Management Scope path. Default is "root".</param>
    /// <returns>Returns a list of available WMI namespaces.</returns>
    public List<string> GetWmiNamespaces(string rootPath = "root")
    {
        List<string> namespaces = new();

        try
        {
            ManagementClass nsClass = new(new ManagementScope(rootPath), new ManagementPath("__namespace"), null);

            foreach (ManagementObject ns in nsClass.GetInstances())
            {
                string namespaceName = rootPath + "\\" + ns["Name"].ToString();
                namespaces.Add(namespaceName);
                namespaces.AddRange(GetWmiNamespaces(namespaceName));
            }
        }
        catch (Exception e)
        {
            _logger.Log(e, "Failed to query list of WMI namespaces");
        }

        return namespaces?.OrderBy(s => s).ToList() ?? namespaces;
    }

    /// <summary>
    /// Gets the available WMI classes for the specified namespace.
    /// </summary>
    /// <param name="wmiNamespaceName">The WMI namespace for obtaining the class list.</param>
    /// <returns>Returns a list of available classes in the WMI namespace.</returns>
    public List<string> GetClassNamesWithinWmiNamespace(string wmiNamespaceName)
    {
        List<string> classes = new();

        try
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                new ManagementScope(wmiNamespaceName),
                new WqlObjectQuery("SELECT * FROM meta_class"));
            ManagementObjectCollection objectCollection = searcher.Get();

            foreach (ManagementClass wmiClass in objectCollection)
            {
                string stringified = wmiClass.ToString();
                string[] parts = stringified.Split(new char[] { ':' });
                classes.Add(parts[1]);
            }
        }
        catch (Exception e)
        {
            _logger.Log(e, $"Failed to query class name list for namespace '{wmiNamespaceName}'");
        }

        return classes?.OrderBy(s => s).ToList() ?? classes;
    }

    /// <summary>
    /// Gets a list of properties (column names) for the specified WMI class.
    /// </summary>
    /// <param name="namespaceName">The WMI namespace which contains the class, e.g. root\\cimv2.</param>
    /// <param name="wmiClassName">The WMI class for obtaining the properties list.</param>
    /// <returns>A list of properties.</returns>
    public List<string> GetPropertiesOfWmiClass(string namespaceName, string wmiClassName)
    {
        List<string> output = new();

        try
        {
            ManagementPath managementPath = new();
            managementPath.Path = namespaceName;
            ManagementScope managementScope = new(managementPath);
            ObjectQuery objectQuery = new($"SELECT * FROM {wmiClassName}");
            ManagementObjectSearcher objectSearcher = new(managementScope, objectQuery);
            ManagementObjectCollection objectCollection = objectSearcher.Get();

            foreach (ManagementObject obj in objectCollection)
            {
                PropertyDataCollection props = obj.Properties;

                foreach (PropertyData p in props)
                {
                    output.Add(p.Name);
                }

                break;
            }
        }
        catch (Exception e)
        {
            _logger.Log(e, $"Failed to query properties of specified class '{namespaceName}\\{wmiClassName}'");
        }

        return output;
    }

    /// <summary>
    /// Gets a list of WMI data, including property names, for the specified WMI class.
    /// </summary>
    /// <param name="namespaceName">The WMI namespace which contains the class, e.g. root\\cimv2.</param>
    /// <param name="wmiClassName">The WMI class for obtaining the properties list.</param>
    /// <param name="columns">The list of columns to retrieve, the default is null/* for all columns.</param>
    /// <returns>A list representing each row of data from the WMI class.</returns>
    public List<string[]> GetWMIData(
        string namespaceName,
        string wmiClassName,
        List<string> columns = null)
    {
        List<string[]> output = new();

        try
        {
            ManagementPath managementPath = new();
            managementPath.Path = namespaceName;
            ManagementScope managementScope = new(managementPath);
            ObjectQuery objectQuery = new($"SELECT * FROM {wmiClassName}");

            if (columns == null || columns[0].Equals("*"))
            {
                objectQuery = new($"SELECT * FROM {wmiClassName}");
            }
            else
            {
                objectQuery = new($"SELECT {string.Join(",", columns)} FROM {wmiClassName}");
            }

            ManagementObjectSearcher searcher = new(managementScope, objectQuery);
            ManagementObjectCollection objectCollection = searcher.Get();

            foreach (ManagementObject obj in objectCollection)
            {
                List<string> row = new();
                PropertyDataCollection props = obj.Properties;

                if (output.Count == 0)
                {
                    List<string> columnNames = new();

                    foreach (PropertyData p in props)
                    {
                        columnNames.Add(p.Name);
                    }

                    output.Add(columnNames.ToArray());
                }

                foreach (PropertyData p in props)
                {
                    if (p.Value == null || string.IsNullOrWhiteSpace(p.Value.ToString()))
                    {
                        row.Add("");
                    }
                    else
                    {
                        row.Add(p.Value.ToString());
                    }
                }

                output.Add(row.ToArray());
            }

        }
        catch (Exception e)
        {
            _logger.Log(e, $"Failed to query data from '{namespaceName}\\{wmiClassName}'");
        }

        return output;
    }

    /// <summary>
    /// Gets a formatted list of WMI data, including property names, for the specified WMI class.
    /// </summary>
    /// <param name="namespaceName">The WMI namespace which contains the class, e.g. root\\cimv2.</param>
    /// <param name="wmiClassName">The WMI class for obtaining the properties list.</param>
    /// <param name="columns">The list of columns to retrieve, the default is null/* for all columns.</param>
    /// <param name="columnPadding">Padding (number of spaces) to add between columns of data in the return string.</param>
    /// <returns></returns>
    public string GetFormattedWMIData(
        string namespaceName,
        string wmiClassName,
        List<string> columns = null,
        int columnPadding = 1)
    {
        List<string[]> output = new();

        try
        {
            ManagementPath managementPath = new();
            managementPath.Path = namespaceName;
            ManagementScope managementScope = new(managementPath);
            ObjectQuery objectQuery = new($"SELECT * FROM {wmiClassName}");

            if (columns == null || columns[0].Equals("*"))
            {
                objectQuery = new($"SELECT * FROM {wmiClassName}");
            }
            else
            {
                objectQuery = new($"SELECT {string.Join(",", columns)} FROM {wmiClassName}");
            }

            ManagementObjectSearcher searcher = new(managementScope, objectQuery);
            ManagementObjectCollection objectCollection = searcher.Get();

            foreach (ManagementObject obj in objectCollection)
            {
                List<string> row = new();
                PropertyDataCollection props = obj.Properties;

                if (output.Count == 0)
                {
                    List<string> columnNames = new();

                    foreach (PropertyData p in props)
                    {
                        columnNames.Add(p.Name);
                    }

                    output.Add(columnNames.ToArray());
                }

                foreach (PropertyData p in props)
                {
                    if (p.Value == null || string.IsNullOrWhiteSpace(p.Value.ToString()))
                    {
                        row.Add("");
                    }
                    else
                    {
                        row.Add(p.Value.ToString());
                    }
                }

                output.Add(row.ToArray());
            }

        }
        catch (Exception e)
        {
            _logger.Log(e, $"Failed to query data from '{namespaceName}\\{wmiClassName}'");
        }

        return DotNetHelper.PadListElements(output, columnPadding);
    }
}
