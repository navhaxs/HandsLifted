using System;
using Avalonia.Controls;
using Avalonia.Input;

namespace HandsLiftedApp.Controls
{
    public class ComboBoxWithoutWheelScroll : ComboBox
    {
        // Without this, Avalonia's Fluent theme (keyed by exact runtime type) finds no ControlTheme
        // for this subclass and renders it with no template at all - invisible, no dropdown chrome.
        protected override Type StyleKeyOverride => typeof(ComboBox);

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            e.Handled = true;
            base.OnPointerWheelChanged(e);
        }
    }
}
