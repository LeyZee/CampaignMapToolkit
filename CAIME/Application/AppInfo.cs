using System.Diagnostics;
using System.Reflection;

namespace CAIME
{
    internal static class AppInfo
    {
        public static readonly string DevBuild = "dev-build";
        public static readonly string Version = GetVersion();

        private static string GetVersion()
        {
            if (Debugger.IsAttached)
            {
                return DevBuild;
            }

            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
