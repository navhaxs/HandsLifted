using System;
using System.IO;
using System.Reflection;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;

namespace PowerPointConverterSampleApp;

public partial class App : Application
{
    public override void Initialize()
    {
        try
        {
            var x = LoadConfigFromResource("PowerPointConverterSampleApp.SyncfusionLicenseKey");
            string syncfusionLicenseKey = new StreamReader(x).ReadToEnd().Trim();
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
        }
        catch (Exception ex)
        {
        }

        AvaloniaXamlLoader.Load(this);
    }

    private Stream LoadConfigFromResource(string configFileName)
    {
        Assembly assembly;
        Stream configStream = null;
        try
        {
            assembly = Assembly.GetExecutingAssembly();
            configStream = (Stream)assembly.GetManifestResourceStream(configFileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        return configStream;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}