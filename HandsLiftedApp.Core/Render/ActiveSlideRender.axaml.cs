using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.XTransitioningContentControl;

namespace HandsLiftedApp.Core.Render
{
	public partial class ActiveSlideRender : UserControl
	{
		public ActiveSlideRender()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Gets or sets the active slide
		/// </summary>
		private Slide _activeSlide = new BlankSlide();
		public Slide ActiveSlide
		{
			get => _activeSlide;
			set
			{
				SetAndRaise(ActiveSlideProperty, ref _activeSlide, value);
				UpdateCrossDissolveMode(value);
			}
		}

		public static readonly DirectProperty<ActiveSlideRender, Slide> ActiveSlideProperty =
			AvaloniaProperty.RegisterDirect<ActiveSlideRender, Slide>(
				nameof(ActiveSlide),
				o => o.ActiveSlide,
				(o, v) => o.ActiveSlide = v);

		private void UpdateCrossDissolveMode(Slide? slide)
		{
			if (TransitionControl?.PageTransition is XFade xFade)
			{
				bool hasMotionBackground = false;

				if (slide is SongSlideInstance songSlide)
				{
					hasMotionBackground = songSlide.HasMotionBackground;
				}
				else if (slide is SongTitleSlideInstance titleSlide)
				{
					hasMotionBackground = titleSlide.HasMotionBackground;
				}

				xFade.CrossDissolve = hasMotionBackground;

				// Use a black background when there is no motion background so that
				// semi-transparent pixels produced during the cross-fade transition
				// composite against black rather than showing through to empty layers.
				Background = hasMotionBackground ? Brushes.Transparent : Brushes.Black;
			}
		}
	}
}
