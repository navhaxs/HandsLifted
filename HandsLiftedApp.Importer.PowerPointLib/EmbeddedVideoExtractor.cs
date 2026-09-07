using System.IO.Compression;
using System.Xml.Linq;

namespace HandsLiftedApp.Importer.PowerPointLib;

public class PowerPointVideoExtractionResult
{
    /// <summary>1-based "shown slide" index (matches ConvertPDF's page numbering — hidden slides excluded) -> extracted video file path.</summary>
    public Dictionary<int, string> SlideIndexToVideoFile { get; init; } = new();

    /// <summary>Human-readable messages for slides that had a video but couldn't import it.</summary>
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Pulls embedded videos straight out of a .pptx package so a slide whose main content is a video
/// can be imported as a real playable video slide instead of the static frame the PDF/PNG
/// rasterization pipeline produces. Never throws: video import is additive, not core — any failure
/// just means that slide keeps its static image.
/// </summary>
public static class EmbeddedVideoExtractor
{
    public const string WarningsFileName = "video-import-warnings.txt";

    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    // Keep in sync with HandsLiftedApp.Core.Constants.SUPPORTED_VIDEO. This project can't reference
    // Core (Core depends on this project), so the list is duplicated here.
    private static readonly HashSet<string> SupportedVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { "mp4", "flv", "mov", "mkv", "avi", "wmv", "webm" };

    public static PowerPointVideoExtractionResult ExtractVideos(string pptxPath, string outputDirectory)
    {
        var result = new PowerPointVideoExtractionResult();

        try
        {
            using var archive = ZipFile.OpenRead(pptxPath);

            var presentationEntry = archive.GetEntry("ppt/presentation.xml");
            var presentationRelsEntry = archive.GetEntry("ppt/_rels/presentation.xml.rels");
            if (presentationEntry == null || presentationRelsEntry == null) return result;

            XDocument presentationDoc;
            using (var stream = presentationEntry.Open()) presentationDoc = XDocument.Load(stream);

            XDocument presentationRelsDoc;
            using (var stream = presentationRelsEntry.Open()) presentationRelsDoc = XDocument.Load(stream);

            var presentationRelTargets = presentationRelsDoc.Root?
                .Elements(Rel + "Relationship")
                .Where(e => e.Attribute("Id") != null && e.Attribute("Target") != null)
                .ToDictionary(e => e.Attribute("Id")!.Value, e => e.Attribute("Target")!.Value);
            if (presentationRelTargets == null) return result;

            var sldIdList = presentationDoc.Root?.Element(P + "sldIdLst")?.Elements(P + "sldId").ToList();
            if (sldIdList == null || sldIdList.Count == 0) return result;

            // Ordered list of slide part paths that are actually shown, matching the numbering
            // ConvertPDF/PowerPoint's own PDF export produce (hidden slides excluded).
            var shownSlideParts = new List<string>();
            foreach (var sldId in sldIdList)
            {
                var rid = sldId.Attribute(R + "id")?.Value;
                if (rid == null || !presentationRelTargets.TryGetValue(rid, out var target)) continue;

                var slidePartPath = ResolvePresentationRelTarget(target);
                var slideEntry = archive.GetEntry(slidePartPath);
                if (slideEntry == null)
                {
                    // Can't read this part to check whether it's hidden - fail open (assume shown)
                    // rather than silently shifting every subsequent slide's number.
                    shownSlideParts.Add(slidePartPath);
                    continue;
                }

                XDocument slideDoc;
                try
                {
                    using var slideStream = slideEntry.Open();
                    slideDoc = XDocument.Load(slideStream);
                }
                catch (Exception)
                {
                    // Same fail-open reasoning as above.
                    shownSlideParts.Add(slidePartPath);
                    continue;
                }

                var show = slideDoc.Root?.Attribute("show")?.Value;
                if (show is "0" or "false") continue; // hidden slide - excluded from PDF page count too

                shownSlideParts.Add(slidePartPath);
            }

            if (shownSlideParts.Count == 0) return result;

            int maxDigits = (int)Math.Floor(Math.Log10(shownSlideParts.Count) + 1);

            for (int i = 0; i < shownSlideParts.Count; i++)
            {
                try
                {
                    ExtractVideoForSlide(archive, shownSlideParts[i], slideNumber: i + 1, maxDigits, outputDirectory, result);
                }
                catch (Exception)
                {
                    // Skip this slide's video; its static PNG (already produced by the PDF/PNG step) stands in.
                }
            }
        }
        catch (Exception)
        {
            return new PowerPointVideoExtractionResult();
        }

        return result;
    }

    private static void ExtractVideoForSlide(ZipArchive archive, string slidePartPath, int slideNumber,
        int maxDigits, string outputDirectory, PowerPointVideoExtractionResult result)
    {
        var slideEntry = archive.GetEntry(slidePartPath);
        if (slideEntry == null) return;

        XDocument slideDoc;
        using (var stream = slideEntry.Open()) slideDoc = XDocument.Load(stream);

        var videoFileElement = slideDoc.Descendants(A + "videoFile").FirstOrDefault();
        if (videoFileElement == null) return; // no video on this slide

        var rid = videoFileElement.Attribute(R + "link")?.Value;
        if (rid == null) return;

        var slashIndex = slidePartPath.LastIndexOf('/');
        var slideDir = slidePartPath[..slashIndex];
        var slideFileName = slidePartPath[(slashIndex + 1)..];
        var relsPartPath = $"{slideDir}/_rels/{slideFileName}.rels";

        var relsEntry = archive.GetEntry(relsPartPath);
        if (relsEntry == null)
        {
            result.Warnings.Add($"Slide {slideNumber}: video reference found but its relationships couldn't be read.");
            return;
        }

        XDocument relsDoc;
        using (var stream = relsEntry.Open()) relsDoc = XDocument.Load(stream);

        var relationship = relsDoc.Root?.Elements(Rel + "Relationship")
            .FirstOrDefault(e => e.Attribute("Id")?.Value == rid);
        if (relationship == null)
        {
            result.Warnings.Add($"Slide {slideNumber}: video reference found but its relationship couldn't be resolved.");
            return;
        }

        var targetMode = relationship.Attribute("TargetMode")?.Value;
        if (string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
        {
            result.Warnings.Add($"Slide {slideNumber}: video is linked to an external file and can't be imported.");
            return;
        }

        var target = relationship.Attribute("Target")?.Value;
        if (string.IsNullOrEmpty(target))
        {
            result.Warnings.Add($"Slide {slideNumber}: video reference is missing its target.");
            return;
        }

        var mediaPartPath = ResolveRelativePartPath(slideDir, target);
        var ext = Path.GetExtension(mediaPartPath).TrimStart('.').ToLowerInvariant();
        if (!SupportedVideoExtensions.Contains(ext))
        {
            result.Warnings.Add($"Slide {slideNumber}: video format '.{ext}' isn't supported.");
            return;
        }

        var mediaEntry = archive.GetEntry(mediaPartPath);
        if (mediaEntry == null)
        {
            result.Warnings.Add($"Slide {slideNumber}: embedded video file couldn't be found in the package.");
            return;
        }

        var paddedNumber = slideNumber.ToString(new string('0', maxDigits));
        var destPath = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.{ext}");
        using (var mediaStream = mediaEntry.Open())
        using (var destStream = File.Create(destPath))
        {
            mediaStream.CopyTo(destStream);
        }

        var siblingPng = Path.Combine(outputDirectory, $"Slide.{paddedNumber}.png");
        if (File.Exists(siblingPng)) File.Delete(siblingPng);

        result.SlideIndexToVideoFile[slideNumber] = destPath;
    }

    private static string ResolvePresentationRelTarget(string target) =>
        target.StartsWith('/') ? target.TrimStart('/') : "ppt/" + target;

    private static string ResolveRelativePartPath(string baseDir, string relativeTarget)
    {
        if (relativeTarget.StartsWith('/')) return relativeTarget.TrimStart('/');

        var combined = baseDir + "/" + relativeTarget;
        var stack = new List<string>();
        foreach (var segment in combined.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..")
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                stack.Add(segment);
            }
        }
        return string.Join('/', stack);
    }
}
