using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScratchApp
{
    public class ProgressBarWorkaround
    {
        public static AvaloniaProperty<double> ValueProperty =
            AvaloniaProperty.RegisterAttached<ProgressBarWorkaround, ProgressBar, double>("Value");

        public static void SetValue(ProgressBar pb, double value) =>
            pb.SetValue(ValueProperty, value);

        static ProgressBarWorkaround()
        {
            ValueProperty.Changed.Subscribe(ev =>
            {
                ((ProgressBar)ev.Sender).Value = ev.NewValue.Value;
            });
        }
    }
}
