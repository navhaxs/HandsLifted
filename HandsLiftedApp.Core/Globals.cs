using System;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core.ViewModels;
using LibMpv.Client;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core
{
    public static class Globals
    {
        public static MainViewModel MainViewModel { get; set; }
        public static MpvContext MpvContextInstance { get; set; }
        public static AppPreferencesViewModel AppPreferences { get; set; }

        public static void OnStartup(IApplicationLifetime applicationLifetime)
        {
            if (Design.IsDesignMode)
            {
                return;
            }

            // initialize LibMPV before MainViewModel
            try
            {
                MpvContextInstance = new MpvContext();
                Log.Information("LibMPV API {Version}", MpvContextInstance.GetMpvClientApiGetApiVersion());
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "LibMPV failed to initialize");
            }
            
            try
            {
                var x = LoadConfigFromResource("HandsLiftedApp.Core.SyncfusionLicenseKey");
                string syncfusionLicenseKey = new StreamReader(x).ReadToEnd().Trim();
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicenseKey);
                Log.Information("SyncfusionLicenseProvider initialized");
            }
            catch
            {
                Log.Fatal("SyncfusionLicenseProvider failed to initialize");
            }

            // Initialize app preferences state here
            {
                // Create the AutoSuspendHelper.
                var suspension = new AutoSuspendHelper(applicationLifetime);
                RxApp.SuspensionHost.CreateNewAppState = () => new AppPreferencesViewModel();
                RxApp.SuspensionHost.SetupDefaultSuspendResume(new NewtonsoftJsonSuspensionDriver<AppPreferencesViewModel>(Constants.APP_STATE_FILEPATH));
                suspension.OnFrameworkInitializationCompleted();

                // Load the saved view model state.
                AppPreferences = RxApp.SuspensionHost.GetAppState<AppPreferencesViewModel>();
            }
            
            MainViewModel = new();
        }
        
        private static Stream LoadConfigFromResource(string configFileName)
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
    }
}