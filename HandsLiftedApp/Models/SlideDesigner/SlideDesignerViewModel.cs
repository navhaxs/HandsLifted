using Avalonia.Media;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideDesigner {
    public class SlideDesignerViewModel : ReactiveObject {

        public List<BaseSlideTheme> ListOfDesigns { get; } = new List<BaseSlideTheme>();
        private BaseSlideTheme _ActiveDesign;
        public BaseSlideTheme ActiveDesign { get => _ActiveDesign; set => this.RaiseAndSetIfChanged(ref _ActiveDesign, value); }

        public SlideDesignerViewModel() {
            ListOfDesigns.Add(new BaseSlideTheme() {
                FontFamily = TryParseFontFamily("Trakya Rounded 300"),
                TextColour = Color.Parse("#2b505e"),
                BackgroundColour = Color.Parse("#b7d1d8"),
                FontSize = 110,
                LineHeight = 160
            });
            ListOfDesigns.Add(new BaseSlideTheme() {
                FontFamily = TryParseFontFamily("Montserrat"),
                TextColour = Color.Parse("#a06d39"),
                BackgroundColour = Color.Parse("#f5ede4"),
                FontSize = 100,
                LineHeight = 1,
                BackgroundGraphicFilePath = "avares://HandsLiftedApp/Assets/DesignerSlideTemplate/bg.png"
            });
        }

        public FontFamily TryParseFontFamily(string fontFamilyName) {
            var installedFontFamilyNames = FontManager.Current.SystemFonts;
            if (installedFontFamilyNames.Contains(fontFamilyName))
                return FontFamily.Parse("Trakya Rounded 300");

            return FontFamily.Default;
        }
    }
}
