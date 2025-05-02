using System;
using System.IO;
using System.Reactive.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Common;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Core.ViewModels;
using LibMpv.Client;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core
{
    public class Globals : ReactiveObject
    {
        private static readonly Lazy<Globals> _instance = new Lazy<Globals>(() => new Globals());
        public static Globals Instance => _instance.Value;

        private ObservableAsPropertyHelper<Bitmap?> _logoBitmap;
        public Bitmap? LogoBitmap => _logoBitmap.Value;

        // Private constructor to enforce singleton pattern
        private Globals()
        {
            // refactor with OnStartup?
        }

        public MainViewModel MainViewModel { get; set; }
        public MpvContext? MpvContextInstance { get; set; }
        public AppPreferencesViewModel AppPreferences { get; set; }

        public ImportWorkerThread ImportWorkerThread { get; } = new();

        public void OnStartup(IApplicationLifetime applicationLifetime)
        {
            if (Design.IsDesignMode)
            {
                return;
            }

            // initialize LibMPV before MainViewModel
            try
            {
                MpvContextInstance = new MpvContext();
                MpvContextInstance.SetPropertyString("video-display", "no");
                MpvContextInstance.SetPropertyString("video-sync", "display-resample");
                MpvContextInstance.SetPropertyString("force-window", "no");
                MpvContextInstance.SetPropertyString("vo", "libmpv");
 
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
            catch (Exception ex)
            {
                Log.Fatal(ex, "SyncfusionLicenseProvider failed to initialize");
            }

            // Initialize app preferences state here
            {
                // Create the AutoSuspendHelper.
                var suspension = new AutoSuspendHelper(applicationLifetime);
                RxApp.SuspensionHost.CreateNewAppState = () => new AppPreferencesViewModel();
                RxApp.SuspensionHost.SetupDefaultSuspendResume(
                    new NewtonsoftJsonSuspensionDriver<AppPreferencesViewModel>(Constants.APP_STATE_FILEPATH));
                suspension.OnFrameworkInitializationCompleted();

                // Load the saved view model state.
                AppPreferences = RxApp.SuspensionHost.GetAppState<AppPreferencesViewModel>();
            }

            MainViewModel = new();

            // Create an observable that combines the changes to both LogoBitmap properties
            _logoBitmap = MainViewModel.WhenAnyValue(x => x.Playlist.LogoBitmap)
                .Merge(AppPreferences.WhenAnyValue(x => x.LogoBitmap))
                .Select(_ => MainViewModel.Playlist.LogoBitmap ?? AppPreferences.LogoBitmap)
                .ToProperty(this, x => x.LogoBitmap);
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
                Log.Error(ex.ToString());
            }

            return configStream;
        }
    }
}