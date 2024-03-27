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
        // Build a config object, using env vars and JSON providers.
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddJsonStream(LoadConfigFromResource("PowerPointConverterSampleApp.appsettings.json"))
            .Build();
        // Get values from the config given their key and their target type.
        Settings? settings = config.Get<Settings>();

        if (settings != null)
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(settings.SyncfusionLicenseKey);
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