using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO.Compression;

namespace AlienCyborgModernTempleOS.Pages
{
    
        // If zips are larger than 30MB consider increasing size

        [RequestSizeLimit(200_000_000)] // 200 MB
    public class IndexModel : PageModel
    {

        [TempData]

        public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnPostUploadSnapshotsAsync(IFormFile? SnapshotZip, CancellationToken ct)
        {
            if(SnapshotZip == null || SnapshotZip.Length == 0)
            {
                StatusMessage = "No file selected or file is empty.";
                return Page();
            }

            if(!SnapshotZip.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Invalid file format. Please upload a .zip file.";
                return Page();
            }

            var jobId = $"job_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
            var jobsRoot = Path.Combine(AppContext.BaseDirectory, "App_Data", "jobs");
            var jobDir = Path.Combine(jobsRoot, jobId);
            var zipPath = Path.Combine(jobDir, "snapshot.zip");
            var extractDir = Path.Combine(jobDir, "extracted"); 

            Directory.CreateDirectory(jobDir);
            Directory.CreateDirectory(extractDir);

            await using(var fs = System.IO.File.Create(zipPath))
            {
                await SnapshotZip.CopyToAsync(fs, ct);
            }

            SafeExtractZip(zipPath, extractDir);

            var htmlFiles = Directory.EnumerateFiles(extractDir, "*.html", SearchOption.AllDirectories).ToList();

            StatusMessage = $"Upload complete. job {jobId}: with {htmlFiles.Count} HTML files extracted HTML snapshot(s).";
            return Page();

        }

        private void SafeExtractZip(string zipPath, string destinationDir)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue; // Skip directory entries
                }

                var fullPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));

                if (!fullPath.StartsWith(Path.GetFullPath(destinationDir), StringComparison.OrdinalIgnoreCase))
                    continue;

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                entry.ExtractToFile(fullPath, overwrite: true);
            }
        }
    }
}
