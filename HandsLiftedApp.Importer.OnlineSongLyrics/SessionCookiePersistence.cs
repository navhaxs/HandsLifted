using System.Net;
using Avalonia.Controls;

namespace HandsLiftedApp.Importer.OnlineSongLyrics
{
    public static class SessionCookiePersistence
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

        public static IReadOnlyList<Cookie> SelectToPersist(IEnumerable<Cookie> cookies, DateTime nowUtc)
        {
            var selected = new List<Cookie>();
            foreach (var cookie in cookies)
            {
                if (!IsSessionCookie(cookie))
                {
                    continue;
                }

                cookie.Expires = nowUtc + Lifetime;
                selected.Add(cookie);
            }

            return selected;
        }

        public static async Task PersistAsync(NativeWebView webView)
        {
            try
            {
                var manager = webView.TryGetCookieManager();
                if (manager == null)
                {
                    return;
                }

                var cookies = await manager.GetCookiesAsync();
                var toPersist = SelectToPersist(cookies, DateTime.UtcNow);
                foreach (var cookie in toPersist)
                {
                    manager.AddOrUpdateCookie(cookie);
                }

                if (toPersist.Count > 0)
                {
                    Console.WriteLine($"[WebView] Persisted {toPersist.Count} session cookie(s)");
                }
            }
            catch (Exception ex)
            {
                // Persisting the login is best-effort; never break the import flow over it.
                Console.WriteLine($"[WebView] Failed to persist session cookies: {ex.Message}");
            }
        }

        // The WebView2 adapter reports a session cookie as an unset (default) Expires.
        private static bool IsSessionCookie(Cookie cookie) => cookie.Expires.Year <= 1970;
    }
}
