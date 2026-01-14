using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;

namespace AlienCyborgModernTempleOS;

public static class YouTubeSnapshotParser
{
    public static async Task<List<AlienOrchestrator.VideoTile>> ExtractTilesFromHtmlAsync(
        string html, int pageNumber, int maxTiles = 12)
    {
        // A) ytInitialData JSON route
        var fromInitial = ExtractTilesFromYtInitialData(html, pageNumber, maxTiles);
        if (fromInitial.Count > 0)
            return fromInitial;

        // B) DOM route (SingleFile-friendly)
        return await ExtractTilesFromDomAsync(html, pageNumber, maxTiles);
    }

    // ---------------------------
    // A) ytInitialData JSON
    // ---------------------------
    private static List<AlienOrchestrator.VideoTile> ExtractTilesFromYtInitialData(
        string html, int pageNumber, int maxTiles)
    {
        var json = TryExtractYtInitialDataJson(html);
        if (json is null) return new();

        using var doc = JsonDocument.Parse(json);

        var tiles = new List<AlienOrchestrator.VideoTile>(maxTiles);
        int idx = 0;

        foreach (var vr in FindObjectsByPropertyName(doc.RootElement, "videoRenderer"))
        {
            if (idx >= maxTiles) break;

            var title = TryGetTitle(vr);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var thumb = TryGetThumbUrl(vr) ?? "";

            idx++;
            tiles.Add(new AlienOrchestrator.VideoTile(
                Page: pageNumber,
                Idx: idx,
                Title: title.Trim(),
                ThumbUrl: thumb
            ));
        }

        return tiles;
    }

    // ---------------------------
    // B) DOM parsing
    // ---------------------------
    private static async Task<List<AlienOrchestrator.VideoTile>> ExtractTilesFromDomAsync(
        string html, int pageNumber, int maxTiles)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html));

        // YouTube titles are commonly: a#video-title (also sometimes: a[href*="/watch"])
        var anchors = document.QuerySelectorAll("a#video-title, a[href*=\"/watch\"]");

        var tiles = new List<AlienOrchestrator.VideoTile>(maxTiles);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int idx = 0;

        foreach (var a in anchors)
        {
            if (idx >= maxTiles) break;

            var title = (a.TextContent ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
                title = (a.GetAttribute("title") ?? "").Trim();

            // ignore garbage
            if (string.IsNullOrWhiteSpace(title)) continue;
            if (title.Length > 180) title = title[..180]; // safety
            if (!seen.Add(title)) continue;

            // Try to find a thumbnail near the link
            var container =
                a.Closest("ytd-rich-item-renderer, ytd-video-renderer, ytd-rich-grid-media, ytd-compact-video-renderer")
                ?? a.ParentElement;

            string thumbUrl = "";

            if (container != null)
            {
                // common patterns
                var img = container.QuerySelector("ytd-thumbnail img, img#img, img");
                thumbUrl = img?.GetAttribute("src") ?? "";

                if (string.IsNullOrWhiteSpace(thumbUrl))
                {
                    var srcset = img?.GetAttribute("srcset");
                    thumbUrl = PickBestFromSrcset(srcset) ?? "";
                }
            }

            idx++;
            tiles.Add(new AlienOrchestrator.VideoTile(
                Page: pageNumber,
                Idx: idx,
                Title: title,
                ThumbUrl: thumbUrl
            ));
        }

        return tiles;
    }

    private static string? PickBestFromSrcset(string? srcset)
    {
        if (string.IsNullOrWhiteSpace(srcset)) return null;
        var parts = srcset.Split(',')
                          .Select(p => p.Trim())
                          .Where(p => p.Length > 0)
                          .ToList();
        if (parts.Count == 0) return null;

        var last = parts[^1];
        return last.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    // ---------------------------
    // ytInitialData extraction (brace-match)
    // ---------------------------
    private static string? TryExtractYtInitialDataJson(string html)
    {
        var markers = new[]
        {
            "var ytInitialData =",
            "let ytInitialData =",
            "const ytInitialData =",
            "ytInitialData =",
            "window[\"ytInitialData\"] =",
            "window['ytInitialData'] ="
        };

        foreach (var marker in markers)
        {
            var i = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (i < 0) continue;

            var braceStart = html.IndexOf('{', i);
            if (braceStart < 0) continue;

            var json = ExtractBalancedBraces(html, braceStart);
            if (!string.IsNullOrWhiteSpace(json))
                return json;
        }

        return null;
    }

    private static string? ExtractBalancedBraces(string s, int startIndex)
    {
        int depth = 0;
        bool inString = false;
        char quote = '\0';
        bool escape = false;

        for (int i = startIndex; i < s.Length; i++)
        {
            var c = s[i];

            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == quote) { inString = false; continue; }
                continue;
            }

            if (c == '"' || c == '\'')
            {
                inString = true;
                quote = c;
                continue;
            }

            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return s.Substring(startIndex, i - startIndex + 1);
            }
        }

        return null;
    }

    private static string? TryGetTitle(JsonElement videoRenderer)
    {
        if (videoRenderer.TryGetProperty("title", out var title))
        {
            if (title.TryGetProperty("runs", out var runs) &&
                runs.ValueKind == JsonValueKind.Array &&
                runs.GetArrayLength() > 0 &&
                runs[0].TryGetProperty("text", out var text))
                return text.GetString();

            if (title.TryGetProperty("simpleText", out var simpleText))
                return simpleText.GetString();
        }
        return null;
    }

    private static string? TryGetThumbUrl(JsonElement videoRenderer)
    {
        if (videoRenderer.TryGetProperty("thumbnail", out var tn) &&
            tn.TryGetProperty("thumbnails", out var thumbs) &&
            thumbs.ValueKind == JsonValueKind.Array &&
            thumbs.GetArrayLength() > 0)
        {
            var last = thumbs[thumbs.GetArrayLength() - 1];
            if (last.TryGetProperty("url", out var url))
                return url.GetString();
        }
        return null;
    }

    private static IEnumerable<JsonElement> FindObjectsByPropertyName(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.NameEquals(propertyName) && prop.Value.ValueKind == JsonValueKind.Object)
                    yield return prop.Value;

                foreach (var nested in FindObjectsByPropertyName(prop.Value, propertyName))
                    yield return nested;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                foreach (var nested in FindObjectsByPropertyName(item, propertyName))
                    yield return nested;
        }
    }
}
