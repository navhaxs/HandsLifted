using LibVLCSharp.Shared;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TestVlcApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await new TestVLC().Run();
            Console.ReadKey();
        }
    }

    class TestVLC
    {
        private MediaPlayer.LibVLCVideoFormatCb _videoFormat;
        private MediaPlayer.LibVLCVideoLockCb _lockCB;
        private MediaPlayer.LibVLCVideoUnlockCb _unlockCB;
        private MediaPlayer.LibVLCVideoDisplayCb _displayCB;
        private MediaPlayer.LibVLCVideoCleanupCb _cleanupVideoCB;

        private MediaPlayer _mediaPlayer = null;
        private GCHandle? _imageData = null;

        private Size _size;

        public async Task Run()
        {
            Core.Initialize();

            _videoFormat = VideoFormat;
            _lockCB = LockVideo;
            _unlockCB = UnlockVideo;
            _displayCB = DisplayVideo;
            _cleanupVideoCB = CleanupVideo;

            var libVLC = new LibVLC();
            libVLC.Log += _libVLC_Log;
            var media = new Media(libVLC, "screen://", FromType.FromLocation);


            _mediaPlayer = new MediaPlayer(media);

            _mediaPlayer.SetVideoFormatCallbacks(_videoFormat, _cleanupVideoCB); // replace _cleanupVideoCB by null, no crash
            _mediaPlayer.SetVideoCallbacks(_lockCB, _unlockCB, _displayCB);

            _mediaPlayer.EncounteredError += (sender, e) =>
            {
                Cleanup();
            };

            _mediaPlayer.EndReached += (sender, e) =>
            {
                Cleanup();
            };

            _mediaPlayer.Stopped += (sender, e) =>
            {
                Cleanup();
            };

            _mediaPlayer.Play();
            await Task.Delay(3000);

            _mediaPlayer.Stop(); // crashes here
        }

        private static void _libVLC_Log(object sender, LogEventArgs e)
        {
            Debug.WriteLine("vlc: " + e.Message);
        }

        private void Cleanup()
        {

        }

        private IntPtr LockVideo(IntPtr userdata, IntPtr planes)
        {
            return userdata;
        }
        private void UnlockVideo(IntPtr opaque, IntPtr picture, IntPtr planes)
        {
        }

        private void CleanupVideo(ref IntPtr opaque)
        {

        }

        private void DisplayVideo(IntPtr userdata, IntPtr picture)
        {

        }

        /// <summary>
        /// Called by vlc when the video format is needed. This method allocats the picture buffers for vlc and tells it to set the chroma to RV32
        /// </summary>
        /// <param name="userdata">The user data that will be given to the <see cref="LockVideo"/> callback. It contains the pointer to the buffer</param>
        /// <param name="chroma">The chroma</param>
        /// <param name="width">The visible width</param>
        /// <param name="height">The visible height</param>
        /// <param name="pitches">The buffer width</param>
        /// <param name="lines">The buffer height</param>
        /// <returns>The number of buffers allocated</returns>
        private uint VideoFormat(ref IntPtr userdata, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            return 1;
        }
    }
}
