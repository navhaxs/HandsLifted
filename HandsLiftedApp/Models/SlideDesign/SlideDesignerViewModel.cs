using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Models.SlideDesign {
    public class SlideDesignerViewModel : ReactiveObject {

        public List<Base> ListOfDesigns { get; } = new List<Base>();

        public SlideDesignerViewModel() {
            ListOfDesigns.Add(new Base() {
                FontFamily = FontFamily.Parse("Trakya Rounded 300"),
                TextColour = Color.Parse("#2b505e"),
                BackgroundColour = Color.Parse("#b7d1d8"),
                FontSize = 110,
                LineHeight = 160
            });
            ListOfDesigns.Add(new Base() {
                FontFamily = FontFamily.Parse("Montserrat"),
                TextColour = Color.Parse("#a06d39"),
                BackgroundColour = Color.Parse("#f5ede4"),
                FontSize = 100,
                LineHeight = 1,
                BackgroundGraphicFilePath = "avares://HandsLiftedApp/Assets/DesignerSlideTemplate/bg.png"
            });
        }
    }
}
