using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.Data;

namespace HandsLiftedApp.Core.Views;

public partial class ThirdPartyNoticesWindow : Window
{
    public ThirdPartyNoticesWindow()
    {
        InitializeComponent();

        var grid = this.FindControl<ItemsControl>("noticesGrid");
        grid!.ItemsSource = ThirdPartyNotices.All;

        var buttonClose = this.FindControl<Button>("buttonClose");
        buttonClose!.Click += (_, _) => Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
