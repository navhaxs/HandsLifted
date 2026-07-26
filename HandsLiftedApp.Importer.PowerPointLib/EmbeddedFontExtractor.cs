using System.IO.Compression;
using System.Xml.Linq;

namespace HandsLiftedApp.Importer.PowerPointLib;

/// <summary>
/// Pulls "regular" style embedded fonts (Insert &gt; Text &gt; ... &gt; Embed Fonts) straight out of a
/// .pptx package so they can be handed to Syncfusion's FontSettings.SubstituteFont event.
/// Without this, Syncfusion falls back to whatever font is installed on the machine doing the
/// conversion, which often looks wrong (wrong glyphs/metrics) for slides authored elsewhere.
/// </summary>
public static class EmbeddedFontExtractor
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Returns typeface name -> raw font file bytes, for every embedded font this pptx carries
    /// whose regular-style part could be read and looks like a real sfnt font.
    /// </summary>
    public static Dictionary<string, byte[]> ExtractRegularFonts(string pptxPath)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        using var archive = ZipFile.OpenRead(pptxPath);

        var presentationEntry = archive.GetEntry("ppt/presentation.xml");
        var relsEntry = archive.GetEntry("ppt/_rels/presentation.xml.rels");
        if (presentationEntry == null || relsEntry == null) return result;

        XDocument presentationDoc;
        using (var stream = presentationEntry.Open())
            presentationDoc = XDocument.Load(stream);

        XDocument relsDoc;
        using (var stream = relsEntry.Open())
            relsDoc = XDocument.Load(stream);

        var relationshipTargets = relsDoc.Root?
            .Elements(Rel + "Relationship")
            .Where(e => e.Attribute("Id") != null && e.Attribute("Target") != null)
            .ToDictionary(e => e.Attribute("Id")!.Value, e => e.Attribute("Target")!.Value);
        if (relationshipTargets == null) return result;

        var embeddedFontLst = presentationDoc.Root?.Element(P + "embeddedFontLst");
        if (embeddedFontLst == null) return result;

        foreach (var embeddedFont in embeddedFontLst.Elements(P + "embeddedFont"))
        {
            var typeface = embeddedFont.Element(P + "font")?.Attribute("typeface")?.Value;
            var regularRid = embeddedFont.Element(P + "regular")?.Attribute(R + "id")?.Value;
            if (string.IsNullOrEmpty(typeface) || string.IsNullOrEmpty(regularRid)) continue;
            if (!relationshipTargets.TryGetValue(regularRid, out var target)) continue;

            var fontEntry = archive.GetEntry(ResolvePartPath(target));
            if (fontEntry == null) continue;

            byte[] eotBytes;
            using (var fontStream = fontEntry.Open())
            using (var buffer = new MemoryStream())
            {
                fontStream.CopyTo(buffer);
                eotBytes = buffer.ToArray();
            }

            var fontBytes = UnwrapEot(eotBytes);
            if (fontBytes == null) continue;

            result[typeface] = fontBytes;
        }

        return result;
    }

    private static string ResolvePartPath(string target)
    {
        return target.StartsWith('/') ? target.TrimStart('/') : "ppt/" + target;
    }

    // PowerPoint stores embedded font parts as EOT (Embedded OpenType) containers: a variable-length
    // header (family/style name strings etc.) followed by the actual sfnt (TTF/OTF) font data.
    // See [MS-EOT]. Layout of the fields we need, all little-endian:
    //   0:  EOTSize        (u32) - total size of this file
    //   4:  FontDataSize   (u32) - size of the sfnt payload at the end of the file
    //   12: Flags          (u32) - bit 0x4 (TTEMBED_TTCOMPRESSED) means the payload is MicroType
    //                              Express compressed, which we can't decode - skip those.
    //   34: MagicNumber    (u16) - must be 0x504C ("PL")
    private const uint EotMagicNumber = 0x504C;
    private const uint TtCompressedFlag = 0x4;

    private static byte[]? UnwrapEot(byte[] data)
    {
        if (data.Length < 36) return null;

        uint eotSize = BitConverter.ToUInt32(data, 0);
        uint fontDataSize = BitConverter.ToUInt32(data, 4);
        uint flags = BitConverter.ToUInt32(data, 12);
        ushort magicNumber = BitConverter.ToUInt16(data, 34);

        if (magicNumber != EotMagicNumber) return null;
        if (eotSize != data.Length) return null;
        if (fontDataSize == 0 || fontDataSize > data.Length) return null;
        if ((flags & TtCompressedFlag) != 0) return null; // MTX-compressed - unsupported, skip

        var payload = new byte[fontDataSize];
        Array.Copy(data, data.Length - (int)fontDataSize, payload, 0, (int)fontDataSize);

        // Defense in depth: confirm the unwrapped payload is actually a font before handing it off.
        return LooksLikeSfnt(payload) ? payload : null;
    }

    private static bool LooksLikeSfnt(byte[] data)
    {
        if (data.Length < 4) return false;
        uint tag = (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        return tag switch
        {
            0x00010000 => true, // TrueType
            0x4F54544F => true, // 'OTTO' - OpenType/CFF
            0x74727565 => true, // 'true'
            0x74746366 => true, // 'ttcf' - TrueType collection
            _ => false
        };
    }
}
