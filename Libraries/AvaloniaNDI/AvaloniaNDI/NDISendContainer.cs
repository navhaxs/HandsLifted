using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NAudio.Wave;
using NewTek;
using NewTek.NDI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace AvaloniaNDI
{
    public class NDISendContainer : Viewbox, INotifyPropertyChanged, IDisposable
    {
        [Category("NewTek NDI"),
        Description("NDI output width in pixels. Required.")]
        public int NdiWidth
        {
            get { return _NdiWidth; }
            set { SetAndRaise(NdiWidthProperty, ref _NdiWidth, value); }
        }
        private int _NdiWidth = 1280;
        public static readonly DirectProperty<NDISendContainer, int> NdiWidthProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, int>(nameof(NdiWidth), o => o.NdiWidth, (o, v) => o.NdiWidth = v);

        [Category("NewTek NDI"),
        Description("NDI output height in pixels. Required.")]
        public int NdiHeight
        {
            get { return _NdiHeight; }
            set { SetAndRaise(NdiHeightProperty, ref _NdiHeight, value); }
        }
        private int _NdiHeight = 720;
        public static readonly DirectProperty<NDISendContainer, int> NdiHeightProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, int>(nameof(NdiHeight), o => o.NdiHeight, (o, v) => o.NdiHeight = v);


        [Category("NewTek NDI"),
        Description("NDI output frame rate numerator. Required.")]
        public int NdiFrameRateNumerator
        {
            get { return _NdiFrameRateNumerator; }
            set { SetAndRaise(NdiFrameRateNumeratorProperty, ref _NdiFrameRateNumerator, value); }
        }
        private int _NdiFrameRateNumerator = 60000;
        public static readonly DirectProperty<NDISendContainer, int> NdiFrameRateNumeratorProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, int>(nameof(NdiFrameRateNumerator), o => o.NdiFrameRateNumerator, (o, v) => o.NdiFrameRateNumerator = v);

        [Category("NewTek NDI"),
        Description("NDI output frame rate denominator. Required.")]
        public int NdiFrameRateDenominator
        {
            get { return _NdiFrameRateDenominator; }
            set { SetAndRaise(NdiFrameRateDenominatorProperty, ref _NdiFrameRateDenominator, value); }
        }
        private int _NdiFrameRateDenominator = 1000;
        public static readonly DirectProperty<NDISendContainer, int> NdiFrameRateDenominatorProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, int>(nameof(NdiFrameRateDenominator), o => o.NdiFrameRateDenominator, (o, v) => o.NdiFrameRateDenominator = v);


        [Category("NewTek NDI"),
        Description("NDI output name as displayed to receivers. Required.")]
        public string NdiName
        {
            get { return _NdiName; }
            set { SetAndRaise(NdiNameProperty, ref _NdiName, value); }
        }
        private string _NdiName = "Unnamed - Fix Me.";
        public static readonly DirectProperty<NDISendContainer, string> NdiNameProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, string>(nameof(NdiName), o => o.NdiName, (o, v) => { o.NdiName = v; });

        [Category("NewTek NDI"),
Description("Function to determine whether the content requires high resolution NDI frame updates (i.e. is an animation or video playback). Optional.")]
        public Func<NDISendContainer, bool> IsContentHighResCheckFunc
        {
            get { return _IsContentHighResCheckFunc; }
            set { SetAndRaise(IsContentHighResCheckFuncProperty, ref _IsContentHighResCheckFunc, value); }
        }
        private Func<NDISendContainer, bool> _IsContentHighResCheckFunc = null;
        public static readonly DirectProperty<NDISendContainer, Func<NDISendContainer, bool>> IsContentHighResCheckFuncProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, Func<NDISendContainer, bool>>(nameof(IsContentHighResCheckFunc), o => o.IsContentHighResCheckFunc, (o, v) => { o.IsContentHighResCheckFunc = v; });

        [Category("NewTek NDI"),
        Description("NDI groups this sender will belong to. Optional.")]
        public List<string> NdiGroups
        {
            get { return _NdiGroups; }
            set { SetAndRaise(NdiGroupsProperty, ref _NdiGroups, value); }
        }
        private List<string> _NdiGroups = new List<string>();
        public static readonly DirectProperty<NDISendContainer, List<string>> NdiGroupsProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, List<string>>(nameof(NdiGroups), o => o.NdiGroups, (o, v) => { o.NdiGroups = v; });

        [Category("NewTek NDI"),
        Description("If clocked to video, NDI will rate limit drawing to the specified frame rate. Defaults to true.")]
        public bool NdiClockToVideo
        {
            get { return _NdiClockToVideo; }
            set { SetAndRaise(NdiClockToVideoProperty, ref _NdiClockToVideo, value); }
        }
        private bool _NdiClockToVideo = true;
        public static readonly DirectProperty<NDISendContainer, bool> NdiClockToVideoProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, bool>(nameof(NdiClockToVideo), o => o.NdiClockToVideo, (o, v) => { o.NdiClockToVideo = v; });

        [Category("NewTek NDI"),
        Description("True if some receiver has this source on program out.")]
        public bool IsOnProgram
        {
            get { return _IsOnProgram; }
            set { SetAndRaise(IsOnProgramProperty, ref _IsOnProgram, value); }
        }
        private bool _IsOnProgram = false;
        public static readonly DirectProperty<NDISendContainer, bool> IsOnProgramProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, bool>(nameof(IsOnProgram), o => o.IsOnProgram, (o, v) => { o.IsOnProgram = v; });

        [Category("NewTek NDI"),
        Description("True if some receiver has this source on preview out.")]
        public bool IsOnPreview
        {
            get { return _IsOnPreview; }
            set { SetAndRaise(IsOnPreviewProperty, ref _IsOnPreview, value); }
        }
        private bool _IsOnPreview = false;
        public static readonly DirectProperty<NDISendContainer, bool> IsOnPreviewProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, bool>(nameof(IsOnPreview), o => o.IsOnPreview, (o, v) => { o.IsOnPreview = v; });


        [Category("NewTek NDI"),
        Description("If True, the send thread does not send, taking no CPU time.")]
        public bool IsSendPaused
        {
            get { return isPausedValue; }
            set
            {
                if (value != isPausedValue)
                {
                    SetAndRaise(IsSendPausedProperty, ref isPausedValue, value);
                }
            }
        }
        public static readonly DirectProperty<NDISendContainer, bool> IsSendPausedProperty =
            AvaloniaProperty.RegisterDirect<NDISendContainer, bool>(nameof(IsSendPaused), o => o.IsSendPaused, (o, v) => { o.IsSendPaused = v; });


        [Category("NewTek NDI"),
        Description("Send System Audio")]
        public bool SendSystemAudio
        {
            get { return sendSystemAudio; }
            set
            {
                if (value != sendSystemAudio)
                {
                    if (value)
                    {
                        try
                        {
                            audioCap = new WasapiLoopbackCapture();
                            audioCap.StartRecording();
                            audioSampleRate = audioCap.WaveFormat.SampleRate;
                            audioSampleSizeInBytes = audioCap.WaveFormat.BitsPerSample / 8;
                            audioNumChannels = audioCap.WaveFormat.Channels;

                            audioCap.DataAvailable += AudioCap_DataAvailable;
                        }
                        catch
                        {
                            // loopback capture may not be available on all systems
                            value = false;
                        }
                    }
                    else
                    {
                        if (audioCap != null)
                        {
                            if (audioCap.CaptureState == NAudio.CoreAudioApi.CaptureState.Capturing)
                            {
                                audioCap.StopRecording();

                                while (audioCap.CaptureState != NAudio.CoreAudioApi.CaptureState.Stopped)
                                {
                                    Thread.Sleep(10);
                                }
                            }

                            audioCap.Dispose();
                            audioCap = null;
                        }
                    }

                    sendSystemAudio = value;
                    NotifyPropertyChanged("SendSystemAudio");
                }
            }
        }

        private void AudioCap_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (isPausedValue || sendInstancePtr == IntPtr.Zero)
                return;

            // how many samples?
            int numSamples = (e.BytesRecorded / (audioNumChannels * audioSampleSizeInBytes));

            // how much float buffer will this need?
            int bufferSizeNeeded = numSamples * audioNumChannels * sizeof(float);

            // is our audio frame big enough? too big is fine
            if (audioBufferSize < bufferSizeNeeded || audioFrame.p_data == IntPtr.Zero)
            {
                if (audioFrame.p_data != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(audioFrame.p_data);

                    audioFrame.p_data = IntPtr.Zero;
                }

                audioFrame.p_data = Marshal.AllocHGlobal(bufferSizeNeeded);

                audioBufferSize = bufferSizeNeeded;
            }

            // set these every time because why not?
            audioFrame.sample_rate = audioSampleRate;
            audioFrame.no_channels = audioNumChannels;
            audioFrame.no_samples = numSamples;

            // pin the byte[] audio received and get a GC handle to it
            GCHandle interleavedHandle = GCHandle.Alloc(e.Buffer, GCHandleType.Pinned);

            if (audioSampleSizeInBytes == 2)
            {
                // make an temporary interleaved NDI audio frame around the received samples
                NDIlib.audio_frame_interleaved_16s_t interleavedShortFrame = new NDIlib.audio_frame_interleaved_16s_t()
                {
                    sample_rate = audioSampleRate,
                    no_channels = audioNumChannels,
                    no_samples = numSamples,
                    p_data = interleavedHandle.AddrOfPinnedObject()
                };

                // Convert from s16 interleaved to float planar audio
                NDIlib.util_audio_from_interleaved_16s_v2(ref interleavedShortFrame, ref audioFrame);
            }
            else if (audioSampleSizeInBytes == 4)
            {
                // make an temporary interleaved NDI audio frame around the received samples
                NDIlib.audio_frame_interleaved_32f_t interleavedFloatFrame = new NDIlib.audio_frame_interleaved_32f_t()
                {
                    sample_rate = audioSampleRate,
                    no_channels = audioNumChannels,
                    no_samples = numSamples,
                    p_data = interleavedHandle.AddrOfPinnedObject()
                };

                // Convert from float interleaved to float planar audio
                NDIlib.util_audio_from_interleaved_32f_v2(ref interleavedFloatFrame, ref audioFrame);
            }
            else
            {
                Debug.Assert(false, "Unexpected audio sample size.");
            }

            // release the GC pinning of the byte[]'s
            interleavedHandle.Free();

            lock (sendInstanceLock)
            {
                // send the planar frame
                if (sendInstancePtr != IntPtr.Zero)
                {
                    if (!IsSendPaused)
                    {
                        NDIlib.send_send_audio_v2(sendInstancePtr, ref audioFrame);
                    }
                }
            }
        }

        // TODO this was from WPF. Is there an Avalonia way to do this?
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public NDISendContainer()
        {
            if (Design.IsDesignMode)
                return;

            // start up a thread to receive on
            sendThread = new Thread(SendThreadProc) { IsBackground = true, Name = "WpfNdiSendThread" };
            sendThread.Start();

            this.LayoutUpdated += NDISendContainer_LayoutUpdated;

            try
            {
                // Not required, but "correct". (see the SDK documentation)
                if (!NDIlib.initialize())
                {
                    // Cannot run NDI. Most likely because the CPU is not sufficient (see SDK documentation).
                    // you can check this directly with a call to NDIlib.is_supported_CPU()
                    //MessageBox.Show("Cannot run NDI");
                }

                NdiNameProperty.Changed.Subscribe(OnNdiSenderPropertyChanged);
                NdiGroupsProperty.Changed.Subscribe(OnNdiSenderPropertyChanged);
                NdiClockToVideoProperty.Changed.Subscribe(OnNdiSenderPropertyChanged);

                InitializeNdi();
            }
            catch (DllNotFoundException ex)
            {
                return;
            }
            catch (Exception)
            {
                // Cannot run NDI. Most likely because the CPU is not sufficient (see SDK documentation).
                // you can check this directly with a call to NDIlib.is_supported_CPU()
                //MessageBox.Show("Cannot run NDI");
                throw;
            }

            this.AttachedToVisualTree += NDISendContainer_AttachedToVisualTree;
            this.DetachedFromVisualTree += NDISendContainer_DetachedFromVisualTree;

        }

        private void NDISendContainer_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            GetNextRenderTick();
        }

        void GetNextRenderTick()
        {
            if (!_disposed)
            {
                var topLevel = Window.GetTopLevel(this);
                if (topLevel != null)
                {
                    topLevel.RequestAnimationFrame((TimeSpan s) =>
                    {
                        OnCompositionTargetRendering();
                        GetNextRenderTick();
                    });
                }
            }
        }

        private void NDISendContainer_LayoutUpdated(object sender, EventArgs e)
        {
        }
        private void NDISendContainer_DetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (Design.IsDesignMode)
                return;

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~NDISendContainer()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                exitThread = true;
                pendingFrames.CompleteAdding();

                // clean up the audio capture if needed
                if (audioCap != null)
                {
                    audioCap.StopRecording();
                }

                ReleaseBuffers();

                if (disposing)
                {
                    // tell the thread to exit
                    exitThread = true;

                    // wait for it to exit (bounded: TryTake timeout=250ms + one
                    // send_send_video_v2 call with NdiClockToVideo=true ≈ 16ms)
                    if (sendThread != null)
                    {
                        if (!sendThread.Join(2000))
                            Log.Warning("[NDI] Send thread did not exit within 2 s during dispose");

                        sendThread = null;
                    }

                    // clear any pending frames
                    while (pendingFrames.Count > 0)
                    {
                        if (pendingFrames.TryTake(out var discardFrame))
                        {
                            _freeBuffers.Add(discardFrame.p_data);
                        }
                    }

                    pendingFrames.Dispose();
                }

                // Destroy the NDI sender
                lock (sendInstanceLock)
                {
                    if (sendInstancePtr != IntPtr.Zero)
                    {
                        NDIlib.send_destroy(sendInstancePtr);

                        sendInstancePtr = IntPtr.Zero;
                    }
                }

                try
                {
                    // Not required, but "correct". (see the SDK documentation)
                    NDIlib.destroy();
                }
                catch (DllNotFoundException) { }

                _disposed = true;
            }
        }

        private bool _disposed = false;

        private const int BufferPoolSize = 3;
        private readonly List<IntPtr> _bufferPool = new();
        private readonly BlockingCollection<IntPtr> _freeBuffers = new();
        private RenderTargetBitmap rtb;

        // Separate buffer (outside the send pool) that holds a copy of the last
        // non-blank rendered frame. Used to substitute black frames produced by
        // Avalonia crossfade transitions fading fully to transparent/black.
        private IntPtr _lastGoodFramePtr = IntPtr.Zero;
        private int _lastGoodFrameSize = 0;
        private bool _hasLastGoodFrame = false;

        private void ReleaseBuffers()
        {
            lock (sendInstanceLock)
            {
                while (_freeBuffers.TryTake(out _)) { }
                foreach (var ptr in _bufferPool)
                {
                    Marshal.FreeHGlobal(ptr);
                }
                _bufferPool.Clear();

                if (rtb != null)
                {
                    rtb.Dispose();
                    rtb = null;
                }

                if (_lastGoodFramePtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_lastGoodFramePtr);
                    _lastGoodFramePtr = IntPtr.Zero;
                    _lastGoodFrameSize = 0;
                    _hasLastGoodFrame = false;
                }
            }
        }

        private void EnsureBuffers(int width, int height)
        {
            // stride: 32bpp = 4 bytes per pixel
            int newStride = width * 4;
            int newBufferSize = height * newStride;

            if (_bufferPool.Count > 0 && bufferSize == newBufferSize && stride == newStride)
                return;

            ReleaseBuffers();

            stride = newStride;
            bufferSize = newBufferSize;
            aspectRatio = (float)width / (float)height;

            for (int i = 0; i < BufferPoolSize; i++)
            {
                IntPtr ptr = Marshal.AllocHGlobal(bufferSize);
                _bufferPool.Add(ptr);
                _freeBuffers.Add(ptr);
            }

            _lastGoodFramePtr = Marshal.AllocHGlobal(bufferSize);
            _lastGoodFrameSize = bufferSize;
            _hasLastGoodFrame = false;

            var scale = 1;
            rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale));
        }

        // Sample every Nth pixel and check luma; returns true when the frame is
        // almost entirely black (i.e. produced by a fully-faded crossfade).
        private static unsafe bool IsLikelyBlankFrame(byte* pixels, int byteCount)
        {
            const int step = 32 * 4; // sample every 32nd pixel
            const int lumaThreshold = 8;
            int samples = 0, brightSamples = 0;

            for (int i = 0; i <= byteCount - 4; i += step)
            {
                int luma = (pixels[i + 2] * 54 + pixels[i + 1] * 183 + pixels[i] * 19) >> 8; // R·54 + G·183 + B·19
                if (luma > lumaThreshold) brightSamples++;
                samples++;
            }

            // blank only when almost no sampled pixel has visible brightness
            return samples > 0 && brightSamples <= Math.Max(1, samples / 100);
        }

        private static readonly TimeSpan _staticThrottleInterval = TimeSpan.FromMilliseconds(500);
        private static readonly NDIlib.FourCC_type_e _nativeFourCC =
            OperatingSystem.IsMacOS() ? NDIlib.FourCC_type_e.FourCC_type_RGBA : NDIlib.FourCC_type_e.FourCC_type_BGRA;

        private DateTime _lastFrameRenderTime = DateTime.MinValue;
        private IGetVideoBufferBitmap? _cachedVideoControl;
        private Visual? _lastChild;
        private bool _hadConnections = false;

        private unsafe void OnCompositionTargetRendering()
        {
            if (IsSendPaused || sendInstancePtr == IntPtr.Zero || this.Child == null)
                return;

            // Skip all rendering work when no NDI receivers are connected.
            // send_get_no_connections with timeout=0 is non-blocking (polls current count).
            // When a new client connects after a period of no connections, reset the
            // static-content throttle timestamp so the first frame is sent immediately
            // rather than waiting up to 500 ms.
            int connections = NDIlib.send_get_no_connections(sendInstancePtr, 0);
            if (connections == 0)
            {
                _hadConnections = false;
                return;
            }
            if (!_hadConnections)
            {
                _lastFrameRenderTime = DateTime.MinValue; // force immediate capture on reconnect
                _hadConnections = true;
            }

            // Detect child change BEFORE the throttle check so a new slide immediately bypasses
            // the 500ms static-content throttle and is captured without delay.
            if (_lastChild != this.Child)
            {
                _lastChild = this.Child;
                // Only use the video buffer shortcut when the video control IS the container's
                // direct child (sole content, no overlay layers). If the video is nested inside
                // a Grid with other controls (e.g. motion background + SlideCanvas lyrics), the
                // shortcut would miss the overlays — require full composited render instead.
                var videoControls = this.Child.FindAllVisuals<IGetVideoBufferBitmap>().ToList();
                _cachedVideoControl = (videoControls.Count == 1 && ReferenceEquals(videoControls[0], this.Child))
                    ? videoControls[0]
                    : null;
                _lastFrameRenderTime = DateTime.MinValue; // force immediate capture on content switch
            }

            // Re-evaluate the shortcut at runtime: if the cached control's bitmap is null
            // (not yet rendering), or if multiple video controls now have active bitmaps,
            // fall back to full render for this frame.
            Bitmap? videoShortcutBitmap = null;
            if (_cachedVideoControl != null)
            {
                videoShortcutBitmap = _cachedVideoControl.GetVideoBufferBitmap();
            }

            // Throttling for static content
            if (IsContentHighResCheckFunc != null && !IsContentHighResCheckFunc(this))
            {
                // Throttle static content to ~2fps (500ms)
                if (DateTime.UtcNow - _lastFrameRenderTime < _staticThrottleInterval)
                    return;
            }

            // NOTE: HasJobsWithPriority(DispatcherPriority.Render) has been intentionally removed.
            // During slide transitions Avalonia's animation system continuously queues Render-priority
            // dispatcher jobs on every compositor tick. That check therefore returned true on every
            // frame while any transition was active, preventing all frames from reaching the send
            // thread and causing Studio Monitor to go black. RequestAnimationFrame already fires after
            // composition, so the visual tree is always in a fully laid-out state at this point.

            int xres = NdiWidth;
            int yres = NdiHeight;

            if (xres < 8 || yres < 8)
                return;

            EnsureBuffers(xres, yres);

            if (!_freeBuffers.TryTake(out IntPtr bufferPtr))
            {
                // Pool exhausted, skip frame
                return;
            }

            try
            {
                Bitmap? sourceBitmap = videoShortcutBitmap;

                if (sourceBitmap == null)
                {
                    rtb.Render(this.Child);
                    sourceBitmap = rtb;
                }

                try
                {
                    sourceBitmap.CopyPixels(new PixelRect(0, 0, xres, yres), bufferPtr, bufferSize, stride);
                }
                catch (NullReferenceException)
                {
                    // Bitmap's internal buffer not yet initialized (race during video context setup).
                    // Skip this frame silently.
                    _freeBuffers.Add(bufferPtr);
                    return;
                }

                // Force all pixels to fully opaque. Avalonia crossfade/opacity transitions produce
                // semi-transparent pixels (premultiplied BGRA alpha=0) that NDI receivers composite
                // against their black background, causing brief black flashes during rapid changes.
                // Compositing premultiplied alpha over black = keep RGB as-is, set A = 0xFF.
                bool isBlank;
                unsafe
                {
                    byte* p = (byte*)bufferPtr;
                    for (int i = 3; i < bufferSize; i += 4)
                        p[i] = 0xFF;

                    isBlank = IsLikelyBlankFrame(p, bufferSize);
                }

                if (isBlank && _hasLastGoodFrame && _lastGoodFramePtr != IntPtr.Zero && _lastGoodFrameSize == bufferSize
                    && IsContentHighResCheckFunc != null && IsContentHighResCheckFunc(this))
                {
                    // This frame is blank during an active transition (crossfade faded out, new content
                    // not yet visible). Substitute the last known-good frame so NDI receivers see the
                    // previous slide held rather than a black flash.
                    // Guard on IsContentHighResCheckFunc so intentional blank state is NOT substituted —
                    // otherwise NDI would show the last slide indefinitely when the user presses Blank.
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)_lastGoodFramePtr, (void*)bufferPtr, bufferSize, bufferSize);
                    }
                }
                else if (!isBlank && _lastGoodFramePtr != IntPtr.Zero && _lastGoodFrameSize == bufferSize)
                {
                    // This is a valid frame — update the last-good-frame store.
                    unsafe
                    {
                        Buffer.MemoryCopy((void*)bufferPtr, (void*)_lastGoodFramePtr, bufferSize, bufferSize);
                    }
                    _hasLastGoodFrame = true;
                }

                NDIlib.video_frame_v2_t videoFrame = new NDIlib.video_frame_v2_t()
                {
                    xres = xres,
                    yres = yres,
                    FourCC = _nativeFourCC,
                    frame_rate_N = NdiFrameRateNumerator,
                    frame_rate_D = NdiFrameRateDenominator,
                    picture_aspect_ratio = aspectRatio,
                    frame_format_type = NDIlib.frame_format_type_e.frame_format_type_progressive,
                    timecode = NDIlib.send_timecode_synthesize,
                    p_data = bufferPtr,
                    line_stride_in_bytes = stride,
                };

                if (pendingFrames.TryAdd(videoFrame))
                {
                    // Only stamp the time when a frame actually reaches the queue.
                    // Stamping before the enqueue caused the static throttle to believe frames
                    // were being delivered even when the send thread was starved.
                    _lastFrameRenderTime = DateTime.UtcNow;
                }
                else
                {
                    _freeBuffers.Add(bufferPtr);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to copy pixels to NDI buffer");
                _freeBuffers.Add(bufferPtr);
            }
        }

        private static void OnNdiSenderPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            NDISendContainer s = e.Sender as NDISendContainer;
            if (s != null)
                s.InitializeNdi();
        }

        private void InitializeNdi()
        {
            if (Design.IsDesignMode)
                return;

            lock (sendInstanceLock)
            {
                // we need a name
                if (String.IsNullOrEmpty(NdiName))
                    return;

                // re-initialize?
                if (sendInstancePtr != IntPtr.Zero)
                {
                    NDIlib.send_destroy(sendInstancePtr);
                    sendInstancePtr = IntPtr.Zero;
                }

                // .Net interop doesn't handle UTF-8 strings, so do it manually
                // These must be freed later
                IntPtr sourceNamePtr = UTF.StringToUtf8(NdiName);

                IntPtr groupsNamePtr = IntPtr.Zero;

                // build a comma separated list of groups?
                if (NdiGroups.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < NdiGroups.Count; i++)
                    {
                        sb.Append(NdiGroups[i]);

                        if (i < NdiGroups.Count - 1)
                            sb.Append(',');
                    }

                    groupsNamePtr = UTF.StringToUtf8(sb.ToString());
                }

                // Create an NDI source description using sourceNamePtr and it's clocked to the video.
                NDIlib.send_create_t createDesc = new NDIlib.send_create_t()
                {
                    p_ndi_name = sourceNamePtr,
                    p_groups = groupsNamePtr,
                    clock_video = NdiClockToVideo,
                    clock_audio = false
                };

                // We create the NDI finder instance
                sendInstancePtr = NDIlib.send_create(ref createDesc);

                // free the strings we allocated
                Marshal.FreeHGlobal(sourceNamePtr);
                Marshal.FreeHGlobal(groupsNamePtr);
            }
        }

        // the receive thread runs though this loop until told to exit
        private void SendThreadProc()
        {
            // look for changes in tally
            bool lastProg = false;
            bool lastPrev = false;

            NDIlib.tally_t tally = new NDIlib.tally_t();
            tally.on_program = lastProg;
            tally.on_preview = lastPrev;

            while (!exitThread)
            {
                NDIlib.video_frame_v2_t frame;
                if (pendingFrames.TryTake(out frame, 250))
                {
                    // Drain any additional queued frames, always upgrading to the NEWEST.
                    // BlockingCollection uses FIFO order, so each TryTake here gives a frame
                    // rendered more recently. Return the older buffer to the free pool.
                    NDIlib.video_frame_v2_t newerFrame;
                    while (pendingFrames.TryTake(out newerFrame))
                    {
                        _freeBuffers.Add(frame.p_data); // return older buffer to pool
                        frame = newerFrame;              // upgrade to the more recent frame
                    }

                    lock (sendInstanceLock)
                    {
                        // if this is not here, then we must be being reconfigured
                        if (sendInstancePtr == IntPtr.Zero || IsSendPaused)
                        {
                            _freeBuffers.Add(frame.p_data);
                        }
                        else
                        {
                            try
                            {

                                NDIlib.send_send_video_v2(sendInstancePtr, ref frame);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Error in NDI SendThreadProc - sending video");
                            }
                            finally
                            {
                                _freeBuffers.Add(frame.p_data);
                            }
                        }
                    }
                }

                // check tally
                IntPtr currentSendInstancePtr;
                lock (sendInstanceLock)
                {
                    currentSendInstancePtr = sendInstancePtr;
                }

                if (currentSendInstancePtr != IntPtr.Zero)
                {
                    NDIlib.send_get_tally(currentSendInstancePtr, ref tally, 0);

                    // if tally changed trigger an update
                    if (lastProg != tally.on_program || lastPrev != tally.on_preview)
                    {
                        // save the last values
                        lastProg = tally.on_program;
                        lastPrev = tally.on_preview;

                        // set these on the UI thread
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            IsOnProgram = lastProg;
                            IsOnPreview = lastPrev;
                        });
                    }
                }
            }
        }

        public bool AddFrame(NDIlib.video_frame_v2_t frame)
        {
            try
            {
                pendingFrames.Add(frame);
            }
            catch (OperationCanceledException)
            {
                // we're shutting down
                pendingFrames.CompleteAdding();
                return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        private Object sendInstanceLock = new Object();
        private IntPtr sendInstancePtr = IntPtr.Zero;

        //RenderTargetBitmap iHaveTheTargetBitmap = null;

        private int stride;
        private int bufferSize;
        private float aspectRatio;

        // a thread to send frames on so that the UI isn't dragged down
        Thread sendThread = null;

        // a way to exit the thread safely
        bool exitThread = false;

        // a thread safe collection to store pending frames
        BlockingCollection<NDIlib.video_frame_v2_t> pendingFrames = new BlockingCollection<NDIlib.video_frame_v2_t>();

        // used for pausing the send thread
        bool isPausedValue = false;

        // should we send system audio with the video?
        bool sendSystemAudio = false;

        // a capture device to grab system audio
        WasapiLoopbackCapture audioCap = null;

        // basic description of the audio stream
        int audioSampleRate = 48000;
        int audioSampleSizeInBytes = 4;
        int audioNumChannels = 2;

        // an audio frame to reuse
        NDIlib.audio_frame_v2_t audioFrame = new NDIlib.audio_frame_v2_t()
        {
            sample_rate = 48000,
            no_channels = 2,
            no_samples = 0,
            timecode = NDIlib.send_timecode_synthesize,
            p_data = IntPtr.Zero,
            channel_stride_in_bytes = 0,
            p_metadata = IntPtr.Zero,
            timestamp = 0
        };

        // the size of the allocated audioFrame.p_data
        int audioBufferSize = 0;
    }
}
