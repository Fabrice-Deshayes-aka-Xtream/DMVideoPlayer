using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System;
using System.Runtime.InteropServices;

namespace DMVideoPlayer
{
    public class VideoView : Control
    {
        private WriteableBitmap? _bitmap;
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private readonly object _lock = new object();
        private uint _videoWidth = 1920;
        private uint _videoHeight = 1080;

        public VideoView()
        {
            ClipToBounds = true;
        }

        public void Initialize(LibVLC libVLC)
        {
            _libVLC = libVLC;
            _mediaPlayer = new MediaPlayer(_libVLC);
            
            // Configure video callbacks BEFORE playing
            // This prevents VLC from opening an external window
            lock (_lock)
            {
                _bitmap = new WriteableBitmap(
                    new PixelSize((int)_videoWidth, (int)_videoHeight),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
            }

            _mediaPlayer.SetVideoFormat("RV32", _videoWidth, _videoHeight, _videoWidth * 4);
            _mediaPlayer.SetVideoCallbacks(Lock, null, Display);
        }

        private void OnVoutChanged(object? sender, MediaPlayerVoutEventArgs e)
        {
            if (_mediaPlayer == null || e.Count == 0) return;

            // Wait a bit for video to start
            System.Threading.Thread.Sleep(100);

            Dispatcher.UIThread.Post(() =>
            {
                // Use fixed dimensions or get from pixel buffer callback
                _videoWidth = 1920;
                _videoHeight = 1080;

                lock (_lock)
                {
                    _bitmap = new WriteableBitmap(
                        new PixelSize((int)_videoWidth, (int)_videoHeight),
                        new Vector(96, 96),
                        PixelFormat.Bgra8888,
                        AlphaFormat.Premul);
                }

                _mediaPlayer.SetVideoFormat("RV32", _videoWidth, _videoHeight, _videoWidth * 4);
                _mediaPlayer.SetVideoCallbacks(Lock, null, Display);
            });
        }

        private IntPtr Lock(IntPtr opaque, IntPtr planes)
        {
            lock (_lock)
            {
                if (_bitmap != null)
                {
                    using var buffer = _bitmap.Lock();
                    Marshal.WriteIntPtr(planes, buffer.Address);
                }
            }
            return IntPtr.Zero;
        }

        private void Display(IntPtr opaque, IntPtr picture)
        {
            Dispatcher.UIThread.Post(() => InvalidateVisual());
        }

        public MediaPlayer? MediaPlayer => _mediaPlayer;

        public override void Render(DrawingContext context)
        {
            lock (_lock)
            {
                if (_bitmap != null)
                {
                    var sourceRect = new Rect(0, 0, _bitmap.PixelSize.Width, _bitmap.PixelSize.Height);
                    
                    // Maintain aspect ratio
                    var scaleX = Bounds.Width / _bitmap.PixelSize.Width;
                    var scaleY = Bounds.Height / _bitmap.PixelSize.Height;
                    var scale = Math.Min(scaleX, scaleY);

                    var scaledWidth = _bitmap.PixelSize.Width * scale;
                    var scaledHeight = _bitmap.PixelSize.Height * scale;
                    var x = (Bounds.Width - scaledWidth) / 2;
                    var y = (Bounds.Height - scaledHeight) / 2;

                    var destRect = new Rect(x, y, scaledWidth, scaledHeight);

                    context.DrawImage(_bitmap, sourceRect, destRect);
                }
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            
            // Stop playback safely before disposing
            // Check for null only - if already disposed, Stop() will throw and we catch it
            try
            {
                if (_mediaPlayer != null)
                {
                    if (_mediaPlayer.IsPlaying)
                    {
                        _mediaPlayer.Stop();
                    }
                    _mediaPlayer.Dispose();
                    _mediaPlayer = null;
                }
            }
            catch (Exception)
            {
                // Ignore exceptions during detachment - native resources may already be released
            }
            
            lock (_lock)
            {
                _bitmap?.Dispose();
                _bitmap = null;
            }
        }
    }
}
