﻿/*
 *  Copyright 2009, 2010 Matvei Stefarov <me@matvei.org>
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 *  THE SOFTWARE.
 *
 */
using System;
using System.Text;
using fCraft;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;


namespace fCraftConsole {

    static class Program {

        static void Main( string[] args ) {
            Server.OnLog += Log;
            Server.OnURLChanged += SetURL;
#if DEBUG
#else
            try {
#endif
                if( Server.Init() ) {

                    UpdaterResult update = Updater.CheckForUpdates();
                    if( update.UpdateAvailable ) {
                        Console.WriteLine( "** A new version of fCraft is available: {0}, released {1:0} day(s) ago. **",
                                           update.GetVersionString(),
                                           DateTime.Now.Subtract( update.ReleaseDate ).TotalDays );
                    }

                    try {
                        if( Process.GetCurrentProcess().PriorityClass != Config.GetProcessPriority() ) {
                            Process.GetCurrentProcess().PriorityClass = Config.GetProcessPriority();
                        }
                    } catch( Exception ) {
                        Logger.Log( "Program.Main: Could not set process priority, using defaults.", LogType.Warning );
                    }

                    if( Server.Start() ) {
                        Console.Title = "fCraft " + Updater.GetVersionString() + " - " + Config.GetString( ConfigKey.ServerName );
                        Console.WriteLine( "** Running fCraft version " + Updater.GetVersionString() + ". **" );
                        Console.WriteLine( "** Server is now ready. Type /shutdown to exit. URL is in externalurl.txt **" );

                        while( true ) {
                            Player.Console.ParseMessage( Console.ReadLine(), true );
                        }

                    } else {
                        Console.Title = "fCraft " + Updater.GetVersionString() + " failed to start";
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine( "** Failed to start the server **" );
                        Server.Shutdown( "failed to start" );
                        Console.ReadLine();
                    }
                } else {
                    Console.Title = "fCraft " + Updater.GetVersionString() + " failed to initialize";
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine( "** Failed to initialize the server **" );
                    Server.Shutdown( "failed to initialize" );
                    Console.ReadLine();
                }
#if DEBUG
#else
            } catch( Exception ex ) {
                Console.Title = "fCraft " + Updater.GetVersionString() + " CRASHED";
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Log( "Unhandled exception in fCraftConsole input loop: " + ex, LogType.FatalError );

                Console.WriteLine( "fCraft crashed! Crash message saved to crash.log." );
                Console.Write( ex );
                Logger.UploadCrashReport( "Unhandled exception in fCraftConsole", "fCraftConsole", ex );

                Server.CheckForCommonErrors( ex );
            }
            Console.ReadLine();
#endif
        }

        static void Log( string message, LogType type ) {
            Console.WriteLine( message );
        }

        static void SetURL( string URL ) {
            File.WriteAllText( "externalurl.txt", URL, ASCIIEncoding.ASCII );
            Console.WriteLine( "** " + URL + " **" );
        }
    }
}