﻿using System;
using Gtk;
using ParkitectNexus.Data.Game;
using ParkitectNexus.Data.Web;
using ParkitectNexus.Data;
using ParkitectNexus.Data.Utilities;
using ParkitectNexus.Data.Game.Windows;
using CommandLine;
using System.IO;
using System.Linq;
using ParkitectNexus.Data.Reporting;
using System.Reflection;

namespace ParkitectNexus.Client.GTK
{
    
    public class MainClass
    {

        public static void Main (string[] args)
        {
            IParkitect parkitect;
            IParkitectNexusWebsite parkitectNexusWebsite;
            IParkitectOnlineAssetRepository parkitectOnlineAssetRepository;


            switch (OperatingSystems.GetOperatingSystem())
            {
            case SupportedOperatingSystem.Windows:
                parkitect = new WindowsParkitect();
                parkitectNexusWebsite = new ParkitectNexusWebsite();
                parkitectOnlineAssetRepository = new ParkitectOnlineAssetRepository(parkitectNexusWebsite);
                break;
            case SupportedOperatingSystem.Linux:
                parkitect = new LinuxParkitect ();
                parkitectNexusWebsite = new ParkitectNexusWebsite ();
                parkitectOnlineAssetRepository = new ParkitectOnlineAssetRepository (parkitectNexusWebsite);
                break;
            default:
                return;
            }
            var options = new CommandLineOptions();
            var settings = new ClientSettings();


            //missing method for LINQ
            Parser.Default.ParseArguments(args, options);

            Log.Open(Path.Combine(AppData.Path, "ParkitectNexusLauncher.log"));
            Log.MinimumLogLevel = options.LogLevel;

            Application.Init ();
        

            try
            {
                Log.WriteLine($"Application was launched with arguments '{string.Join(" ", args)}'.", LogLevel.Info);

                //restrict update to windows. linux can use package manager to install updates
                if(OperatingSystems.GetOperatingSystem() == SupportedOperatingSystem.Windows)
                {
                    // Check for updates. If updates are available, do not resume usual logic.
                    var updateInfo = ParkitectUpdate.CheckForUpdates(parkitectNexusWebsite);
                    if (updateInfo != null)
                    {
                        ParkitectUpdate parkitectUpdate = new ParkitectUpdate(updateInfo,settings,options);
                        parkitectUpdate.Show();
                        if (parkitectUpdate.Run () == (int)Gtk.ResponseType.Close) {
                            parkitectUpdate.Destroy ();
                        }
                    }
                }

                ParkitectNexusProtocol.Install();

                //find the new location of where parkitect is installed
                if (!parkitect.IsInstalled) {
                    ParkitectFindError parkitectError = new ParkitectFindError (parkitect);
                    parkitectError.Show ();
                    switch(parkitectError.Run ())
                    {
                        case (int)Gtk.ResponseType.Ok:
                            parkitectError.Destroy();
                        break;
                        default:
                            Environment.Exit (0);
                        break;
                    }
                }

                // Ensure parkitect has been installed. If it has not been installed, quit the application.
                if(OperatingSystems.GetOperatingSystem() == SupportedOperatingSystem.Windows)
                    ParkitectUpdate.MigrateMods(parkitect);

                ModLoaderUtil.InstallModLoader(parkitect);

                // Install backlog.
                if (!string.IsNullOrWhiteSpace(settings.DownloadOnNextRun))
                {
                    if(!ModDownload.Download(settings.DownloadOnNextRun, parkitect, parkitectOnlineAssetRepository))
                        Environment.Exit(0);    
                    settings.DownloadOnNextRun = null;
                    settings.Save();
                }

                // Process download option.
                if (options.DownloadUrl != null)
                {
                    ModDownload.Download(options.DownloadUrl, parkitect, parkitectOnlineAssetRepository);
                    return;
                }

                // If the launch option has been used, launch the game.
                if (options.Launch)
                {
                    parkitect.Launch();
                    return;
                }

                if (options.Silent && !settings.BootOnNextRun)
                    return;

                settings.BootOnNextRun = false;
                settings.Save();

                MainWindow window = new MainWindow (parkitectNexusWebsite,parkitect,parkitectOnlineAssetRepository);
                window.DeleteEvent += (o , arg) =>{
                    // Handle silent calls.
                    Log.Close();
            
                };
            
            

            }
            catch (Exception e)
            {
                Log.WriteLine("Application exited in an unusual way.", LogLevel.Fatal);
                Log.WriteException(e);
                CrashReporter.Report("global", parkitect, parkitectNexusWebsite, e);

                Gtk.MessageDialog err = new MessageDialog (null, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Ok, "The application has crashed in an unusual way.\n\nThe error has been logged to:\n"+ Log.LoggingPath);
                err.Run();

                Environment.Exit(0);
        

            }

            Application.Run ();


        }


    
    }
}
