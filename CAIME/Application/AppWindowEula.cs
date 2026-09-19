using System;
using System.IO;

namespace CAIME
{
    internal static class AppWindowEula
    {
        public const string Version = "1.0";
        public const string WindowTitle = "End User License Agreement";
        public const string Caption = "End User License Agreement";

        public static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EULA.txt");
    }
}
