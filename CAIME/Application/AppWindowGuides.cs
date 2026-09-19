using System;
using System.IO;

namespace CAIME
{
    /// <summary>
    /// Metadata and path helpers for the User Guides window. The guides are plain markdown
    /// files in a <c>Docs</c> folder; <see cref="IndexFileName"/> is the entry point shown
    /// on open and links to every other guide.
    /// </summary>
    internal static class AppWindowGuides
    {
        public const string WindowTitle = "User Guides";
        public const string Caption = "User Guides";

        /// <summary>The markdown index file that lists all guides.</summary>
        public const string IndexFileName = "user-guides.md";

        /// <summary>Name of the folder that holds the guide markdown files.</summary>
        private const string DocsFolderName = "Docs";

        public static string DocsDirectory
        {
            get
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, DocsFolderName);
                    if (Directory.Exists(candidate))
                        return candidate;

                    dir = dir.Parent;
                }

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DocsFolderName);
            }
        }
    }
}
