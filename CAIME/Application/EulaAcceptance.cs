using Microsoft.Win32;

namespace CAIME
{
    internal static class EulaAcceptance
    {
        private const string RegistryKey = @"Software\CampaignMapToolkit";
        private const string ValueName = "EulaVersion";

        public static bool IsAccepted()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryKey))
                return (key?.GetValue(ValueName) as string) == AppWindowEula.Version;
        }

        public static void Accept()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RegistryKey))
                key?.SetValue(ValueName, AppWindowEula.Version);
        }
    }
}
