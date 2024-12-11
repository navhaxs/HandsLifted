using Avalonia.Controls;
using Avalonia.Input;

namespace HandsLiftedApp.Controls
{
    public class ComboBoxWithoutWheelScroll : ComboBox
    {
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            e.Handled = true;
            base.OnPointerWheelChanged(e);
        }
    }
}
