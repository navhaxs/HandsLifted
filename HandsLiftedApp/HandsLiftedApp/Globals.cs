using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using HandsLiftedApp.Models;
using HandsLiftedApp.Utils;
using HandsLiftedApp.ViewModels;
using ReactiveUI;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.IO;
using System.Diagnostics;

namespace HandsLiftedApp
{
    public static class Globals
    {
        public static PreferencesViewModel Preferences;
        public static Env Env;

        // note: this is initialized by App.axaml.cs on program start up
        public static void OnStartup(IApplicationLifetime applicationLifetime)
        {
            LoadEnv();

            // Initialize app preferences state here

            // Create the AutoSuspendHelper.
            var suspension = new AutoSuspendHelper(applicationLifetime);
            RxApp.SuspensionHost.CreateNewAppState = () => new PreferencesViewModel();
            RxApp.SuspensionHost.SetupDefaultSuspendResume(new NewtonsoftJsonSuspensionDriver("appstate.json"));
            suspension.OnFrameworkInitializationCompleted();

            // Load the saved view model state.
            Globals.Preferences = RxApp.SuspensionHost.GetAppState<PreferencesViewModel>();

            Debug.Print(Preferences.ToString());
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
