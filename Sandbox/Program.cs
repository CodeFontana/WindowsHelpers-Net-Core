using LoggerLibrary;
using WindowsLibrary;
using System;
using System.Collections.Generic;

namespace Sandbox
{
    class Program
    {
        static void Main(string[] args)
        {
            // Open log.
            var sandbox = new Logger("Sandbox", null, 52428800, 1);

            // EXAMPLE: Get all columns.
            sandbox.Log("Example: Win32_QuickFixEngineering [ALL COLUMNS]\n" + 
                WMIHelper.GetFormattedWMIData(
                    sandbox.LogComponent,
                    "root\\cimv2",
                    "Win32_QuickFixEngineering",
                    null));

            // EXAMPLE: Get specified columns.
            sandbox.Log("Example: Win32_QuickFixEngineering [SPECIFIC COLUMNS]\n" + 
                WMIHelper.GetFormattedWMIData(
                    sandbox.LogComponent,
                    "root\\cimv2",
                    "Win32_QuickFixEngineering",
                    new List<string> { "HotFixID", "Description", "InstalledOn", "Caption" },
                    4));

            // Close log.
            sandbox.Close();
        }
    }
}