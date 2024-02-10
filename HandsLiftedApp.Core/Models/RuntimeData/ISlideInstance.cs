using Avalonia.Media.Imaging;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    public interface ISlideInstance
    {
        Bitmap? Cached { get; set; }
    }
}