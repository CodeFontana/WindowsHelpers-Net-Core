
using System.Runtime.Versioning;

namespace LocalPolicyLibrary
{
    [SupportedOSPlatform("windows")]
    public enum GroupPolicySection
    {
        Root = 0,
        User = 1,
        Machine = 2,
    }
}
