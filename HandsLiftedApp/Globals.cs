using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Models;
using HandsLiftedApp.Services.Bitmaps;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels;
using LibMpv.Client;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace HandsLiftedApp
{
    public static class Globals
    {
        // note: these are public to be accessed from axaml bindings
        public static MainWindowViewModel MainWindowViewModel;
        public static PreferencesViewModel Preferences;
        public static StageDisplayViewModel StageDisplay = new StageDisplayViewModel();
        public static Env Env;
        public static MpvContext GlobalMpvContextInstance;
        public static BitmapLoadWorkerThread BitmapLoadWorkerThread;

        // note: this is initialized by App.axaml.cs on program start up
        public static void OnStartup(IApplicationLifetime applicationLifetime)
        {
            if (Design.IsDesignMode)
                return;

            LoadEnv();

            // Initialize app preferences state here

            // Create the AutoSuspendHelper.
            var suspension = new AutoSuspendHelper(applicationLifetime);
            RxApp.SuspensionHost.CreateNewAppState = () => new PreferencesViewModel();
            RxApp.SuspensionHost.SetupDefaultSuspendResume(new NewtonsoftJsonSuspensionDriver<PreferencesViewModel>(Constants.APP_STATE_FILEPATH));
            suspension.OnFrameworkInitializationCompleted();

            // Load the saved view model state.
            Globals.Preferences = RxApp.SuspensionHost.GetAppState<PreferencesViewModel>();

            //try
            //{
            //    GlobalLibVLCInstance = new LibVLC();
            //    Log.Information("LibVLC initialized OK");
            //}
            //catch (Exception ex)
            //{
            //    Log.Fatal(ex, "LibVLC failed to initialize");
            //}

            if (!Design.IsDesignMode)
            {
                try
                {
                    GlobalMpvContextInstance = new MpvContext();
                    Log.Information("LibMPV initialized OK");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "LibMPV failed to initialize");
                }
            }

            BitmapLoadWorkerThread = new BitmapLoadWorkerThread();
        }

        private static void LoadEnv()
        {
            //TODO
            if (File.Exists("env.yml"))
            {
                var yml = File.ReadAllText("env.yml");

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)  // see height_in_inches in sample yml 
                    .Build();

                //yml contains a string containing your YAML
                var p = deserializer.Deserialize<Env>(yml);

                Globals.Env = p;
            }
            else
            {
                Globals.Env = new Env();
            }
        }


    }
}
