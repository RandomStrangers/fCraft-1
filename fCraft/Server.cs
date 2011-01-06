﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org> and Jesse O'Brien <destroyer661@gmail.com>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;


namespace fCraft {
    public static class Server {
        static string[] args = new string[0]; // saved to allow restarting with same params

        // player list
        static Dictionary<int, Player> players = new Dictionary<int, Player>();
        internal static Player[] playerList;
        static object playerListLock = new object();

        // session list
        static List<Session> sessions = new List<Session>();
        static object sessionLock = new object();

        // world list
        public static SortedDictionary<string, World> worlds = new SortedDictionary<string, World>();
        public static object worldListLock = new object();
        public const string WorldListFileName = "worlds.xml";
        public static World mainWorld;

        // networking
        static TcpListener listener;
        public static IPAddress IP;

        public static int maxUploadSpeed,   // set by Config.ApplyConfig
                          packetsPerSecond, // set by Config.ApplyConfig
                          MaxSessionPacketsPerTick = 128,
                          MaxBlockUpdatesPerTick = 60000; // used when there are no players in a world
        internal static float ticksPerSecond;

        const int MaxPortAttempts = 20;

        public static int Port;
        public static string URL;


        /// <summary>
        /// Reads command-line switches and sets up paths and logging.
        /// This should be called before any other library function.
        /// </summary>
        /// <param name="_args">string arguments passed to the frontend (if any)</param>
        public static void InitLibrary( string[] _args ) {
            args = _args;

            // try to parse arguments
            Dictionary<string, string> parsedArgs = new Dictionary<string, string>();
            foreach( string arg in args ) {
                if( arg.StartsWith( "--" ) && arg.Contains( '=' ) ) {
                    string argKey = arg.Substring( 2, arg.IndexOf( '=' ) - 2 ).ToLower().Trim();
                    string argValue = arg.Substring( arg.IndexOf( '=' ) + 1 ).Trim();
                    parsedArgs.Add( argKey, argValue );
                    Console.WriteLine( "{0} = {1}", argKey, argValue );
                }
            }


            // before we do anything, set path to the default location
            Directory.SetCurrentDirectory( Paths.WorkingPath );

            // set custom working path (if specified)
            if( parsedArgs.ContainsKey( "path" ) && Paths.TestDirectory( parsedArgs["path"], true ) ) {
                Paths.WorkingPath = Path.GetFullPath( parsedArgs["path"] );
                Directory.SetCurrentDirectory( Paths.WorkingPath );
            } else if( Paths.TestDirectory( Paths.WorkingPathDefault, true ) ) {
                Paths.WorkingPath = Path.GetFullPath( Paths.WorkingPathDefault );
                Directory.SetCurrentDirectory( Paths.WorkingPath );
            } else {
                throw new Exception( "Could not set the working path." );
            }

            Console.WriteLine( "Working directory: {0}", Directory.GetCurrentDirectory() );

            // set log path
            if( parsedArgs.ContainsKey( "logpath" ) && Paths.TestDirectory( parsedArgs["logpath"], true ) ) {
                Paths.LogPath = Path.GetFullPath( parsedArgs["logpath"] );
            } else if( Paths.TestDirectory( Paths.LogPathDefault, true ) ) {
                Paths.LogPath = Path.GetFullPath( Paths.LogPathDefault );
            } else {
                throw new Exception( "Could not set the log path." );
            }


            // set map path
            if( parsedArgs.ContainsKey( "mappath" ) && Paths.TestDirectory( parsedArgs["mappath"], true ) ) {
                Paths.MapPath = Path.GetFullPath( parsedArgs["mappath"] );
                Paths.IgnoreMapPathConfigKey = true;
            } else if( Paths.TestDirectory( Paths.MapPathDefault, true ) ) {
                Paths.MapPath = Path.GetFullPath( Paths.MapPathDefault );
            } else {
                throw new Exception( "Could not set the map path." );
            }


            // set config path
            Paths.ConfigFileName = Paths.ConfigFileNameDefault;
            if( parsedArgs.ContainsKey( "config" ) ) {
                string fileName = parsedArgs["config"];
                try {
                    if( File.Exists( fileName ) ) {
                        using( File.OpenWrite( fileName ) ) { }
                    } else {
                        using( File.Create( fileName ) ) { }
                    }
                    FileInfo info = new FileInfo( fileName );
                    Paths.ConfigFileName = info.FullName;

                } catch( Exception ex ) {
                    if( ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException ) {
                        Logger.Log( "Specified config path is invalid or incorrectly formatted ({0}: {1}).", LogType.Error,
                                    ex.GetType().ToString(), ex.Message );
                    } else if( ex is SecurityException || ex is UnauthorizedAccessException ) {
                        Logger.Log( "Cannot create config file, check permissions ({0}: {1}).", LogType.Error,
                                    ex.GetType().ToString(), ex.Message );
                    } else if( ex is DirectoryNotFoundException ) {
                        Logger.Log( "Cannot create config file: directory/drive/volume does not exist or is not mounted ({0}).", LogType.Error,
                                    ex.Message );
                    } else if( ex is IOException ) {
                        Logger.Log( "Cannot write to specified directory ({0}: {1}).", LogType.Error,
                                    ex.GetType().ToString(), ex.Message );
                    } else {
                        throw ex;
                    }
                }
            }
        }


        public static bool InitServer() {
            // warnings/disclaimers
            if( Updater.IsDev ) {
                Logger.Log( "You are using an unreleased developer version of fCraft. " +
                            "Do not use this version unless are are ready to deal with bugs and potential data loss. " +
                            "Consider using the lastest stable version instead, available from www.fcraft.net",
                            LogType.Warning );
            }
            if( Updater.IsBroken ) {
                Logger.Log( "This build has been marked as BROKEN. " +
                            "Do not use except for debugging purposes. " +
                            "Latest non-broken version is {0}.", LogType.Warning,
                            Updater.LatestNonBroken );
            }

            // try to load the config
            if( !Config.Load( false ) ) return false;
            Config.ApplyConfig();
            GenerateSalt();
            if( !Config.Save( true ) ) return false;

            // start the task thread
            BackgroundTasks.Start();

            // load player DB
            PlayerDB.Load();
            IPBanList.Load();

            // prepare the list of commands
            CommandList.Init();

            // Init IRC
            IRC.Init();

            if( OnInit != null ) OnInit();

            if( Config.GetBool( ConfigKey.AutoRankEnabled ) ) {
                AutoRank.Init();
            }

            return true;
        }


        public static bool StartServer() {
            serverStart = DateTime.Now;

            if( CheckForFCraftProcesses() ) {
                Logger.Log( "Please close all other fCraft processes (fCraftUI, fCraftConsole, or ConfigTool) " +
                            "that are started from the same directory.", LogType.Warning );
            }

            Player.Console = new Player( null, "(console)" );


            // try to load the world list
            if( !LoadWorldList() ) return false;
            SaveWorldList();

            // open the port
            bool portFound = false;
            int attempts = 0;
            Port = Config.GetInt( ConfigKey.Port );

            do {
                try {
                    listener = new TcpListener( IPAddress.Parse( Config.GetString( ConfigKey.IP ) ), Port );
                    listener.Start();
                    portFound = true;

                } catch( Exception ex ) {
                    // if the port is unavailable, try next one
                    Logger.Log( "Could not start listening on port {0}, trying next port. ({1})", LogType.Error,
                                Port, ex.Message );
                    Port++;
                    attempts++;
                }
            } while( !portFound && attempts < MaxPortAttempts );

            // if the port still cannot be opened after [maxPortAttempts] attemps, die.
            if( !portFound ) {
                Logger.Log( "Could not start listening after {0} tries. Giving up!", LogType.FatalError,
                            MaxPortAttempts );
                return false;
            }

            IP = ((IPEndPoint)listener.LocalEndpoint).Address;

            if( IP.ToString() != IPAddress.Any.ToString() ) {
                Logger.Log( "Server.Run: now accepting connections at {0}:{1}.", LogType.SystemActivity,
                            IP, Port );
            } else {
                Logger.Log( "Server.Run: now accepting connections at port {0}.", LogType.SystemActivity,
                            Port );
            }

            // list loaded worlds
            string line = "All available worlds: ";
            bool firstPrintedWorld = true;
            foreach( string worldName in Server.worlds.Keys ) {
                if( !firstPrintedWorld ) {
                    line += ", ";
                }
                line += worldName;
                firstPrintedWorld = false;
            }
            Logger.Log( line, LogType.SystemActivity );
            Logger.Log( "Main world: {0}; default rank: {1}", LogType.SystemActivity,
                        mainWorld.name, RankList.DefaultRank.Name );

            // Check for incoming connections 4 times per second
            AddTask( CheckConnections, CheckConnectionsInterval );

            // Check for idle people every 30 seconds
            AddTask( CheckIdles, 30000 );

            // Monitor CPU usage every 60 seconds
            AddTask( MonitorProcessorUsage, CPUMonitorInterval );

            AddTask( SavePlayerDB, PlayerDB.SaveInterval, null, 15000 );

            // Write out initial (empty) playerlist cache
            UpdatePlayerList();

            // apply AutoRank
            if( Config.GetBool( ConfigKey.AutoRankEnabled ) ) {
                AddTask( AutoRankTick, AutoRankTickInterval, null, 30000 );
            }

            // Announcements
            if( Config.GetInt( ConfigKey.AnnouncementInterval ) > 0 ) {
                AddTask( ShowRandomAnnouncement, Config.GetInt( ConfigKey.AnnouncementInterval ) * 60000 );
            }

            AddTask( DoGC, GCInterval, null, 45000 );

            // start the main loop - server is now connectible
            mainThread = new Thread( MainLoop );
            mainThread.Start();

            Heartbeat.Start();

            if( Config.GetBool( ConfigKey.IRCBot ) ) IRC.Start();

            // fire OnStart event
            if( OnStart != null ) OnStart();
            return true;
        }


        // shuts down the server and aborts threads
        // NOTE: Do not call from any of the usual threads (main, heartbeat, tasks).
        // Call from UI thread or a new separate thread only.
        public static void Shutdown( string reason ) {
            if( shuttingDown ) return;
#if DEBUG
#else
            try {
#endif
                shuttingDown = true;
                if( OnShutdownBegin != null ) OnShutdownBegin();

                Logger.Log( "Server shutting down ({0})", LogType.SystemActivity,
                            reason );

                // kick all players
                if( playerList != null ) {
                    Player[] pListCached = playerList;
                    foreach( Player player in pListCached ) {
                        // NOTE: kick packet delivery here is not currently guaranteed
                        player.session.Kick( "Server shutting down (" + reason + Color.White + ")" );
                    }
                }

                // kill the main thread
                if( mainThread != null && mainThread.IsAlive ) {
                    mainThread.Join( 5000 );
                    if( mainThread.IsAlive ) {
                        mainThread.Abort(); // temporary kludge until i find a real cause
                    }
                }

                // stop accepting new players
                if( listener != null ) {
                    listener.Stop();
                    listener = null;
                }

                // kill the heartbeat
                Heartbeat.Shutdown();

                // kill IRC bot
                IRC.Disconnect();

                // kill background tasks
                BackgroundTasks.Shutdown();

                lock( worldListLock ) {
                    // unload all worlds (includes saving)
                    foreach( World world in worlds.Values ) {
                        world.Shutdown();
                    }
                }

                if( PlayerDB.isLoaded ) PlayerDB.Save();
                if( IPBanList.isLoaded ) IPBanList.Save();

                if( OnShutdownEnd != null ) OnShutdownEnd();
#if DEBUG
#else
            } catch( Exception ex ) {
                Logger.LogAndReportCrash( "Error in Server.Shutdown", "fCraft", ex );
            }
#endif
        }


        class ShutdownParams {
            public string Reason;
            public int Delay;
            public bool KillProcess;
            public bool Restart;
        }


        public static void InitiateShutdown( string reason, int delay, bool killProcess, bool restart ) {
            new Thread( delegate( object obj ) {
                ShutdownParams param = (ShutdownParams)obj;
                Thread.Sleep( param.Delay * 1000 );
                Server.Shutdown( param.Reason );
                if( param.Restart ) {
                    Process.Start( Process.GetCurrentProcess().MainModule.FileName, String.Join( " ", args ) );
                }
                if( param.KillProcess ) {
                    Process.GetCurrentProcess().Kill();
                }
            } ).Start( new ShutdownParams {
                Reason = reason,
                Delay = delay,
                KillProcess = killProcess,
                Restart = restart
            } );
        }

        #region Worlds

        #region World List Saving/Loading

        static bool LoadWorldList() {
            if( File.Exists( WorldListFileName ) ) {
                try {
                    LoadWorldListXML();
                } catch( Exception ex ) {
                    Logger.Log( "An error occured while trying to parse the world list: {0}", LogType.FatalError, ex );
                    return false;
                }
            } else {
                Logger.Log( "Server.Start: No world list found. Creating default \"main\" world.", LogType.SystemActivity );
                mainWorld = AddWorld( "main", null, true );
            }

            if( worlds.Count == 0 ) {
                Logger.Log( "Server.Start: Could not load any of the specified worlds, or no worlds were specified. Creating default \"main\" world.", LogType.Error );
                mainWorld = AddWorld( "main", null, true );
            }

            // if there is no default world still, die.
            if( mainWorld == null ) {
                Logger.Log( "World creation failed. Shutting down.", LogType.FatalError );
                return false;
            } else {
                if( mainWorld.accessRank != RankList.LowestRank ) {
                    Logger.Log( "Server.LoadWorldList: Main world cannot have any access restrictions. " +
                                "Access permission for \"{0}\" has been reset.", LogType.Warning,
                                 mainWorld.name );
                    mainWorld.accessRank = RankList.LowestRank;
                }
                if( !mainWorld.neverUnload ) {
                    mainWorld.neverUnload = true;
                    mainWorld.LoadMap();
                }
            }

            return true;
        }


        static void LoadWorldListXML() {
            XDocument doc = XDocument.Load( WorldListFileName );
            XElement root = doc.Root;
            World firstWorld = null;
            XAttribute temp = null;
            string worldName;

            foreach( XElement el in root.Elements( "World" ) ) {
                if( (temp = el.Attribute( "name" )) == null ) {
                    Logger.Log( "Server.ParseWorldListXML: World tag with no name skipped.", LogType.Error );
                    continue;
                }
                worldName = temp.Value;
                if( !Player.IsValidName( worldName ) ) {
                    Logger.Log( "Server.ParseWorldListXML: Invalid world name skipped: \"" + worldName + "\"", LogType.Error );
                    continue;
                }

                World world = AddWorld( worldName, null, (el.Attribute( "noUnload" ) != null) );

                if( world == null ) {
                    Logger.Log( "Server.ParseWorldListXML: Error loading world \"" + worldName + "\"", LogType.Error );
                } else {
                    if( (temp = el.Attribute( "hidden" )) != null ) {
                        if( !Boolean.TryParse( temp.Value, out world.isHidden ) ) {
                            Logger.Log( "Server.ParseWorldListXML: Could not parse \"hidden\" attribute of world \"{0}\", " +
                                        "assuming NOT hidden.", LogType.Warning,
                                        worldName );
                            world.isHidden = false;
                        }
                    }
                    if( firstWorld == null ) firstWorld = world;
                    Logger.Log( "Server.ParseWorldListXML: Loaded world \"{0}\"", LogType.Debug, worldName );

                    LoadWorldRankRestriction( world, ref world.accessRank, "access", el );
                    LoadWorldRankRestriction( world, ref world.buildRank, "build", el );
                }
            }

            if( (temp = root.Attribute( "main" )) != null ) {
                mainWorld = FindWorld( temp.Value );
                // if specified main world does not exist, use first-defined world
                if( mainWorld == null && firstWorld != null ) {
                    Logger.Log( "The specified main world \"{0}\" does not exist. " +
                                "\"{1}\" was designated main instead. You can use /wmain to change it.",
                                LogType.Warning,
                                temp.Value, firstWorld.name );
                    mainWorld = firstWorld;
                }
                // if firstWorld was also null, LoadWorldList() should try creating a new mainWorld

            } else {
                mainWorld = firstWorld;
            }
        }


        static void LoadWorldRankRestriction( World world, ref Rank field, string fieldType, XElement element ) {
            XAttribute temp;
            Rank rank;
            if( (temp = element.Attribute( fieldType )) != null ) {
                if( (rank = RankList.ParseRank( temp.Value )) != null ) {
                    field = rank;
                } else {
                    Logger.Log( "Server.ParseWorldListXML: Could not parse the specified {0} class for world \"{1}\": \"{2}\". No access limit was set.", LogType.Error,
                                fieldType,
                                world.name,
                                temp.Value );
                    field = RankList.LowestRank;
                }
            } else {
                field = RankList.LowestRank;
            }
        }


        public static void SaveWorldList() {
            // Save world list
            try {
                string tempWorldListFile = WorldListFileName + ".tmp";
                string backupWorldListFile = WorldListFileName + ".backup";
                XDocument doc = new XDocument();
                XElement root = new XElement( "fCraftWorldList" );
                XElement temp;
                lock( worldListLock ) {
                    foreach( World world in worlds.Values ) {
                        temp = new XElement( "World" );
                        temp.Add( new XAttribute( "name", world.name ) );
                        temp.Add( new XAttribute( "access", world.accessRank ) );
                        temp.Add( new XAttribute( "build", world.buildRank ) );
                        if( world.neverUnload ) {
                            temp.Add( new XAttribute( "noUnload", true ) );
                        }
                        if( world.isHidden ) {
                            temp.Add( new XAttribute( "hidden", true ) );
                        }
                        root.Add( temp );
                    }
                    root.Add( new XAttribute( "main", mainWorld.name ) );
                }
                doc.Add( root );
                doc.Save( tempWorldListFile );
                if( File.Exists( WorldListFileName ) ) {
                    File.Replace( tempWorldListFile, WorldListFileName, backupWorldListFile );
                } else {
                    File.Move( tempWorldListFile, WorldListFileName );
                }
            } catch( Exception ex ) {
                Logger.Log( "Server.SaveWorldList: An error occured while trying to save the world list: {0}", LogType.Error, ex );
            }
        }

        #endregion

        public static World AddWorld( string name, Map map, bool neverUnload ) {
            lock( worldListLock ) {
                if( worlds.ContainsKey( name ) ) return null;
                if( !Player.IsValidName( name ) ) return null;
                World newWorld = new World( name );
                newWorld.neverUnload = neverUnload;

                if( map != null ) {
                    // if a map is given
                    newWorld.map = map;
                    if( !neverUnload ) {
                        newWorld.UnloadMap();// UnloadMap also saves the map
                    } else {
                        newWorld.SaveMap( null );
                    }

                } else {
                    // generate default map
                    if( neverUnload ) newWorld.LoadMap();
                }
                worlds.Add( name.ToLower(), newWorld );

                newWorld.UpdatePlayerList();

                newWorld.updateTaskId = AddTask( UpdateBlocks, Config.GetInt( ConfigKey.TickInterval ), newWorld );

                if( Config.GetInt( ConfigKey.SaveInterval ) > 0 ) {
                    int saveInterval = Config.GetInt( ConfigKey.SaveInterval ) * 1000;
                    newWorld.saveTaskId = AddTask( SaveMap, saveInterval, newWorld, saveInterval );
                }

                if( Config.GetInt( ConfigKey.BackupInterval ) > 0 ) {
                    int backupInterval = Config.GetInt( ConfigKey.BackupInterval ) * 1000 * 60;
                    newWorld.backupTaskId = AddTask( AutoBackup, backupInterval, newWorld, (Config.GetBool( ConfigKey.BackupOnStartup ) ? 0 : backupInterval) );
                }

                return newWorld;
            }
        }


        public static World FindWorld( string name ) {
            if( name == null ) return null;
            lock( worldListLock ) {
                if( worlds.ContainsKey( name.ToLower() ) ) {
                    return worlds[name.ToLower()];
                } else {
                    return null;
                }
            }
        }


        public static World[] FindWorlds( string name ) {
            if( name == null ) return null;
            World[] tempList;
            lock( worldListLock ) {
                tempList = worlds.Values.ToArray();
            }

            List<World> results = new List<World>();
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i] != null ) {
                    if( tempList[i].name.Equals( name, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Clear();
                        results.Add( tempList[i] );
                        break;
                    } else if( tempList[i].name.StartsWith( name, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Add( tempList[i] );
                    }
                }
            }
            return results.ToArray();
        }


        public static bool RemoveWorld( string name ) {
            lock( worldListLock ) {
                World worldToDelete = FindWorld( name );
                if( worldToDelete == null || worldToDelete == mainWorld ) {
                    return false;
                } else {
                    Player[] worldPlayerList = worldToDelete.playerList;
                    worldToDelete.SendToAll( "&SYou have been moved to the main world." );
                    foreach( Player player in worldPlayerList ) {
                        player.session.JoinWorld( mainWorld, null );
                    }
                    worldToDelete.SaveMap( null );
                    lock( taskListLock ) {
                        tasks.Remove( worldToDelete.updateTaskId );
                        // If saveTaskId or backupTaskId were not defined, Remove does nothing
                        // because default value for saveTaskId and backupTaskId is -1
                        tasks.Remove( worldToDelete.saveTaskId );
                        tasks.Remove( worldToDelete.backupTaskId );
                        UpdateTaskListCache();
                    }
                    worlds.Remove( name.ToLower() );
                    SaveWorldList();
                    return true;
                }
            }
        }


        // Note: no autocompletion
        public static bool RenameWorld( string oldName, string newName ) {
            lock( worldListLock ) {
                World oldWorld = FindWorld( oldName );
                World newWorld = FindWorld( newName );
                if( oldWorld == null || (newWorld != null && newWorld != oldWorld) ) return false;
                worlds.Remove( oldName.ToLower() );
                oldWorld.name = newName;
                worlds.Add( newName.ToLower(), oldWorld );
                return true;
            }
        }


        public static bool ReplaceWorld( string name, World newWorld ) {
            lock( worldListLock ) {
                World oldWorld = FindWorld( name );
                if( oldWorld == null ) return false;

                newWorld.name = oldWorld.name;
                if( oldWorld == mainWorld ) {
                    mainWorld = newWorld;
                }

                // initialize the player list cache
                newWorld.UpdatePlayerList();

                // swap worlds
                worlds[name.ToLower()] = newWorld;
                lock( taskListLock ) {
                    // removes tasks associated with the old world
                    tasks.Remove( oldWorld.updateTaskId );
                    tasks.Remove( oldWorld.saveTaskId );
                    tasks.Remove( oldWorld.backupTaskId );

                    // if the new world was previously associated with some tasks, remove the old one, and reregister
                    tasks.Remove( newWorld.updateTaskId );
                    tasks.Remove( newWorld.saveTaskId );
                    tasks.Remove( newWorld.backupTaskId );

                    UpdateTaskListCache();
                }

                // adds tasks to the new world
                newWorld.updateTaskId = AddTask( UpdateBlocks, Config.GetInt( ConfigKey.TickInterval ), newWorld );

                if( Config.GetInt( ConfigKey.SaveInterval ) > 0 ) {
                    int saveInterval = Config.GetInt( ConfigKey.SaveInterval ) * 1000;
                    newWorld.saveTaskId = AddTask( SaveMap, saveInterval, newWorld, saveInterval );
                }

                if( Config.GetInt( ConfigKey.BackupInterval ) > 0 ) {
                    int backupInterval = Config.GetInt( ConfigKey.BackupInterval ) * 1000 * 60;
                    newWorld.backupTaskId = AddTask( AutoBackup, backupInterval, newWorld, (Config.GetBool( ConfigKey.BackupOnStartup ) ? 0 : backupInterval) );
                }
                return true;
            }
        }


        public static int CountLoadedWorlds() {
            int counter = 0;
            lock( worldListLock ) {
                foreach( World world in worlds.Values ) {
                    if( world.map != null ) counter++;
                }
            }
            return counter;
        }

        #endregion


        #region Messaging / Packet Sending

        // Send a low-priority packet to everyone
        // If 'except' is not null, excludes specified player
        public static void SendToAllDelayed( Packet packet, Player except ) {
            Player[] tempList = playerList;
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i] != except ) {
                    tempList[i].SendDelayed( packet );
                }
            }
        }


        // Send a normal priority packet to everyone
        public static void SendToAll( Packet packet ) {
            SendToAll( packet, null );
        }


        // Send a normal priority packet to everyone
        // If 'except' is not null, excludes specified player
        public static void SendToAll( Packet packet, Player except ) {
            Player[] tempList = playerList;
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i] != except ) {
                    tempList[i].Send( packet );
                }
            }
        }


        // Send a message to everyone (except a specified player)
        // Wraps String.Format() for easy formatting
        public static void SendToAllExcept( string message, Player except, params object[] args ) {
            if( args.Length > 0 ) message = String.Format( message, args );
            if( except != Player.Console ) Logger.LogConsole( message );
            foreach( Packet p in PacketWriter.MakeWrappedMessage( "> ", message, false ) ) {
                SendToAll( p, except );
            }
        }


        // Send a message to everyone
        // Wraps String.Format() for easy formatting
        public static void SendToAll( string message, params object[] args ) {
            SendToAllExcept( message, null, args );
        }

        public static void SendToAllExceptIgnored( Player origin, string message, Player except, params object[] args ) {
            if( args.Length > 0 ) message = String.Format( message, args );
            foreach( Packet p in PacketWriter.MakeWrappedMessage( "> ", message, false ) ) {
                Player[] tempList = playerList;
                for( int i = 0; i < tempList.Length; i++ ) {
                    if( tempList[i] != except && !tempList[i].IsIgnored( origin.info ) ) {
                        tempList[i].Send( p );
                    }
                }
            }
        }


        // Sends a packet to everyone who CAN see 'source' player
        public static void SendToSeeing( Packet packet, Player source ) {
            Player[] playerListCopy = playerList;
            for( int i = 0; i < playerListCopy.Length; i++ ) {
                if( playerListCopy[i] != source && playerListCopy[i].CanSee( source ) ) {
                    playerListCopy[i].Send( packet );
                }
            }
        }


        // Sends a string to everyone who CAN see 'source' player
        public static void SendToSeeing( string message, Player source ) {
            foreach( Packet packet in PacketWriter.MakeWrappedMessage( ">", message, false ) ) {
                SendToSeeing( packet, source );
            }
        }


        // Sends a packet to everyone who CAN'T see 'source' player
        public static void SendToBlind( Packet packet, Player source ) {
            Player[] playerListCopy = playerList;
            for( int i = 0; i < playerListCopy.Length; i++ ) {
                if( playerListCopy[i] != source && !playerListCopy[i].CanSee( source ) ) {
                    playerListCopy[i].Send( packet );
                }
            }
        }


        // Sends a string to everyone who CAN'T see 'source' player
        public static void SendToBlind( string message, Player source ) {
            foreach( Packet packet in PacketWriter.MakeWrappedMessage( ">", message, false ) ) {
                SendToBlind( packet, source );
            }
        }

        // Sends a packet to all players of a specific rank
        public static void SendToRank( Packet packet, Rank rank ) {
            Player[] tempList = playerList;
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i].info.rank == rank ) {
                    tempList[i].Send( packet );
                }
            }
        }


        // Sends a string to all players of a specific rank
        public static void SendToRank( Player origin, string message, Rank rank ) {
            foreach( Packet packet in PacketWriter.MakeWrappedMessage( ">", message, false ) ) {
                Player[] tempList = playerList;
                for( int i = 0; i < tempList.Length; i++ ) {
                    if( tempList[i].info.rank == rank && !tempList[i].IsIgnored( origin.info ) ) {
                        tempList[i].Send( packet );
                    }
                }
            }
        }

        #endregion


        #region Events
        // events
        public static event SimpleEventHandler OnInit;
        public static event SimpleEventHandler OnStart;
        public static event PlayerConnectedEventHandler OnPlayerConnected;
        public static event PlayerDisconnectedEventHandler OnPlayerDisconnected;
        public static event PlayerKickedEventHandler OnPlayerKicked;
        public static event PlayerRankChangedEventHandler OnRankChanged;
        public static event URLChangeEventHandler OnURLChanged;
        public static event SimpleEventHandler OnShutdownBegin;
        public static event SimpleEventHandler OnShutdownEnd;
        public static event PlayerChangedWorldEventHandler OnPlayerChangedWorld;
        public static event LogEventHandler OnLog;
        public static event PlayerListChangedHandler OnPlayerListChanged;
        public static event PlayerSentMessageEventHandler OnPlayerSentMessage;
        public static event PlayerBanStatusChangedEventHandler OnPlayerBanned;
        public static event PlayerBanStatusChangedEventHandler OnPlayerUnbanned;

        internal static void FireURLChangeEvent( string URL ) {
            if( OnURLChanged != null ) OnURLChanged( URL );
        }

        internal static void FireLogEvent( string message, LogType type ) {
            if( OnLog != null ) OnLog( message, type );
        }

        internal static bool FirePlayerConnectedEvent( Session session ) {
            bool cancel = false;
            if( OnPlayerConnected != null ) OnPlayerConnected( session, ref cancel );
            return !cancel;
        }

        internal static bool FirePlayerRankChange( PlayerInfo target, Player player, Rank oldRank, Rank newRank, string reason ) {
            bool cancel = false;
            if( OnRankChanged != null ) OnRankChanged( target, player, oldRank, newRank, reason, ref cancel );
            return !cancel;
        }

        internal static void FireWorldChangedEvent( Player player, World oldWorld, World newWorld ) {
            if( OnPlayerChangedWorld != null ) OnPlayerChangedWorld( player, oldWorld, newWorld );
        }

        internal static void FirePlayerListChangedEvent() {
            if( OnPlayerListChanged != null ) {
                Player[] playerListCache = playerList;
                string[] list = new string[playerListCache.Length];
                for( int i = 0; i < list.Length; i++ ) {
                    list[i] = playerListCache[i].info.rank.Name + " - " + playerListCache[i].name;
                }
                Array.Sort<string>( list );
                OnPlayerListChanged( list );
            }
        }

        internal static bool FireSentMessageEvent( Player player, ref string message ) {
            bool cancel = false;
            if( OnPlayerSentMessage != null ) {
                OnPlayerSentMessage( player, player.world, ref message, ref cancel );
            }
            return !cancel;
        }

        internal static void FirePlayerKickedEvent( Player player, Player kicker, string reason ) {
            if( OnPlayerKicked != null ) {
                OnPlayerKicked( player, kicker, reason );
            }
        }

        internal static void FirePlayerBannedEvent( PlayerInfo player, Player banner, string reason ) {
            if( OnPlayerBanned != null ) {
                OnPlayerBanned( player, banner, reason );
            }
        }

        internal static void FirePlayerUnbannedEvent( PlayerInfo player, Player unbanner, string reason ) {
            if( OnPlayerUnbanned != null ) {
                OnPlayerUnbanned( player, unbanner, reason );
            }
        }

        #endregion


        #region Scheduler

        /// <summary>
        /// Represents the state of a scheduled task, for Server.MainLoop
        /// </summary>
        sealed class ScheduledTask {
            public DateTime nextTime;
            public int interval;
            public TaskCallback callback;
            public object param;
            public bool enabled = true;
        }

        static int taskIdCounter;
        static Dictionary<int, ScheduledTask> tasks = new Dictionary<int, ScheduledTask>();
        static ScheduledTask[] taskList;
        static Thread mainThread;
        public static DateTime serverStart;
        public static bool shuttingDown;
        static object taskListLock = new object();

        internal static void MainLoop() {
#if !DEBUG
            try {
#endif
                ScheduledTask[] taskCache;
                ScheduledTask task;
                while( !shuttingDown ) {
                    taskCache = taskList;
                    for( int i = 0; i < taskCache.Length; i++ ) {
                        task = taskCache[i];
                        if( task.enabled && task.nextTime < DateTime.UtcNow ) {
#if DEBUG
                        task.callback( task.param );
#else
                            try {
                                task.callback( task.param );
                            } catch( Exception ex ) {
                                Logger.LogAndReportCrash( "Exception was thrown by a scheduled task", "fCraft", ex );
                            }
#endif
                            task.nextTime += TimeSpan.FromMilliseconds( task.interval );
                        }
                    }
                    Thread.Sleep( 1 );
                }
#if !DEBUG
            } catch( Exception ex ) {
                Logger.LogAndReportCrash( "Fatal error in fCraft.Server main loop", "fCraft", ex );
            }
#endif
        }

        // checks for incoming connections
        const int CheckConnectionsInterval = 250;
        internal static void CheckConnections( object param ) {
            if( listener.Pending() ) {
                try {
                    Session newSession = new Session( listener.AcceptTcpClient() );
                    newSession.Start();
                } catch( Exception ex ) {
                    Logger.Log( "Server.CheckConnections: Could not accept incoming connection: " + ex, LogType.Error );
                }
            }
        }

        const int AutoRankTickInterval = 60000; // 60 seconds
        static void AutoRankTick( object param ) {
            BackgroundTasks.Add( (TaskCallback)delegate( object innerParam ) {
                AutoRankCommands.DoAutoRankAll( Player.Console, PlayerDB.GetPlayerListCopy(), false, "~AutoRank" );
            }, null );
        }

        static void AutoBackup( object param ) {
            World world = (World)param;
            if( world.map == null ) return;
            world.map.SaveBackup( world.GetMapName(), String.Format( "backups/{0}_{1:yyyy-MM-ddTHH-mm}.fcm", world.name, DateTime.Now ), true );
        }

        static void SaveMap( object param ) {
            World world = (World)param;
            if( world.map == null ) return;
            if( world.map.changedSinceSave ) {
                BackgroundTasks.Add( world.SaveMap, null );
            }
        }

        static void SavePlayerDB( object param ) {
            BackgroundTasks.Add( (TaskCallback)delegate( object innerParam ) {
                PlayerDB.Save();
            }, null );
        }

        static void UpdateBlocks( object param ) {
            World world = (World)param;
            if( world.map == null ) return;
            world.map.ProcessUpdates();
        }

        static void CheckIdles( object param ) {
            Player[] tempPlayerList = playerList;
            foreach( Player player in tempPlayerList ) {
                if( player.info.rank.IdleKickTimer > 0 ) {
                    if( DateTime.UtcNow.Subtract( player.idleTimer ).TotalMinutes >= player.info.rank.IdleKickTimer ) {
                        SendToAllExcept( "{0}&S was kicked for being idle for {1} min", player,
                                         player.GetClassyName(),
                                         player.info.rank.IdleKickTimer.ToString() );
                        AdminCommands.DoKick( Player.Console, player, "Idle for " + player.info.rank.IdleKickTimer + " minutes", true );
                        player.ResetIdleTimer(); // to prevent kick from firing more than once
                    }
                }
            }
        }

        const int GCInterval = 60000;
        static void DoGC( object param ) {
            if( GCRequested ) {
                GCRequested = false;
                BackgroundTasks.Add( (TaskCallback)delegate( object innerParam ) {
                    GC.Collect( GC.MaxGeneration, GCCollectionMode.Forced );
                    Logger.Log( "Server.DoGC: Collected on schedule.", LogType.Debug );
                }, null );
            }
        }


        internal static int AddTask( TaskCallback task, int interval ) {
            return AddTask( task, interval, null, 0 );
        }

        internal static int AddTask( TaskCallback task, int interval, object param ) {
            return AddTask( task, interval, param, 0 );
        }

        internal static int AddTask( TaskCallback task, int interval, object param, int delay ) {
            ScheduledTask newTask = new ScheduledTask();
            newTask.nextTime = DateTime.UtcNow.AddMilliseconds( delay );
            newTask.callback = task;
            newTask.interval = interval;
            newTask.param = param;
            lock( taskListLock ) {
                tasks.Add( ++taskIdCounter, newTask );
                UpdateTaskListCache();
            }
            return taskIdCounter;
        }

        internal static void TaskToggle( int id, bool enabled ) {
            tasks[id].nextTime = DateTime.UtcNow;
            tasks[id].enabled = enabled;
            UpdateTaskListCache();
        }

        static void UpdateTaskListCache() {
            List<ScheduledTask> tempTaskList = new List<ScheduledTask>();
            lock( taskListLock ) {
                foreach( ScheduledTask task in tasks.Values ) {
                    if( task.enabled ) {
                        tempTaskList.Add( task );
                    }
                }
            }
            taskList = tempTaskList.ToArray();
        }

        public const string AnnouncementsFile = "announcements.txt";
        static void ShowRandomAnnouncement( object param ) {
            if( File.Exists( AnnouncementsFile ) ) {
                string[] lines = File.ReadAllLines( AnnouncementsFile );
                if( lines.Length == 0 ) return;
                string line = lines[new Random().Next( 0, lines.Length )].Trim();
                if( line.Length > 0 ) {
                    if( line.StartsWith( "&" ) ) {
                        SendToAll( "{0}", line );
                    } else {
                        SendToAll( "{0}{1}", Color.Announcement, line );
                    }
                }
            }
        }


        static TimeSpan oldCPUTime = new TimeSpan( 0 );
        public static float CPUUsageTotal, CPUUsageLastMinute;
        const int CPUMonitorInterval = 60000; // 1 minute
        public static void MonitorProcessorUsage( object param ) {
            TimeSpan newCPUTime = Process.GetCurrentProcess().TotalProcessorTime;
            CPUUsageLastMinute = (float)((newCPUTime - oldCPUTime).TotalMilliseconds / (Environment.ProcessorCount * CPUMonitorInterval));
            CPUUsageTotal = (float)(newCPUTime.TotalMilliseconds / (Environment.ProcessorCount * DateTime.Now.Subtract( serverStart ).TotalMilliseconds));
            oldCPUTime = newCPUTime;
        }

        #endregion


        #region Utilities

        static bool GCRequested = false;
        public static void RequestGC() {
            GCRequested = true;
        }


        internal static string Salt = "";

        // To keep server restarts as smooth as possible, fCreft stores the salt
        // from the previous session in the config, and checks it if verification
        // against the current salt fails.
        internal static string OldSalt = "";


        static void GenerateSalt() {
            // generate random salt
            Random rand = new Random();
            int saltLength = rand.Next( 12, 17 );
            string saltChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-.~";
            for( int i = 0; i < saltLength; i++ ) {
                Salt += saltChars[rand.Next( 0, saltChars.Length - 1 )];
            }
        }

        public static bool VerifyName( string name, string hash, string salt ) {
            while( hash.Length < 32 ) {
                hash = "0" + hash;
            }
            MD5 hasher = MD5.Create();
            byte[] data = hasher.ComputeHash( Encoding.ASCII.GetBytes( salt + name ) );
            for( int i = 0; i < 16; i += 2 ) {
                if( hash[i] + "" + hash[i + 1] != data[i / 2].ToString( "x2" ) ) {
                    return false;
                }
            }
            return true;
        }


        public static int CalculateMaxPacketsPerUpdate( World world ) {
            int packetsPerTick = (int)(packetsPerSecond / ticksPerSecond);
            int maxPacketsPerUpdate = (int)(maxUploadSpeed / ticksPerSecond * 128);

            int playerCount = world.playerList.Length;
            if( playerCount > 0 && !world.isFlushing ) {
                maxPacketsPerUpdate /= playerCount;
                if( maxPacketsPerUpdate > packetsPerTick ) {
                    maxPacketsPerUpdate = packetsPerTick;
                }
            } else {
                maxPacketsPerUpdate = MaxBlockUpdatesPerTick;
            }

            return maxPacketsPerUpdate;
        }

        public static bool CheckForFCraftProcesses() {
            try {
                Process[] processList = Process.GetProcesses();

                foreach( Process process in processList ) {
                    if( process.ProcessName.ToLower() == "fcraftui" ||
                        process.ProcessName.ToLower() == "configtool" ||
                        process.ProcessName.ToLower() == "fcraftconsole" ) {
                        if( process.Id != Process.GetCurrentProcess().Id ) {
                            Logger.Log( "Another fCraft process detected running: {0}", LogType.Warning, process.ProcessName );
                            return true;
                        }
                    }
                }
                return false;

            } catch( Exception ex ) {
                Logger.Log( "Server.CheckForFCraftProcesses: {0}", LogType.Debug, ex );
                return false;
            }
        }


        static Regex regexIP = new Regex( @"\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b", RegexOptions.Compiled );
        public static bool IsIP( string IPString ) {
            return regexIP.IsMatch( IPString );
        }

        #region Extension Methods

        public static bool IsLAN( this IPAddress addr ) {
            byte[] bytes = addr.GetAddressBytes();
            return (bytes[0] == 192 && bytes[1] == 168);
        }

        #endregion

        #endregion


        #region PlayerList

        public static void KickGhostsAndRegisterSession( Session newSession ) {
            List<Session> sessionsToKick = new List<Session>();
            lock( sessionLock ) {
                foreach( Session s in sessions ) {
                    if( s.player.name.Equals( newSession.player.name, StringComparison.OrdinalIgnoreCase ) ) {
                        sessionsToKick.Add( s );
                        s.Kick( "Connected from elsewhere!" );
                        Logger.Log( "Session.LoginSequence: Player {0} logged in. Ghost was kicked.", LogType.SuspiciousActivity,
                                    s.player.name );
                    }
                }
                sessions.Add( newSession );
            }
            foreach( Session ses in sessionsToKick ) {
                ses.WaitForDisconnect();
            }
        }


        public static string MakePlayerConnectedMessage( Player player, bool firstTime, World world ) {
            if( firstTime ) {
                return String.Format( "&S{0} ({1}&S) connected for the first time, joined {2}",
                                      player.name,
                                      player.info.rank.GetClassyName(),
                                      world.GetClassyName() );
            } else {
                return String.Format( "&S{0} ({1}&S) connected, joined {2}",
                                      player.name,
                                      player.info.rank.GetClassyName(),
                                      world.GetClassyName() );
            }
        }


        // Add a newly-logged-in player to the list, and notify existing players.
        public static bool RegisterPlayer( Player player ) {
            lock( playerListLock ) {
                if( players.Count >= Config.GetInt( ConfigKey.MaxPlayers ) && !player.info.rank.ReservedSlot ||
                    players.Count == Config.MaxPlayersSupported ) {
                    return false;
                }
                for( int i = 0; i < Config.MaxPlayersSupported; i++ ) {
                    if( !players.ContainsKey( i ) ) {
                        player.id = i;
                        players[i] = player;
                        UpdatePlayerList();
                        return true;
                    }
                }
                return false;
            }
        }


        // Remove player from the list, and notify remaining players
        public static void UnregisterPlayer( Player player ) {
            if( player == null ) {
                throw new ArgumentNullException( "player", "Server.UnregisterPlayer: player cannot be null." );
            }

            lock( worldListLock ) {
                lock( playerListLock ) {
                    if( players.ContainsKey( player.id ) ) {
                        SendToAll( PacketWriter.MakeRemoveEntity( player.id ) );
                        Logger.Log( "{0} left the server.", LogType.UserActivity,
                                    player.name );
                        if( player.session.hasRegistered ) {
                            SendToAll( "&SPlayer {0}&S left the server.", player.GetClassyName() );

                            // better safe than sorry: go through ALL worlds looking for leftover players
                            foreach( World world in worlds.Values ) {
                                world.ReleasePlayer( player );
                            }
                            players.Remove( player.id );
                            UpdatePlayerList();
                        }

                        if( player.info != null ) player.info.ProcessLogout( player );
                    } else {
                        Logger.Log( "Server.UnregisterPlayer: Trying to unregister a non-existent player.",
                                    LogType.Warning );
                    }
                }
            }
        }


        public static void UnregisterSession( Session session ) {
            lock( sessionLock ) {
                if( sessions.Contains( session ) ) {
                    sessions.Remove( session );
                    if( OnPlayerDisconnected != null ) OnPlayerDisconnected( session );
                }
            }
        }


        public static void UpdatePlayerList() {
            lock( playerListLock ) {
                Player[] newPlayerList = new Player[players.Count];
                int i = 0;
                foreach( Player player in players.Values ) {
                    newPlayerList[i++] = player;
                }
                playerList = newPlayerList.OrderBy( player => player.name ).ToArray<Player>();
            }
            FirePlayerListChangedEvent();
        }


        // Find player by name using autocompletion (IGNORES HIDDEN PERMISSIONS)
        public static Player[] FindPlayers( string name ) {
            Player[] tempList = playerList;
            List<Player> results = new List<Player>();
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i] != null ) {
                    if( tempList[i].name.Equals( name, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Clear();
                        results.Add( tempList[i] );
                        break;
                    } else if( tempList[i].name.StartsWith( name, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Add( tempList[i] );
                    }
                }
            }
            return results.ToArray();
        }


        // Find player by name using autocompletion (returns only whose whom player can see)
        public static Player[] FindPlayers( Player player, string name ) {
            Player[] tempList = playerList;
            List<Player> results = new List<Player>();
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i] != null && player.CanSee( tempList[i] ) ) {
                    if( tempList[i].name.Equals( name, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Clear();
                        results.Add( tempList[i] );
                        break;
                    } else if( tempList[i].name.StartsWith( name, StringComparison.OrdinalIgnoreCase ) ) {
                        results.Add( tempList[i] );
                    }
                }
            }
            return results.ToArray();
        }


        // Find player by IP
        public static List<Player> FindPlayers( IPAddress ip ) {
            Player[] tempList = playerList;
            List<Player> results = new List<Player>();
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i] != null && tempList[i].session.GetIP().ToString() == ip.ToString() ) {
                    results.Add( tempList[i] );
                }
            }
            return results;
        }


        // Get player by name without autocompletion
        public static Player FindPlayerExact( string name ) {
            name = name.ToLower();
            Player[] tempList = playerList;
            for( int i = 0; i < tempList.Length; i++ ) {
                if( tempList[i] != null && tempList[i].name.Equals( name, StringComparison.OrdinalIgnoreCase ) ) {
                    return tempList[i];
                }
            }
            return null;
        }


        public static int GetPlayerCount( bool includeHiddenPlayers ) {
            if( includeHiddenPlayers ) {
                return playerList.Length;
            } else {
                int count = 0;
                Player[] playerListCache = playerList;
                foreach( Player player in playerListCache ) {
                    if( !player.isHidden ) count++;
                }
                return count;
            }
        }

        #endregion
    }
}