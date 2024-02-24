using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using HandsLiftedApp.Core.ViewModels;
using LibMpv.Client;
using Serilog;

namespace HandsLiftedApp.Core
{
    public static class Globals
    {
        public static MainViewModel MainViewModel { get; set; }
        public static MpvContext MpvContextInstance { get; set; }

        public static void OnStartup(IApplicationLifetime applicationLifetime)
        {
            if (Design.IsDesignMode)
            {
                return;
            }

            try
            {
                MpvContextInstance = new MpvContext();
                Log.Information("LibMPV API {Version}", MpvContextInstance.GetMpvClientApiGetApiVersion());
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "LibMPV failed to initialize");
            }
            
            // initialize LibMPV before MainViewModel
            MainViewModel = new();
        }
    }
}