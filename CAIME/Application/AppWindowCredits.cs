using System;
using System.IO;

namespace CAIME
{
    internal static class AppWindowCredits
    {
        public const string WindowTitle = "Credits";
        public const string Caption = "Credits";

        public static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CREDITS.txt");
    }
}
