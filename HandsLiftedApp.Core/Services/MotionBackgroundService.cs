using System;
using System.IO;
using HandsLiftedApp.Core.Utils;
using LibMpv.Client;
using Serilog;

namespace HandsLiftedApp.Core.Services
{
	public static class MotionBackgroundService
	{
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
