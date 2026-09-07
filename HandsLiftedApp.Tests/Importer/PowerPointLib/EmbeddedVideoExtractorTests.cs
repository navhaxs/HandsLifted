using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Importer.PowerPointLib;

namespace HandsLiftedApp.Tests.Importer.PowerPointLib;

[TestClass]
public class EmbeddedVideoExtractorTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "EmbeddedVideoExtractorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private sealed record TestSlide(bool Hidden, string? VideoTarget, string? VideoTargetMode);

    private string CreateTestPptx(TestSlide[] slides)
    {
        var pptxPath = Path.Combine(_tempDir, "test.pptx");
        using (var archive = ZipFile.Open(pptxPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "ppt/presentation.xml", BuildPresentationXml(slides.Length));
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", BuildPresentationRelsXml(slides.Length));

            for (int i = 0; i < slides.Length; i++)
            {
                int slideNum = i + 1;
                var slide = slides[i];

                WriteEntry(archive, $"ppt/slides/slide{slideNum}.xml",
                    BuildSlideXml(slide.Hidden, hasVideo: slide.VideoTarget != null));

                if (slide.VideoTarget != null)
                {
                    WriteEntry(archive, $"ppt/slides/_rels/slide{slideNum}.xml.rels",
                        BuildSlideRelsXml(slide.VideoTarget, slide.VideoTargetMode));
                }
            }

            WriteEntry(archive, "ppt/media/media1.mp4", "dummy-mp4-bytes");
            WriteEntry(archive, "ppt/media/media1.mpg", "dummy-mpg-bytes");
        }
        return pptxPath;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static string BuildPresentationXml(int slideCount)
    {
        var sldIds = string.Join("", Enumerable.Range(0, slideCount)
            .Select(i => $"<p:sldId id=\"{256 + i}\" r:id=\"rId{2 + i}\"/>"));
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                             xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:sldIdLst>{sldIds}</p:sldIdLst>
            </p:presentation>
            """;
    }

    private static string BuildPresentationRelsXml(int slideCount)
    {
        var rels = string.Join("", Enumerable.Range(0, slideCount)
            .Select(i => $"<Relationship Id=\"rId{2 + i}\" " +
                "Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" " +
                $"Target=\"slides/slide{i + 1}.xml\"/>"));
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{rels}</Relationships>
            """;
    }

    private static string BuildSlideXml(bool hidden, bool hasVideo)
    {
        var showAttr = hidden ? " show=\"0\"" : "";
        var videoElement = hasVideo
            ? """<p:pic><p:nvPicPr><p:nvPr><a:videoFile r:link="rId1"/></p:nvPr></p:nvPicPr></p:pic>"""
            : "";
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sld{showAttr}
                   xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                   xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:cSld><p:spTree>{videoElement}</p:spTree></p:cSld>
            </p:sld>
            """;
    }

    private static string BuildSlideRelsXml(string videoTarget, string? targetMode)
    {
        var targetModeAttr = targetMode != null ? $" TargetMode=\"{targetMode}\"" : "";
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/video" Target="{videoTarget}"{targetModeAttr}/>
            </Relationships>
            """;
    }

    [TestMethod]
    public void ExtractVideos_NoVideoAnywhere_ReturnsEmptyResult()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),
        });

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.SlideIndexToVideoFile.Count);
        Assert.AreEqual(0, result.Warnings.Count);
    }

    [TestMethod]
    public void ExtractVideos_SingleEmbeddedVideo_ExtractsAndDeletesSiblingPng()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "../media/media1.mp4", VideoTargetMode: null),
        });
        var siblingPng = Path.Combine(_tempDir, "Slide.1.png");
        File.WriteAllText(siblingPng, "png-bytes"); // simulates the PDF/PNG step having already run

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.Warnings.Count);
        Assert.IsTrue(result.SlideIndexToVideoFile.ContainsKey(1));
        var extractedPath = result.SlideIndexToVideoFile[1];
        Assert.AreEqual(Path.Combine(_tempDir, "Slide.1.mp4"), extractedPath);
        Assert.AreEqual("dummy-mp4-bytes", File.ReadAllText(extractedPath));
        Assert.IsFalse(File.Exists(siblingPng));
    }

    [TestMethod]
    public void ExtractVideos_HiddenSlideBeforeVideoSlide_NumbersByShownPositionNotPhysicalPosition()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: null, VideoTargetMode: null),                    // shown #1
            new TestSlide(Hidden: true, VideoTarget: null, VideoTargetMode: null),                     // hidden, not numbered
            new TestSlide(Hidden: false, VideoTarget: "../media/media1.mp4", VideoTargetMode: null),   // shown #2
        });

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.Warnings.Count);
        Assert.IsTrue(result.SlideIndexToVideoFile.ContainsKey(2));
        Assert.IsFalse(result.SlideIndexToVideoFile.ContainsKey(3));
        Assert.AreEqual(Path.Combine(_tempDir, "Slide.2.mp4"), result.SlideIndexToVideoFile[2]);
    }

    [TestMethod]
    public void ExtractVideos_ExternallyLinkedVideo_KeepsPngAndRecordsWarning()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "file:///C:/videos/clip.mp4", VideoTargetMode: "External"),
        });
        var siblingPng = Path.Combine(_tempDir, "Slide.1.png");
        File.WriteAllText(siblingPng, "png-bytes");

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.SlideIndexToVideoFile.Count);
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], "linked to an external file");
        Assert.IsTrue(File.Exists(siblingPng));
    }

    [TestMethod]
    public void ExtractVideos_UnsupportedVideoExtension_KeepsPngAndRecordsWarning()
    {
        var pptxPath = CreateTestPptx(new[]
        {
            new TestSlide(Hidden: false, VideoTarget: "../media/media1.mpg", VideoTargetMode: null),
        });
        var siblingPng = Path.Combine(_tempDir, "Slide.1.png");
        File.WriteAllText(siblingPng, "png-bytes");

        var result = EmbeddedVideoExtractor.ExtractVideos(pptxPath, _tempDir);

        Assert.AreEqual(0, result.SlideIndexToVideoFile.Count);
        Assert.AreEqual(1, result.Warnings.Count);
        StringAssert.Contains(result.Warnings[0], "isn't supported");
        Assert.IsTrue(File.Exists(siblingPng));
    }
}
