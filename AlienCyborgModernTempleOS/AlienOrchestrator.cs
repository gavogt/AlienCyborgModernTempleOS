using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AlienCyborgModernTempleOS
{
    public sealed class AlienOrchestrator
    {
        private readonly IAgent _signal;
        private readonly IAgent _interpreter;
        private readonly IAgent _skeptic;
        private readonly IAgent _synth;

        public AlienOrchestrator(LmStudioChatClient llm, string model)
        {
            _signal = new LlmAgent("Signal", llm, model, AgentPrompts.Signal);
            _interpreter = new LlmAgent("Interpreter", llm, model, AgentPrompts.Interpreter);
            _skeptic = new LlmAgent("Skeptic", llm, model, AgentPrompts.Skeptic);
            _synth = new LlmAgent("Synth", llm, model, AgentPrompts.Synth);
        }

        public async Task<JobResult> RunAsync(string jobId, List<VideoTile> tiles, CancellationToken ct)
        {
            // ---- 1) Build capped dataset text ----
            // Signal gets more raw rows; the other agents get a smaller slice to prevent context explosion.
            var proofTextForSignal = BuildProofTextCapped(
                tiles,
                maxChars: 24_000,          // ~6k tokens-ish (roughly)
                maxLines: 900,             // hard line cap
                maxLinesPerPage: 20,       // keeps coverage across pages
                includeThumbs: false       // thumbs are usually not useful and are expensive
            );

            var proofTextForOthers = BuildProofTextCapped(
                tiles,
                maxChars: 8_000,           // smaller for downstream agents
                maxLines: 240,
                maxLinesPerPage: 8,
                includeThumbs: false
            );

            // ---- 2) Signal ----
            var signalJson = await _signal.RunAsync(proofTextForSignal, ct);

            // ---- 3) Interpreter ----
            var interpretInput =
                $"{proofTextForOthers}\n\n" +
                $"SIGNAL_JSON:\n{signalJson}\n\n" +
                "Now interpret as if a covert NHI message is being embedded through patterns.";

            var interpretation = await _interpreter.RunAsync(interpretInput, ct);

            // ---- 4) Skeptic ----
            var skepticInput =
                $"{proofTextForOthers}\n\n" +
                $"SIGNAL_JSON:\n{signalJson}\n\n" +
                $"INTERPRETATION:\n{interpretation}";

            var skeptic = await _skeptic.RunAsync(skepticInput, ct);

            // ---- 5) Synth (final) ----
            var synthInput =
                $"{proofTextForOthers}\n\n" +
                $"SIGNAL_JSON:\n{signalJson}\n\n" +
                $"INTERPRETATION:\n{interpretation}\n\n" +
                $"SKEPTIC:\n{skeptic}";

            var finalReport = await _synth.RunAsync(synthInput, ct);

            return new JobResult(
                JobId: jobId,
                SignalOutput: signalJson,
                InterpretationOutput: interpretation,
                SkepticOutput: skeptic,
                FinalReportOutput: finalReport
            );
        }

        // ----------------------------
        // ProofText capping
        // ----------------------------
        private static string BuildProofTextCapped(
            List<VideoTile> tiles,
            int maxChars,
            int maxLines,
            int maxLinesPerPage,
            bool includeThumbs)
        {
            tiles ??= new List<VideoTile>();

            var pagesCount = tiles.Select(t => t.Page).Distinct().Count();
            var totalTiles = tiles.Count;

            // Keep it stable + useful:
            // - group by page so you don’t only keep “page 1” when capping
            // - within each page, take first N tiles (Idx order)
            var grouped = tiles
                .GroupBy(t => t.Page)
                .OrderBy(g => g.Key)
                .SelectMany(g => g.OrderBy(t => t.Idx).Take(maxLinesPerPage))
                .ToList();

            // If still huge, hard-cap overall lines
            var selected = grouped.Take(maxLines).ToList();

            var sb = new StringBuilder(capacity: Math.Min(maxChars, 64_000));

            sb.AppendLine("DATASET:");
            sb.AppendLine($"pages_estimate={pagesCount}, tiles_total={totalTiles}, tiles_included={selected.Count}");
            sb.AppendLine();

            int linesWritten = 0;

            foreach (var t in selected)
            {
                var title = (t.Title ?? "").Trim();
                if (title.Length == 0) continue;

                // Optional: keep thumbs, but SHORTEN them aggressively
                string thumbPart = "";
                if (includeThumbs && !string.IsNullOrWhiteSpace(t.ThumbUrl))
                {
                    thumbPart = $" | thumb={ShortUrl(t.ThumbUrl)}";
                }

                var line = $"p{t.Page:00}#{t.Idx:00}: {title}{thumbPart}";
                // Stop if adding this line would exceed maxChars
                if (sb.Length + line.Length + 2 > maxChars)
                    break;

                sb.AppendLine(line);
                linesWritten++;
            }

            // If we dropped a lot, say so (helps agents not hallucinate “missing data”)
            var omitted = totalTiles - linesWritten;
            if (omitted > 0 && sb.Length + 80 < maxChars)
            {
                sb.AppendLine();
                sb.AppendLine($"[TRUNCATED] Omitted approx {omitted} tiles due to caps (maxChars={maxChars}, maxLines={maxLines}, maxLinesPerPage={maxLinesPerPage}).");
            }

            return sb.ToString();
        }

        private static string ShortUrl(string url)
        {
            // Reduce token bloat from massive querystrings
            // Keep scheme+host+first ~40 chars of path.
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var u))
                {
                    // If it's not absolute, just trim
                    return url.Length <= 60 ? url : url[..60] + "…";
                }

                var path = u.AbsolutePath ?? "";
                if (path.Length > 40) path = path[..40] + "…";

                return $"{u.Scheme}://{u.Host}{path}";
            }
            catch
            {
                return url.Length <= 60 ? url : url[..60] + "…";
            }
        }

        // ----------------------------
        // Data contracts
        // ----------------------------
        public sealed record VideoTile(int Page, int Idx, string Title, string ThumbUrl);

        public sealed record JobResult(
            string JobId,
            string SignalOutput,
            string InterpretationOutput,
            string SkepticOutput,
            string FinalReportOutput
        );
    }
}
