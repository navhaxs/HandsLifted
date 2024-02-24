using Avalonia.Input;
using HandsLiftedApp.Core.Models.AppState;
using HandsLiftedApp.Core.Models.UI;
using ReactiveUI;

namespace HandsLiftedApp.Core.Controller
{
    public static class KeyboardSlideNavigation
    {
        public static void OnKeyDown( Avalonia.Input.KeyEventArgs e)
        {
            
            switch (e.Key)
            {
                case Key.F1:
                    MessageBus.Current.SendMessage(new ActionMessage()
                        { Action = ActionMessage.NavigateSlideAction.GotoBlank });
                    e.Handled = true;
                    break;
                case Key.F12:
                    MessageBus.Current.SendMessage(new ActionMessage()
                        { Action = ActionMessage.NavigateSlideAction.GotoLogo });
                    e.Handled = true;
                    break;
                case Key.PageDown:
                case Key.Right:
                case Key.Space:
                    MessageBus.Current.SendMessage(new ActionMessage()
                        { Action = ActionMessage.NavigateSlideAction.NextSlide });
                    MessageBus.Current.SendMessage(new FocusSelectedItem());
                    e.Handled = true;
                    break;
                case Key.PageUp:
                case Key.Left:
                    MessageBus.Current.SendMessage(new ActionMessage()
                        { Action = ActionMessage.NavigateSlideAction.PreviousSlide });
                    MessageBus.Current.SendMessage(new FocusSelectedItem());
                    e.Handled = true;
                    break;
            }
        }
    }
}