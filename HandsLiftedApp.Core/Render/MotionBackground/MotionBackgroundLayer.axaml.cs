using System;
using System.IO;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using ReactiveUI;
using Avalonia.Threading;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using LibMpv.Client;
using Serilog;

namespace HandsLiftedApp.Core.Render.MotionBackground
{
	public partial class MotionBackgroundLayer : UserControl, IDisposable
	{
		private MpvContext? _motionMpvContext;
		private string? _currentVideoPath;
		private bool _isFirstFrameReceived;
		private IDisposable? _activeItemSubscription;
		private IDisposable? _pendingFadeOutTimer;

		/// <summary>
		/// When true, a fade-out + stop has been scheduled but hasn't completed yet.
		/// The _currentVideoPath is still set (context alive) but we've committed to stopping.
		/// </summary>
		private bool _isStopPending;
		private IDisposable? _activeItemPathSubscription;

		/// <summary>
		/// Gets or sets the fade-in duration for the motion background layer.
		/// </summary>
		public TimeSpan FadeInDuration { get; set; } = TimeSpan.FromMilliseconds(500);

		/// <summary>
		/// Gets or sets the fade-out duration for the motion background layer.
		/// </summary>
		public TimeSpan FadeOutDuration { get; set; } = TimeSpan.FromMilliseconds(500);

		private Item? _activeItem;

		/// <summary>
		/// Gets or sets the active playlist item bound to this motion background layer.
		/// </summary>
		public Item? ActiveItem
		{
			get => _activeItem;
			set => SetAndRaise(ActiveItemProperty, ref _activeItem, value);
		}

		public static readonly DirectProperty<MotionBackgroundLayer, Item?> ActiveItemProperty =
			AvaloniaProperty.RegisterDirect<MotionBackgroundLayer, Item?>(
				nameof(ActiveItem),
				o => o.ActiveItem,
				(o, v) => o.ActiveItem = v);

		public MotionBackgroundLayer()
		{
			InitializeComponent();
			Log.Debug("[MotionBg] MotionBackgroundLayer initialized");
			SubscribeToActiveItemChanges();
		}

		#region Playback Lifecycle

		/// <summary>
		/// Starts video playback for the given file path.
		/// Creates a new MpvContext, connects it to the SoftwareVideoView, and issues a loadfile command.
		/// </summary>
		public void StartPlayback(string videoFilePath)
		{
			Log.Debug("[MotionBg] StartPlayback called with: {FilePath}", videoFilePath);

			try
			{
				if (!File.Exists(videoFilePath))
				{
					Log.Warning("[MotionBg] Video file not found: {FilePath}", videoFilePath);
					RenderTransparent();
					return;
				}

				Log.Debug("[MotionBg] File exists, creating MpvContext...");
				_isFirstFrameReceived = false;

				_motionMpvContext = Services.MotionBackgroundService.CreateMotionMpvContext();
				if (_motionMpvContext == null)
				{
					Log.Error("[MotionBg] CreateMotionMpvContext returned null for: {FilePath}", videoFilePath);
					RenderTransparent();
					return;
				}

				Log.Debug("[MotionBg] MpvContext created, subscribing to events...");

				// Subscribe to first frame detection via VideoReconfig event
				_motionMpvContext.VideoReconfig += OnFirstFrameDecoded;

				// Subscribe to end-file for error detection during playback
				_motionMpvContext.EndFile += OnEndFile;

				// Connect the MpvContext to the SoftwareVideoView
				Log.Debug("[MotionBg] Connecting MpvContext to VideoView...");
				VideoView.MpvContext = _motionMpvContext;

				// Issue loadfile command to start playback
				Log.Debug("[MotionBg] Issuing loadfile command for: {FilePath}", videoFilePath);
				_motionMpvContext.Command("loadfile", videoFilePath);
				_currentVideoPath = videoFilePath;
				Services.MotionBackgroundService.PublishActiveContext(_motionMpvContext);
				Log.Debug("[MotionBg] Playback started successfully for: {FilePath}", videoFilePath);
			}
			catch (MpvException ex)
			{
				Log.Error(ex, "[MotionBg] MpvException starting playback for: {FilePath}", videoFilePath);
				StopPlayback();
				RenderTransparent();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[MotionBg] Unexpected error starting playback for: {FilePath}", videoFilePath);
				StopPlayback();
				RenderTransparent();
			}
		}

		/// <summary>
		/// Stops video playback, disposes the MpvContext, and resets state.
		/// </summary>
		public void StopPlayback()
		{
			Log.Debug("[MotionBg] StopPlayback called. CurrentPath={CurrentPath}, HasContext={HasContext}",
				_currentVideoPath, _motionMpvContext != null);

			try
			{
				if (_motionMpvContext != null)
				{
					_motionMpvContext.VideoReconfig -= OnFirstFrameDecoded;
					_motionMpvContext.EndFile -= OnEndFile;
				}

				VideoView.MpvContext = null;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "[MotionBg] Error disconnecting SoftwareVideoView during stop");
			}

			try
			{
				if (_motionMpvContext != null)
				{
					Log.Debug("[MotionBg] Issuing stop command to MpvContext");
					_motionMpvContext.Command("stop");
				}
			}
			catch (MpvException ex)
			{
				Log.Error(ex, "[MotionBg] Error issuing stop command");
			}
			catch (ObjectDisposedException)
			{
				Log.Debug("[MotionBg] MpvContext already disposed during stop");
			}

			Services.MotionBackgroundService.PublishActiveContext(null);
			Services.MotionBackgroundService.DisposeContext(ref _motionMpvContext);
			_currentVideoPath = null;
			_isFirstFrameReceived = false;
			Log.Debug("[MotionBg] StopPlayback complete");
		}

		/// <summary>
		/// Fades in the motion background layer by setting Opacity to 1.
		/// The DoubleTransition defined in AXAML animates the change.
		/// </summary>
		public void FadeIn()
		{
			Log.Debug("[MotionBg] FadeIn called");
			Dispatcher.UIThread.Post(() =>
			{
				Opacity = 1;
			});
		}

		/// <summary>
		/// Fades out the motion background layer by setting Opacity to 0.
		/// Invokes the optional callback after the fade-out duration completes.
		/// </summary>
		public void FadeOut(Action? onComplete = null)
		{
			Log.Debug("[MotionBg] FadeOut called, hasCallback={HasCallback}", onComplete != null);

			// Cancel any previously pending fade-out callback
			_pendingFadeOutTimer?.Dispose();
			_pendingFadeOutTimer = null;

			Dispatcher.UIThread.Post(() =>
			{
				Opacity = 0;

				if (onComplete != null)
				{
					_isStopPending = true;

					// Schedule callback after the fade-out transition completes
					_pendingFadeOutTimer = Observable.Timer(FadeOutDuration)
						.ObserveOn(AvaloniaScheduler.Instance)
						.Subscribe(_ =>
						{
							_isStopPending = false;
							_pendingFadeOutTimer = null;
							Log.Debug("[MotionBg] FadeOut complete, invoking callback");
							onComplete();
						});
				}
			});
		}

		/// <summary>
		/// Cancels any pending fade-out timer and immediately stops playback.
		/// Used when a new item arrives before the previous fade-out completes.
		/// </summary>
		private void CancelPendingFadeOut()
		{
			if (_pendingFadeOutTimer != null)
			{
				Log.Debug("[MotionBg] Cancelling pending fade-out timer");
				_pendingFadeOutTimer.Dispose();
				_pendingFadeOutTimer = null;
				_isStopPending = false;

				// Immediately stop the old playback since we're superseding it
				StopPlayback();
			}
		}

		/// <summary>
		/// Called when the first video frame is decoded and ready to render.
		/// Triggers the fade-in animation.
		/// </summary>
		private void OnFirstFrameDecoded(object? sender, EventArgs e)
		{
			Log.Debug("[MotionBg] VideoReconfig event fired. FirstFrameAlreadyReceived={Already}",
				_isFirstFrameReceived);

			if (_isFirstFrameReceived) return;
			_isFirstFrameReceived = true;

			// Unsubscribe to avoid repeated triggers on subsequent reconfigs
			if (_motionMpvContext != null)
			{
				_motionMpvContext.VideoReconfig -= OnFirstFrameDecoded;
			}

			Log.Debug("[MotionBg] First frame decoded for: {FilePath}, triggering FadeIn", _currentVideoPath);
			FadeIn();
		}

		/// <summary>
		/// Called when a file ends playback. Handles error conditions during playback.
		/// </summary>
		private void OnEndFile(object? sender, MpvEndFileEventArgs e)
		{
			Log.Debug("[MotionBg] EndFile event: Reason={Reason}, Error={Error}, Path={Path}",
				e.Reason, e.Error, _currentVideoPath);

			if (e.Reason == mpv_end_file_reason.MPV_END_FILE_REASON_ERROR)
			{
				var errorMessage = libmpv.mpv_error_string(e.Error);
				Log.Error("[MotionBg] Decode failure for {FilePath}: {Reason}",
					_currentVideoPath, errorMessage);

				Dispatcher.UIThread.Post(() =>
				{
					StopPlayback();
					RenderTransparent();
				});
			}
		}

		/// <summary>
		/// Ensures the layer renders as transparent (Opacity = 0).
		/// </summary>
		private void RenderTransparent()
		{
			Log.Debug("[MotionBg] RenderTransparent called");
			if (Dispatcher.UIThread.CheckAccess())
			{
				Opacity = 0;
			}
			else
			{
				Dispatcher.UIThread.Post(() => Opacity = 0);
			}
		}

		#endregion

		#region Reactive Item-Change Detection

		/// <summary>
		/// Subscribes to ActiveItem property changes to manage playback lifecycle.
		/// </summary>
		private void SubscribeToActiveItemChanges()
		{
			_activeItemSubscription = this.GetObservable(ActiveItemProperty)
				.Subscribe(OnActiveItemChanged);
		}

		/// <summary>
		/// Handles changes to the ActiveItem property.
		/// Determines whether to start, stop, or transition motion background playback.
		/// </summary>
		private void OnActiveItemChanged(Item? newItem)
		{
			_activeItemPathSubscription?.Dispose();
			_activeItemPathSubscription = null;

			var songItem = newItem as SongItemInstance;
			var hasMotionBackground = songItem?.HasMotionBackground == true;
			var newVideoPath = hasMotionBackground ? songItem!.MotionBackgroundVideoPath : null;

			Log.Debug("[MotionBg] OnActiveItemChanged: ItemType={ItemType}, HasMotionBg={HasMotionBg}, " +
			          "NewVideoPath={NewPath}, CurrentVideoPath={CurrentPath}, IsStopPending={IsStopPending}",
				newItem?.GetType().Name ?? "null",
				hasMotionBackground,
				newVideoPath,
				_currentVideoPath,
				_isStopPending);

			// If a stop is pending (fade-out in progress) but the new item wants the same video,
			// cancel the pending stop and resume playback immediately.
			if (_isStopPending && newVideoPath != null &&
			    _currentVideoPath != null &&
			    string.Equals(_currentVideoPath, newVideoPath, StringComparison.OrdinalIgnoreCase))
			{
				Log.Debug("[MotionBg] Stop was pending but same video requested again — cancelling stop and fading back in");
				_pendingFadeOutTimer?.Dispose();
				_pendingFadeOutTimer = null;
				_isStopPending = false;
				FadeIn();
				return;
			}

			// If a stop is pending and the new item wants a different video, cancel the pending
			// fade-out, stop immediately, and start the new video.
			if (_isStopPending && newVideoPath != null)
			{
				Log.Debug("[MotionBg] Stop was pending but different video requested — cancelling and starting new");
				CancelPendingFadeOut();
				StartPlayback(newVideoPath);
				return;
			}

			// If a stop is pending and the new item has no motion background, let it proceed naturally.
			if (_isStopPending && newVideoPath == null)
			{
				Log.Debug("[MotionBg] Stop already pending and new item has no motion background — no action");
				return;
			}

			// If the same video is already playing, do nothing (slide navigation within same song)
			if (_currentVideoPath != null && newVideoPath != null &&
			    string.Equals(_currentVideoPath, newVideoPath, StringComparison.OrdinalIgnoreCase))
			{
				Log.Debug("[MotionBg] Same video already playing, skipping");
				return;
			}

			// If currently playing a video and new item has a different (or no) motion background
			if (_currentVideoPath != null)
			{
				if (newVideoPath != null)
				{
					Log.Debug("[MotionBg] Transitioning from {OldPath} to {NewPath}",
						_currentVideoPath, newVideoPath);
					// Transition: fade out old → stop → start new → fade in
					FadeOut(() =>
					{
						StopPlayback();
						StartPlayback(newVideoPath);
					});
				}
				else
				{
					Log.Debug("[MotionBg] Stopping playback (new item has no motion background)");
					// No motion background on new item: fade out → stop
					FadeOut(() =>
					{
						StopPlayback();
					});
				}
			}
			else if (newVideoPath != null)
			{
				Log.Debug("[MotionBg] Starting fresh playback for: {NewPath}", newVideoPath);
				// No current playback, new item has motion background: start new → fade in
				StartPlayback(newVideoPath);
			}
			else
			{
				Log.Debug("[MotionBg] No action needed (no current playback, no new motion background)");
			}

			// When the active item's motion background path changes (e.g. user edits it in the
			// song editor while this song is already live), re-run this handler so the transition
			// logic picks up the new path immediately.
			if (songItem != null)
			{
				_activeItemPathSubscription = songItem
					.WhenAnyValue(x => x.MotionBackgroundVideoPath)
					.Skip(1)
					.Subscribe(_ => Dispatcher.UIThread.Post(() => OnActiveItemChanged(newItem)));
			}
		}

		#endregion

		protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);
			Dispose();
		}

		public void Dispose()
		{
			_pendingFadeOutTimer?.Dispose();
			_pendingFadeOutTimer = null;
			_activeItemSubscription?.Dispose();
			_activeItemSubscription = null;
			_activeItemPathSubscription?.Dispose();
			_activeItemPathSubscription = null;
			StopPlayback();
		}
	}
}
