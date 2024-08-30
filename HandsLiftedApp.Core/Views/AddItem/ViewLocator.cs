using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.Core.ViewModels.AddItem;
using HandsLiftedApp.Core.ViewModels.AddItem.Pages;
using HandsLiftedApp.Core.Views.AddItem.Pages;

namespace HandsLiftedApp.Core.Views.AddItem;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
            return null;

        if (data is AddItemPageViewModel vm)
        {
            if (vm is StartViewModel)
            {
                return new StartView();
            }
            else if (vm is ResultsViewModel)
            {
                return new ResultsView();
            }
        }
        //
        // var name = !.Replace("ViewModel", "View", StringComparison.Ordinal);
        // var type = Type.GetType(name);
        //
        // if (type != null)
        // {
        //     return (Control)Activator.CreateInstance(type)!;
        // }

        return new TextBlock { Text = "Not Found: " + data.GetType().FullName };
    }

    public bool Match(object? data)
    {
        return data is AddItemPageViewModel;
    }
}