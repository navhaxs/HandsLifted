using System;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace HandsLiftedApp.Controls.Items
{
    public partial class CommentItemView : UserControl
    {
        public static readonly IValueConverter ClampWidthTo600 =
            new FuncValueConverter<double, double>(w => Math.Min(w, 600));

        public CommentItemView()
        {
            InitializeComponent();
        }
    }
}
