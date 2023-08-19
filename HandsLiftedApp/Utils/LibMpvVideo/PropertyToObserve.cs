using LibMpv.Client;

namespace HandsLiftedApp.Utils.LibMpvVideo;

public class PropertyToObserve
{
    public string MvvmName { get; set; }
    public string LibMpvName { get; set; }
    public mpv_format LibMpvFormat { get; set; }
}
