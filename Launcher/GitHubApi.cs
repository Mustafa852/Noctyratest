using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NoctyraSetup
{
    public class ReleaseInfo
    {
        public string Tag;
        public string Name;
        public string Body;
        public string HtmlUrl;
        public string AssetName;
        public string AssetUrl;
        public long AssetSize;
        public string AssetSha256; // "digest" alanından, yoksa null
    }

    public static class GitHubApi
    {
        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            var c = new HttpClient();
            c.Timeout = TimeSpan.FromMinutes(30);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("NoctyraSetup/0.2");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        /// <summary>Repo adı; sırayla: komut satırı/ortam değişkeni, repo.txt, derleme sırasında gömülen değer.</summary>
        public static string RepoSlug()
        {
            string env = Environment.GetEnvironmentVariable("NOCTYRA_REPO");
            if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

            try
            {
                string file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "repo.txt");
                if (System.IO.File.Exists(file))
                {
                    string t = System.IO.File.ReadAllText(file).Trim();
                    if (t.Length > 0) return t;
                }
            }
            catch { }

            foreach (var a in Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
            {
                var m = (AssemblyMetadataAttribute)a;
                if (m.Key == "RepoSlug") return m.Value;
            }
            return "OWNER/Noctyra";
        }

        /// <summary>En son sürümü getirir. Hiç sürüm yoksa null döner.</summary>
        public static async Task<ReleaseInfo> GetLatestAsync(string slug)
        {
            string url = "https://api.github.com/repos/" + slug + "/releases/latest";
            using (var resp = await Http.GetAsync(url))
            {
                if (resp.StatusCode == HttpStatusCode.NotFound) return null;
                if (resp.StatusCode == HttpStatusCode.Forbidden)
                    throw new Exception("GitHub istek sınırına takıldın. Birkaç dakika sonra tekrar dene.");
                resp.EnsureSuccessStatusCode();

                string json = await resp.Content.ReadAsStringAsync();
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = ser.Deserialize<Dictionary<string, object>>(json);

                var info = new ReleaseInfo
                {
                    Tag = Str(root, "tag_name"),
                    Name = Str(root, "name"),
                    Body = Str(root, "body"),
                    HtmlUrl = Str(root, "html_url")
                };

                var assets = root.ContainsKey("assets") ? root["assets"] as ArrayList : null;
                if (assets != null)
                {
                    Dictionary<string, object> best = null;
                    foreach (var o in assets)
                    {
                        var a = o as Dictionary<string, object>;
                        if (a == null) continue;
                        string n = (Str(a, "name") ?? "").ToLowerInvariant();
                        if (!n.EndsWith(".zip")) continue;
                        if (best == null || n.Contains("win")) best = a;
                    }
                    if (best != null)
                    {
                        info.AssetName = Str(best, "name");
                        info.AssetUrl = Str(best, "browser_download_url");
                        info.AssetSize = best.ContainsKey("size") ? Convert.ToInt64(best["size"]) : 0;
                        string digest = Str(best, "digest");
                        if (digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                            info.AssetSha256 = digest.Substring(7);
                    }
                }
                return info;
            }
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v as string : null;
        }

        public static async Task DownloadAsync(string url, string destFile, Action<long, long> progress)
        {
            using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? 0;
                using (var src = await resp.Content.ReadAsStreamAsync())
                using (var dst = System.IO.File.Create(destFile))
                {
                    var buf = new byte[81920];
                    long done = 0;
                    int n;
                    while ((n = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await dst.WriteAsync(buf, 0, n);
                        done += n;
                        if (progress != null) progress(done, total);
                    }
                }
            }
        }
    }
}
