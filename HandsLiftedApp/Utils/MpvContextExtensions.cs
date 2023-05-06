using LibMpv.Client;
using System.Drawing;
using System.Drawing.Imaging;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace HandsLiftedApp.Utils
{
    internal static class MpvContextExtensions
    {
        public static void Play(this MpvContext mpvContext)
        {
            mpvContext.SetPropertyFlag("pause", false);
        }

        public static void Pause(this MpvContext mpvContext)
        {
            mpvContext.SetPropertyFlag("pause", true);
        }

        public static void Stop(this MpvContext mpvContext)
        {
            mpvContext.Command("stop");
        }

        public static void Load(this MpvContext mpvContext, string MediaUrl)
        {
            mpvContext.Command("loadfile", MediaUrl, "replace");
        }
    }
}
