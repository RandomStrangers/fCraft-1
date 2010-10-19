﻿// Copyright 2009, 2010 Matvei Stefarov <me@matvei.org>
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;
using System.Threading;


namespace fCraft {
    public static class PlayerDB {
        static StringTree tree = new StringTree();
        static List<PlayerInfo> list = new List<PlayerInfo>();
        public const int SaveInterval = 60000; // 60s

        static int MaxID = 255;

        public static string ToCompactString( this TimeSpan span ) {
            return String.Format( "{0}.{1:00}:{2:00}:{3:00}",
                span.Days, span.Hours, span.Minutes, span.Seconds );
        }

        public static string ToCompactString( this DateTime date ) {
            return date.ToString( "yyyy'-'MM'-'dd'T'HH':'mm':'ssK" );
        }


        public const string DBFile = "PlayerDB.txt",
                            Header = " fCraft PlayerDB | Row format: " +
                                     "playerName,lastIP,rank,rankChangeDate,rankChangeBy," +
                                     "banStatus,banDate,bannedBy,unbanDate,unbannedBy," +
                                     "firstLoginDate,lastLoginDate,lastFailedLoginDate," +
                                     "lastFailedLoginIP,failedLoginCount,totalTimeOnServer," +
                                     "blocksBuilt,blocksDeleted,timesVisited," +
                                     "linesWritten,UNUSED,UNUSED,previousRank,rankChangeReason," +
                                     "timesKicked,timesKickedOthers,timesBannedOthers,UID," +
                                     "rankChangeType,lastKickDate,LastSeen,BlocksDrawn,lastKickBy,lastKickReason";

        public static ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
        public static bool isLoaded;


        public static PlayerInfo AddFakeEntry( string name, RankChangeType _rankChangeType ) {
            PlayerInfo info = new PlayerInfo( name, RankList.DefaultRank, false, _rankChangeType );
            locker.EnterWriteLock();
            try {
                list.Add( info );
                tree.Add( info.name, info );
            } finally {
                locker.ExitWriteLock();
            }
            return info;
        }


        #region Saving/Loading

        public static void Load() {
            if( File.Exists( DBFile ) ) {
                locker.EnterWriteLock();
                try {
                    using( StreamReader reader = File.OpenText( DBFile ) ) {

                        string header = reader.ReadLine();// header
                        int maxIDField;

                        // first number of the header is MaxID
                        if( Int32.TryParse( header.Split( ' ' )[0], out maxIDField ) ) {
                            if( maxIDField >= 255 ) {// IDs start at 256
                                MaxID = maxIDField;
                            }
                        }

                        while( !reader.EndOfStream ) {
                            string[] fields = reader.ReadLine().Split( ',' );
                            if( fields.Length >= PlayerInfo.MinFieldCount && fields.Length <= PlayerInfo.MaxFieldCount ) {
                                try {
                                    PlayerInfo info = new PlayerInfo( fields );
                                    PlayerInfo dupe = tree.Get( info.name );
                                    if( dupe == null ) {
                                        tree.Add( info.name, info );
                                        list.Add( info );
                                    } else {
                                        Logger.Log( "PlayerDB.Load: Duplicate record for player \"{0}\" skipped.", LogType.Error, info.name );
                                    }
                                } catch( FormatException ex ) {
                                    Logger.Log( "PlayerDB.Load: Could not parse a record: {0}.", LogType.Error, ex );
                                } catch( IOException ex ) {
                                    Logger.Log( "PlayerDB.Load: Error while trying to read from file: {0}.", LogType.Error, ex );
                                }
                            } else {
                                Logger.Log( "PlayerDB.Load: Unexpected field count ({0}), expecting between {1} and {2} fields for a PlayerDB entry.", LogType.Error,
                                            fields.Length,
                                            PlayerInfo.MinFieldCount,
                                            PlayerInfo.MaxFieldCount );
                            }
                        }
                    }
                } finally {
                    locker.ExitWriteLock();
                }
                Logger.Log( "PlayerDB.Load: Done loading player DB ({0} records).", LogType.Debug, tree.Count() );
                list.TrimExcess();
            } else {
                Logger.Log( "PlayerDB.Load: No player DB file found.", LogType.Warning );
            }
            isLoaded = true;
        }


        public static void Save() {
            Logger.Log( "PlayerDB.Save: Saving player database ({0} records).", LogType.Debug, tree.Count() );
            string tempFile = Path.GetTempFileName();

            locker.EnterReadLock();
            try {
                using( StreamWriter writer = File.CreateText( tempFile ) ) {
                    writer.WriteLine( MaxID + Header );
                    foreach( PlayerInfo entry in list ) {
                        writer.WriteLine( entry.Serialize() );
                    }
                }
            } finally {
                locker.ExitReadLock();
            }
            try {
                File.Delete( DBFile );
                File.Move( tempFile, DBFile );
            } catch( Exception ex ) {
                Logger.Log( "PlayerDB.Save: An error occured while trying to save PlayerDB: " + ex, LogType.Error );
            }
        }

        #endregion


        #region Lookup

        public static PlayerInfo FindPlayerInfo( Player player ) {
            if( player == null ) return null;
            PlayerInfo info;
            locker.EnterUpgradeableReadLock();
            try {
                info = tree.Get( player.name );
                if( info == null ) {
                    info = new PlayerInfo( player );
                    locker.EnterWriteLock();
                    try {
                        tree.Add( player.name, info );
                        list.Add( info );
                    } finally {
                        locker.ExitWriteLock();
                    }
                }
            } finally {
                locker.ExitUpgradeableReadLock();
            }
            return info;
        }


        public static List<PlayerInfo> FindPlayersByIP( IPAddress address ) {
            List<PlayerInfo> result = new List<PlayerInfo>();
            locker.EnterReadLock();
            try {
                foreach( PlayerInfo info in list ) {
                    if( info.lastIP.ToString() == address.ToString() ) {
                        result.Add( info );
                    }
                }
            } finally {
                locker.ExitReadLock();
            }
            return result;
        }


        public static bool FindPlayerInfo( string name, out PlayerInfo info ) {
            if( name == null ) {
                info = null;
                return false;
            }

            bool noDupe;
            locker.EnterReadLock();
            try {
                noDupe = tree.Get( name, out info );
            } finally {
                locker.ExitReadLock();
            }

            return noDupe;
        }


        public static PlayerInfo FindPlayerInfoExact( string name ) {
            if( name == null ) return null;
            PlayerInfo info;
            locker.EnterReadLock();
            try {
                info = tree.Get( name );
            } finally {
                locker.ExitReadLock();
            }

            return info;
        }

        #endregion


        #region Stats

        public static int CountBannedPlayers() {
            int banned = 0;
            locker.EnterReadLock();
            try {
                foreach( PlayerInfo info in list ) {
                    if( info.banned ) banned++;
                }
                return banned;
            } finally {
                locker.ExitReadLock();
            }
        }


        public static int CountTotalPlayers() {
            return list.Count;
        }


        public static int CountPlayersByRank( Rank pc ) {
            int count = 0;
            locker.EnterReadLock();
            try {
                foreach( PlayerInfo info in list ) {
                    if( info.rank == pc ) count++;
                }
                return count;
            } finally {
                locker.ExitReadLock();
            }
        }

        #endregion


        public static int GetNextID() {
            return Interlocked.Increment( ref MaxID );
        }


        public static int MassRankChange( Player player, Rank from, Rank to, bool silent ) {
            int affected = 0;
            locker.EnterWriteLock();
            try {
                foreach( PlayerInfo info in list ) {
                    if( info.rank == from ) {
                        Player target = Server.FindPlayerExact( info.name );
                        AdminCommands.DoChangeRank( player, info, target, to, "~MassRank", silent );
                        affected++;
                    }
                }
                return affected;
            } finally {
                locker.ExitWriteLock();
            }
        }


        public static PlayerInfo[] GetPlayerListCopy() {
            locker.EnterReadLock();
            try {
                return list.ToArray();
            } finally {
                locker.ExitReadLock();
            }
        }


        public static PlayerInfo[] GetPlayerListCopy( Rank pc ) {
            locker.EnterReadLock();
            try {
                List<PlayerInfo> tempList = new List<PlayerInfo>();
                foreach( PlayerInfo info in list ) {
                    if( info.rank == pc ) {
                        tempList.Add( info );
                    }
                }
                return tempList.ToArray();
            } finally {
                locker.ExitReadLock();
            }
        }
    }
}