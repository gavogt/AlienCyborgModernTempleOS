using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO.Compression;
using System.Text.Json;
using static AlienCyborgModernTempleOS.AlienOrchestrator;

namespace AlienCyborgModernTempleOS.Pages
{
    [RequestSizeLimit(200_000_000)]
    public class IndexModel : PageModel
    {
        [TempData] public string? StatusMessage { get; set; }
        [TempData] public string? LastJobId { get; set; }

        private readonly AlienOrchestrator _orchestrator;

        public IndexModel(AlienOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public async Task<IActionResult> OnPostUploadSnapshotsAsync(IFormFile? SnapshotZip, CancellationToken ct)
        {
            if (SnapshotZip == null || SnapshotZip.Length == 0)
            {
                StatusMessage = "No file selected or file is empty.";
                return Page();
            }

            if (!SnapshotZip.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Invalid file format. Please upload a .zip file.";
                return Page();
            }

            var jobId = $"job_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            LastJobId = jobId;

            var jobsRoot = Path.Combine(AppContext.BaseDirectory, "App_Data", "jobs");
            var jobDir = Path.Combine(jobsRoot, jobId);
            var zipPath = Path.Combine(jobDir, "snapshot.zip");
            var extractDir = Path.Combine(jobDir, "extracted");

            Directory.CreateDirectory(jobDir);
            Directory.CreateDirectory(extractDir);

            await using (var fs = System.IO.File.Create(zipPath))
            {
                await SnapshotZip.CopyToAsync(fs, ct);
            }

            SafeExtractZip(zipPath, extractDir);

            var htmlFiles = Directory.EnumerateFiles(extractDir, "*.html", SearchOption.AllDirectories).ToList();

            await WriteJsonAsync(Path.Combine(jobDir, "status.json"), new
            {
                jobId,
                state = "extracted",
                htmlFiles = htmlFiles.Count,
                updatedUtc = DateTime.UtcNow
            }, ct);

            var tiles = new List<VideoTile>();
            int page = 0;
            var perFile = new List<object>();

            foreach (var file in htmlFiles)
            {
                page++;
                var html = await System.IO.File.ReadAllTextAsync(file, ct);
                var hasMarker = html.Contains("ytInitialData", StringComparison.OrdinalIgnoreCase);

                var pageTiles = await YouTubeSnapshotParser.ExtractTilesFromHtmlAsync(html, page, 12);
                tiles.AddRange(pageTiles);

                perFile.Add(new { file = Path.GetFileName(file), hasMarker, tiles = pageTiles.Count });
            }

            // If parsing failed, you’ll know immediately
            if (tiles.Count == 0)
            {
                await WriteJsonAsync(Path.Combine(jobDir, "parse_debug.json"), perFile, ct);

                StatusMessage = $"Job {jobId}: extracted {htmlFiles.Count} HTML files but found 0 tiles. (Could not locate ytInitialData.)";
                return Page();
            }

            await WriteJsonAsync(Path.Combine(jobDir, "status.json"), new
            {
                jobId,
                state = "running_agents",
                htmlFiles = htmlFiles.Count,
                updatedUtc = DateTime.UtcNow
            }, ct);

            var result = await _orchestrator.RunAsync(jobId, tiles, ct);

            // Save the real result so Activity can display it
            await WriteJsonAsync(Path.Combine(jobDir, "results.json"), result, ct);

            await WriteJsonAsync(Path.Combine(jobDir, "status.json"), new
            {
                jobId,
                state = "completed",
                htmlFiles = htmlFiles.Count,
                updatedUtc = DateTime.UtcNow
            }, ct);

            StatusMessage = $"Upload complete. Job {jobId}: extracted {htmlFiles.Count} HTML snapshot(s).";
            return Page();
        }

        // GET /?handler=Activity&jobId=...
        public IActionResult OnGetActivity(string jobId)
        {
            var jobDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "jobs", jobId);
            var statusPath = Path.Combine(jobDir, "status.json");
            var resultsPath = Path.Combine(jobDir, "results.json");

            if (!System.IO.File.Exists(statusPath))
                return new JsonResult(new { jobId, state = "unknown" });

            var statusJson = System.IO.File.ReadAllText(statusPath);

            if (System.IO.File.Exists(resultsPath))
            {
                var resultsJson = System.IO.File.ReadAllText(resultsPath);
                return Content($@"{{""status"":{statusJson},""results"":{resultsJson}}}", "application/json");
            }

            return Content($@"{{""status"":{statusJson}}}", "application/json");
        }

        private static async Task WriteJsonAsync<T>(string filePath, T data, CancellationToken ct)
        {
            // Path.GetDirectoryName can return null
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            await using var stream = System.IO.File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, data, new JsonSerializerOptions { WriteIndented = true }, ct);
        }

        private static void SafeExtractZip(string zipPath, string destinationDir)
        {
            var destFull = Path.GetFullPath(destinationDir);

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var fullPath = Path.GetFullPath(Path.Combine(destFull, entry.FullName));
                if (!fullPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                entry.ExtractToFile(fullPath, overwrite: true);
            }
        }

        private static List<VideoTile> BuildTilesStub(List<string> htmlFiles)
        {
            var tiles = new List<VideoTile>();
            int page = 0;

            foreach (var file in htmlFiles)
            {
                page++;
                tiles.Add(new VideoTile(
                    Page: page,
                    Idx: 1,
                    Title: Path.GetFileNameWithoutExtension(file),
                    ThumbUrl: ""
                ));
            }

            return tiles;
        }
    }
}
