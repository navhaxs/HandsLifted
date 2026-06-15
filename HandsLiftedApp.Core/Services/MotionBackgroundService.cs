using System;
using System.IO;
using System.Reactive.Subjects;
using HandsLiftedApp.Core.Utils;
using LibMpv.Client;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
	public static class MotionBackgroundService
	{
		// Replays the current context to new subscribers (BehaviorSubject starts with null).
		private static readonly BehaviorSubject<MpvContext?> _activeContextSubject = new(null);

		/// <summary>
		/// Emits the currently active MpvContext whenever playback starts or stops.
		/// New subscribers immediately receive the current value.
		/// </summary>
		public static IObservable<MpvContext?> ActiveContext => _activeContextSubject;

		/// <summary>
		/// Returns the currently active MpvContext synchronously, or null if no motion
		/// background is playing. Use for lightweight polling (e.g. NDI throttle check).
		/// </summary>
		public static MpvContext? CurrentContext => _activeContextSubject.Value;

		// True while a cross-fade between two different motion background videos is in progress.
		// Slide canvases use this to synchronize text fades with the video fades.
		private static readonly BehaviorSubject<bool> _isTransitioningSubject = new(false);

		/// <summary>
		/// Emits true when a motion-background cross-fade starts and false when the new video
		/// begins fading in (first frame decoded). New subscribers receive the current value.
		/// </summary>
		public static IObservable<bool> IsTransitioning => _isTransitioningSubject;

		/// <summary>Synchronous snapshot of the current transitioning state.</summary>
		public static bool IsCurrentlyTransitioning => _isTransitioningSubject.Value;

		/// <summary>
		/// Duration of the outgoing video fade. Published by MotionBackgroundLayer when a
		/// cross-fade starts so slide canvases can fade their text out in sync.
		/// </summary>
		public static TimeSpan CrossFadeOutDuration { get; internal set; } = TimeSpan.FromMilliseconds(500);

		/// <summary>
		/// Duration of the incoming video fade. Published by MotionBackgroundLayer when a
		/// cross-fade starts so slide canvases can fade their text in in sync.
		/// </summary>
		public static TimeSpan CrossFadeInDuration { get; internal set; } = TimeSpan.FromMilliseconds(500);

		internal static void SetTransitioning(bool value) =>
			_isTransitioningSubject.OnNext(value);

		// Fires when the new video's first frame is ready and it begins fading in.
		// Separate from SetTransitioning(false) so the observer knows to fade IN only when
		// a new video is actually starting — not on cancel/stop paths that also release the gate.
		private static readonly System.Reactive.Subjects.Subject<System.Reactive.Unit> _fadeInSubject = new();
		public static IObservable<System.Reactive.Unit> WhenNewVideoFadesIn => _fadeInSubject;
		internal static void SignalFadeIn() => _fadeInSubject.OnNext(System.Reactive.Unit.Default);

		/// <summary>
		/// Called by MotionBackgroundLayer to broadcast the active context to all observers.
		/// Pass null when playback stops.
		/// </summary>
		internal static void PublishActiveContext(MpvContext? context) =>
			_activeContextSubject.OnNext(context);

		private static readonly string[] SupportedVideoExtensions =
		{
			".mp4", ".mov", ".avi", ".wmv", ".mkv", ".webm"
		};

		/// <summary>
		/// Creates a new MpvContext configured for motion background video playback.
		/// Returns null if initialization fails.
		/// </summary>
		public static MpvContext? CreateMotionMpvContext()
		{
			try
			{
				Log.Debug("[MotionBg] Creating new MpvContext with motion background config...");
				var context = new MpvContext();
				context.SetPropertyString("vo", "libmpv");
				context.SetPropertyString("force-window", "no");
				context.SetPropertyString("loop-file", "inf");
				context.SetPropertyString("video-sync", "display-resample");
				context.SetPropertyString("aid", "no");
				context.SetPropertyString("mute", "yes");
				Log.Debug("[MotionBg] MpvContext created successfully");
				return context;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[MotionBg] Failed to initialize MpvContext");
				return null;
			}
		}

		/// <summary>
		/// Safely disposes a MpvContext, setting the reference to null.
		/// Catches and logs any exceptions during disposal.
		/// </summary>
		public static void DisposeContext(ref MpvContext? context)
		{
			if (context == null)
			{
				Log.Debug("[MotionBg] DisposeContext called but context is already null");
				return;
			}

			try
			{
				Log.Debug("[MotionBg] Disposing MpvContext...");
				context.Dispose();
				Log.Debug("[MotionBg] MpvContext disposed successfully");
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[MotionBg] Error disposing MpvContext");
			}
			finally
			{
				context = null;
			}
		}

		/// <summary>
		/// Validates whether the given file path has a supported video extension
		/// and is a fully qualified (absolute) path.
		/// Returns false for null, empty, relative paths, or unsupported extensions.
		/// </summary>
		public static bool IsValidVideoFile(string? filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath))
			{
				return false;
			}

			if (!Path.IsPathFullyQualified(filePath))
			{
				Log.Debug("[MotionBg] IsValidVideoFile: path is not fully qualified: {FilePath}", filePath);
				return false;
			}

			var extension = Path.GetExtension(filePath);
			if (string.IsNullOrEmpty(extension))
			{
				Log.Debug("[MotionBg] IsValidVideoFile: no extension on path: {FilePath}", filePath);
				return false;
			}

			foreach (var supported in SupportedVideoExtensions)
			{
				if (string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			Log.Debug("[MotionBg] IsValidVideoFile: unsupported extension '{Extension}' for: {FilePath}",
				extension, filePath);
			return false;
		}

		/// <summary>
		/// Resolves a relative video path against the playlist directory.
		/// Returns null if either argument is null or empty.
		/// </summary>
		public static string? ResolveVideoPath(string? relativePath, string? playlistDirectory)
		{
			if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(playlistDirectory))
			{
				return null;
			}

			return RelativeFilePathResolver.ToAbsolutePath(playlistDirectory, relativePath);
		}
	}
}
