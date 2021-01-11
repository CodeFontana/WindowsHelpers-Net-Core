using System;
using System.Runtime.Versioning;

namespace LocalPolicyLibrary
{
    [SupportedOSPlatform("windows")]
    public class GroupPolicyException : Exception
    {
        internal GroupPolicyException(string message)
            : base(message) { }
    }
}
