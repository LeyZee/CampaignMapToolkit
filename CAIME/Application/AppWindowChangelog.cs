using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    internal static class AppWindowChangelog
    {
        private const string ReleaseApiUrl =
            "https://api.github.com/repos/TW-Campaign-Map-Modding-Team/CampaignMapToolkit/releases/tags/v{0}";

        public const string WindowTitle = "Release Notes";

        public static string Caption(string version) => $"What's New in v{version}";

        public static Func<Task<string>> ContentLoader(string version) =>
            () => FetchReleaseNotesAsync(version);

        private static async Task<string> FetchReleaseNotesAsync(string version)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "CAIME-ChangelogViewer");
                    var json = await client.GetStringAsync(string.Format(ReleaseApiUrl, version));
                    var body = JObject.Parse(json)["body"]?.ToString();
                    return string.IsNullOrWhiteSpace(body)
                        ? "No release notes available for this version."
                        : body;
                }
            }
            catch (Exception)
            {
                return "Unable to load release notes.\nPlease visit GitHub to view the changelog.";
            }
        }
    }
}
