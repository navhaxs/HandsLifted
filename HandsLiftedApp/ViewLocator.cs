using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HandsLiftedApp.ViewModels;
using System;

namespace HandsLiftedApp
{
    /// <summary>
    /// Boilerplate Avalonia code
    /// </summary>
    public class ViewLocator : IDataTemplate
    {
        public bool SupportsRecycling => false;

        public Control Build(object data)
        {
            var name = data.GetType().FullName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }
            else
            {
                return new TextBlock { Text = "Not Found: " + name };
            }
        }

        public bool Match(object data)
        {
            return data is ViewModelBase;
        }

        // Source: https://github.com/AvaloniaUI/Avalonia/discussions/5344
        /// <summary>
        /// Finds a view from a given ViewModel
        /// </summary>
        /// <param name="vm">The ViewModel representing a View</param>
        /// <returns>The View that matches the ViewModel. Null is no match found</returns>
        public static Window ResolveViewFromViewModel<T>(T vm) where T : ViewModelBase
        {
            var name = vm.GetType().AssemblyQualifiedName!.Replace("ViewModel", "View");
            var type = Type.GetType(name);
            return type != null ? (Window)Activator.CreateInstance(type)! : null;
        }
    }
}
