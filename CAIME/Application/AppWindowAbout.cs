using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    internal static class AppWindowAbout
    {
        private const string AppName = "Campaign pAthfInding Map Editor (CAIME)";
        private const string RepoUrl = "https://github.com/TW-Campaign-Map-Modding-Team/CampaignMapToolkit";
        private const string ReleaseApiUrl =
            "https://api.github.com/repos/TW-Campaign-Map-Modding-Team/CampaignMapToolkit/releases/tags/v{0}";

        public const string WindowTitle = "About";
        public const string Caption = "About the application";

        public static Func<Task<IEnumerable<Inline>>> ContentLoader(string version) =>
            () => BuildContentAsync(version);

        private static async Task<IEnumerable<Inline>> BuildContentAsync(string version)
        {
            (string releaseDate, string changelog) = await FetchReleaseInfoAsync(version);

            var inlines = new List<Inline>
            {
                new Run("Application: " + AppName + "\n"),
                new Run("Application version: " + version + "\n"),
                new Run("Version release date: " + releaseDate + "\n\n"),
                new Run("Source code: ")
            };

            var link = new Hyperlink(new Run(RepoUrl))
            {
                NavigateUri = new Uri(RepoUrl),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 255))
            };

            link.RequestNavigate += (s, e) =>
            {
                System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            };

            inlines.Add(link);
            inlines.Add(new Run("\n\n"));
            inlines.Add(new Run("───────────────────────────────\n") { FontWeight = FontWeights.Bold });
            inlines.Add(new Run("Changelog\n") { FontWeight = FontWeights.Bold, FontSize = 14 });
            inlines.Add(new Run("───────────────────────────────\n\n") { FontWeight = FontWeights.Bold });
            inlines.Add(new Run(changelog));

            return inlines;
        }

        private static async Task<(string date, string changelog)> FetchReleaseInfoAsync(string version)
        {
            if (version == AppInfo.DevBuild)
                return ("development build", "Not available in dev mode.");

            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "CAIME-AboutViewer");
                    var json = await client.GetStringAsync(string.Format(ReleaseApiUrl, version));
                    var obj = JObject.Parse(json);

                    string date = "(unknown)";
                    var publishedAt = obj["published_at"]?.ToString();
                    if (!string.IsNullOrEmpty(publishedAt) && DateTime.TryParse(publishedAt, out var dt))
                        date = dt.ToString("MMMM d, yyyy");

                    var body = obj["body"]?.ToString();
                    string changelog = string.IsNullOrWhiteSpace(body)
                        ? "No release notes available."
                        : body;

                    return (date, changelog);
                }
            }
            catch
            {
                return (
                    "(unavailable — check GitHub for release date)",
                    "Unable to load release notes.\nPlease visit GitHub to view the changelog.");
            }
        }
    }
}
