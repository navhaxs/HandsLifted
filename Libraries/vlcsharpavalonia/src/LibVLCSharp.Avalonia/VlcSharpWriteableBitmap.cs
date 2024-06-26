﻿using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using System.Threading;

namespace LibVLCSharp.Avalonia
{
    public sealed class VlcSharpWriteableBitmap : Bitmap
    {
        private bool _disposed;
        private object _lockRead = new object();
        private object _lockWrite = new object();
        private PixelFormat? _pixelFormat;
        private WriteableBitmap _read;
        private ISubject<Unit> _rendered = new Subject<Unit>();
        private ISubject<Unit> _updated = new Subject<Unit>();
        private WriteableBitmap _write;

        // TODO: broken in Avalonia 11.x
        public VlcSharpWriteableBitmap() : base("")
        {
        }

        public void NotClientImplementable()
        {
            // throw new NotImplementedException();
        }

        public Vector Dpi => GetValueSafe(x => x.Dpi);
        public PixelSize PixelSize => GetValueSafe(x => x.PixelSize, new PixelSize(1, 1));
        //public Ref<IBitmapImpl> PlatformImpl => GetValueSafe(x => x.PlatformImpl);
        public IObservable<Unit> Rendered => _rendered;
        public Size Size => GetValueSafe(x => x.Size);
        public IObservable<Unit> Updated => _updated;
        public event EventHandler Invalidated;

        public void Clear()
        {
            using (LockWrite())
            {
                _write?.Dispose();
                _write = null;
            }
            using (LockRead())
            {
                _read?.Dispose();
                _read = null;
            }

            NotifyUpdated();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _updated.OnCompleted();
            _rendered.OnCompleted();

            Clear();

            _disposed = true;
        }

        public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
        {
            // this is the expensive operation!!
            Read(b => context.DrawImage(_read, destRect));
            NotifyRendered();
        }

        public void Read(Action<Bitmap> action)
        {
            using (LockRead())
            {
                if (_read != null)
                {
                    action(_read);
                }
            }
        }

        //public void Render(IDrawingContextImpl context, double opacity, Rect sourceRect, Rect destRect, BitmapInterpolationMode bitmapInterpolationMode = BitmapInterpolationMode.Default)
        //{
        //    //Read(b => context.DrawImage(_read, sourceRect));
        //    NotifyRendered();
        //}

        public void Save(string fileName, int? quality = null) => Read(b => b.Save(fileName));

        public void Save(Stream stream, int? quality = null) => Read(b => b.Save(stream));

        public void Write(PixelSize size, Vector dpi, PixelFormat format, Action<ILockedFramebuffer> action)
        {
            using (LockWrite())
            {
                if (_write == null || _write.Dpi != dpi || _write.PixelSize != size || _pixelFormat != format)
                {
                    _write?.Dispose();
                    _write = new WriteableBitmap(size, dpi, format);
                    _pixelFormat = format;
                }

                using (var fb = _write.Lock())
                {
                    action(fb);
                }

                using (LockRead())
                {
                    var tmp = _read;
                    _read = _write;
                    _write = tmp;
                }
            }

            NotifyUpdated();
        }

        private T GetValueSafe<T>(Func<Bitmap, T> getter, T defaultvalue = default(T))
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VlcSharpWriteableBitmap));

            using (LockRead())
            {
                return _read == null ? defaultvalue : getter(_read);
            }
        }

        private IDisposable Lock(object lockObject)
        {
            Monitor.Enter(lockObject);
            return Disposable.Create(() => Monitor.Exit(lockObject));
        }

        private IDisposable LockRead() => Lock(_lockRead);

        private IDisposable LockWrite() => Lock(_lockWrite);

        private void NotifyRendered() => _rendered?.OnNext(Unit.Default);

        private void NotifyUpdated()
        {
            _updated?.OnNext(Unit.Default);
            if (Invalidated != null)
            {
                Dispatcher.UIThread.Post(() => Invalidated?.Invoke(this, EventArgs.Empty));
            }
        }
    }
}