using System;
using System.Collections.Generic;
using System.Linq;

namespace HandsLiftedApp.Core.Data;

public record NoticeEntry(
    string Library,
    string License,
    string Copyright,
    string? Url = null);

public static class ThirdPartyNotices
{
    public static readonly IReadOnlyList<NoticeEntry> All =
        new NoticeEntry[]
        {
            new("AsyncImageLoader.Avalonia", "MIT",             "© AsyncImageLoader contributors"),
            new("Avalonia",                  "MIT",             "© Avalonia contributors",           "https://avaloniaui.net"),
            new("AvaloniaNDI",               "Public Domain + Ms-PL", "© AvaloniaNDI contributors"),
            new("BmpSharp",                  "MIT",             "© BmpSharp contributors"),
            new("ByteSize",                  "MIT",             "© Omar Khudeira"),
            new("Config.Net",                "MIT",             "© Ivan Gavryliuk"),
            new("DebounceThrottle",          "MIT",             "© DebounceThrottle contributors"),
            new("DynamicData",               "MIT",             "© Roland Pheasant",                 "https://github.com/reactivemarbles/DynamicData"),
            new("Google APIs (Drive/Slides)", "Apache 2.0",     "© Google LLC",                      "https://github.com/googleapis/google-api-dotnet-client"),
            new("HidApi.Net",                "MIT",             "© HidApi.Net contributors"),
            new("libmpv",                    "GPL 2.0+",        "© mpv contributors",                "https://mpv.io"),
            new("LibVLC / VideoLAN",         "LGPL 2.1+",       "© VideoLAN",                        "https://videolan.org"),
            new("LoadingIndicators.Avalonia", "MIT",            "© LoadingIndicators contributors"),
            new("Magick.NET",                "Apache 2.0",      "© Dirk Lemstra",                   "https://github.com/dlemstra/Magick.NET"),
            new("Material.Icons.Avalonia",   "MIT",             "© Material.Icons.Avalonia contributors"),
            new("NAudio",                    "Ms-PL",           "© Mark Heath",                      "https://github.com/naudio/NAudio"),
            new("NaturalSort.Extension",     "MIT",             "© NaturalSort contributors"),
            new("NDI SDK",                   "Proprietary",     "© Vizrt Group AS — includes RapidJSON (MIT), Speex (BSD), RapidXML (MIT), Opus (BSD), ASIO (Boost 1.0)"),
            new("Newtonsoft.Json",           "MIT",             "© James Newton-King",               "https://www.newtonsoft.com/json"),
            new("OpenMoji",                  "CC BY-SA 4.0",    "© OpenMoji contributors",           "https://openmoji.org"),
            new("PDFiumCore",                "BSD-3-Clause",    "© PDFium Authors"),
            new("protobuf-net",              "Apache 2.0",      "© Marc Gravell"),
            new("ReactiveUI",                "MIT",             "© ReactiveUI contributors",         "https://reactiveui.net"),
            new("RtfDomParser",              "MIT",             "© RtfDomParser contributors"),
            new("Serilog",                   "Apache 2.0",      "© Serilog contributors",            "https://serilog.net"),
            new("SIL.Scripture",             "MIT",             "© SIL International"),
            new("SkiaSharp",                 "MIT",             "© Microsoft",                       "https://github.com/mono/SkiaSharp"),
            new("Syncfusion",                "Commercial",      "© Syncfusion Inc.",                 "https://syncfusion.com"),
            new("System.Reactive",           "MIT",             "© .NET Foundation"),
            new("YamlDotNet",                "MIT",             "© Antoine Aubry",                   "https://github.com/aaubry/YamlDotNet"),
        }
        .OrderBy(e => e.Library, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
