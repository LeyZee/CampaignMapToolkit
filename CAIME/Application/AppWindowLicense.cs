using System;
using System.IO;

namespace CAIME
{
    internal static class AppWindowLicense
    {
        public const string WindowTitle = "License";
        public const string Caption = "License";

        public static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LICENSE");
    }
}
