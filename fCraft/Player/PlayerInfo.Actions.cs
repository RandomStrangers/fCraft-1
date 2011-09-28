﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;
using System.Linq;
using System.Net;
using System.Threading;
using fCraft.Events;
using fCraft.Drawing;
using JetBrains.Annotations;

namespace fCraft {
    public sealed partial class PlayerInfo {
        object actionLock = new object();

        #region Ban / Unban

        /// <summary> Bans given player. Kicks if online. Throws PlayerOpException on problems. </summary>
        /// <param name="player"> Player who is banning. </param>
        /// <param name="reason"> Reason for ban. May be empty, if permitted by server configuration. </param>
        /// <param name="announce"> Whether ban should be publicly announced on the server. </param>
        /// <param name="raiseEvents"> Whether BanChanging and BanChanged events should be raised. </param>
        public void Ban( [NotNull] Player player, [NotNull] string reason, bool announce, bool raiseEvents ) {
            BanPlayerInfoInternal( player, reason, false, announce, raiseEvents );
        }


        /// <summary> Unbans a player. Throws PlayerOpException on problems. </summary>
        /// <param name="player"> Player who is unbanning. </param>
        /// <param name="reason"> Reason for unban. May be empty, if permitted by server configuration. </param>
        /// <param name="announce"> Whether unban should be publicly announced on the server. </param>
        /// <param name="raiseEvents"> Whether BanChanging and BanChanged events should be raised. </param>
        public void Unban( [NotNull] Player player, [NotNull] string reason, bool announce, bool raiseEvents ) {
            BanPlayerInfoInternal( player, reason, true, announce, raiseEvents );
        }


        void BanPlayerInfoInternal( [NotNull] Player player, [NotNull] string reason,
                                    bool unban, bool announce, bool raiseEvents ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( reason == null ) throw new ArgumentNullException( "reason" );
            lock( actionLock ) {
                // Check if player can ban/unban in general
                if( !player.Can( Permission.Ban ) ) {
                    PlayerOpException.ThrowPermissionMissing( player, this, unban ? "unban" : "ban", Permission.Ban );
                }

                // Check if player is trying to ban/unban self
                if( player.Info == this ) {
                    PlayerOpException.ThrowCannotTargetSelf( player, this, unban ? "unban" : "ban" );
                }

                // See if target is already banned/unbanned
                if( unban && BanStatus != BanStatus.Banned ) {
                    PlayerOpException.ThrowPlayerNotBanned( player, this, "banned" );
                } else if( !unban && BanStatus == BanStatus.Banned ) {
                    PlayerOpException.ThrowPlayerAlreadyBanned( player, this, "banned" );
                }

                // Check if player has sufficient rank permissions
                if( !unban && !player.Can( Permission.Ban, Rank ) ) {
                    PlayerOpException.ThrowPermissionLimit( player, this, "ban", Permission.Ban );
                }

                PlayerOpException.CheckBanReason( reason, player, this, unban );

                // Raise PlayerInfo.BanChanging event
                PlayerInfoBanChangingEventArgs e = new PlayerInfoBanChangingEventArgs( this, player, unban, reason );
                if( raiseEvents ) {
                    RaiseBanChangingEvent( e );
                    if( e.Cancel ) return;
                    reason = e.Reason;
                }

                // Actually ban
                bool result;
                if( unban ) {
                    result = ProcessUnban( player.Name, reason );
                } else {
                    result = ProcessBan( player, player.Name, reason );
                }

                // Check what happened
                if( result ) {
                    if( raiseEvents ) {
                        RaiseBanChangedEvent( e );
                    }
                    Player target = PlayerObject;
                    string verb = (unban ? "unbanned" : "banned");
                    if( target != null ) {
                        // Log and announce ban/unban
                        Logger.Log( "{0} was {1} by {2}. Reason: {3}", LogType.UserActivity,
                                    target.Info.Name, verb, player.Name, reason );
                        if( announce ) {
                            Server.Message( target, "{0}&W was {1} by {2}",
                                            target.ClassyName, verb, player.ClassyName );
                        }

                        // Kick the target
                        if( !unban ) {
                            string kickReason;
                            if( reason.Length > 0 ) {
                                kickReason = String.Format( "Banned by {0}: {1}", player.Name, reason );
                            } else {
                                kickReason = String.Format( "Banned by {0}", player.Name );
                            }
                            target.Kick( kickReason, LeaveReason.Ban ); // TODO: check side effects of not using DoKick
                        }
                    } else {
                        Logger.Log( "{0} (offline) was {1} by {2}. Reason: {3}", LogType.UserActivity,
                                    Name, verb, player.Name, reason );
                        Server.Message( "{0}&W (offline) was {1} by {2}",
                                        ClassyName, verb, player.ClassyName );
                    }

                    // Announce ban/unban reason
                    if( announce && ConfigKey.AnnounceKickAndBanReasons.Enabled() && reason.Length > 0 ) {
                        if( unban ) {
                            Server.Message( "&WUnban reason: {0}", reason );
                        } else {
                            Server.Message( "&WBan reason: {0}", reason );
                        }
                    }

                } else {
                    // Player is already banned/unbanned
                    if( unban ) {
                        PlayerOpException.ThrowPlayerNotBanned( player, this, "banned" );
                    } else {
                        PlayerOpException.ThrowPlayerAlreadyBanned( player, this, "banned" );
                    }
                }
            }
        }


        /// <summary> Bans given player and their IP address.
        /// All players from IP are kicked. Throws PlayerOpException on problems. </summary>
        /// <param name="player"> Player who is banning. </param>
        /// <param name="reason"> Reason for ban. May be empty, if permitted by server configuration. </param>
        /// <param name="announce"> Whether ban should be publicly announced on the server. </param>
        /// <param name="raiseEvents"> Whether AddingIPBan, AddedIPBan, BanChanging, and BanChanged events should be raised. </param>
        public void BanIP( [NotNull] Player player, [NotNull] string reason, bool announce, bool raiseEvents ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( reason == null ) throw new ArgumentNullException( "reason" );
            lock( actionLock ) {
                if( !player.Can( Permission.Ban, Permission.BanIP ) ) {
                    PlayerOpException.ThrowPermissionMissing( player, this, "IP-ban", Permission.Ban, Permission.BanIP );
                }

                IPAddress address = LastIP;

                // Check if player is trying to ban self
                if( player.Info == this || address.Equals( player.IP ) && !player.IsSuper ) {
                    PlayerOpException.ThrowCannotTargetSelf( player, this, "IP-ban" );
                }

                // Check if any high-ranked players use this address
                PlayerInfo infoWhomPlayerCantBan = PlayerDB.FindPlayers( address )
                                                            .FirstOrDefault( info => !player.Can( Permission.Ban, info.Rank ) );
                if( infoWhomPlayerCantBan != null ) {
                    PlayerOpException.ThrowPermissionLimitIP( player, infoWhomPlayerCantBan, address );
                }

                // Check existing ban statuses
                bool needNameBan = !IsBanned;
                bool needIPBan = !IPBanList.Contains( address );
                if( !needIPBan && !needNameBan ) {
                    string msg, colorMsg;
                    if( player.Can( Permission.ViewPlayerIPs ) ) {
                        msg = String.Format( "Given player ({0}) and their IP address ({1}) are both already banned.",
                                             Name, address );
                        colorMsg = String.Format( "&SGiven player ({0}&S) and their IP address ({1}) are both already banned.",
                                                  ClassyName, address );
                    } else {
                        msg = String.Format( "Given player ({0}) and their IP address are both already banned.",
                                             Name );
                        colorMsg = String.Format( "&SGiven player ({0}&S) and their IP address are both already banned.",
                                                  ClassyName );
                    }
                    throw new PlayerOpException( player, this, PlayerOpExceptionCode.NoActionNeeded, msg, colorMsg );
                }

                // Check if target is IPBan-exempt
                bool targetIsExempt = (BanStatus == BanStatus.IPBanExempt);
                if( !needIPBan && targetIsExempt ) {
                    string msg = String.Format( "Given player ({0}) is exempt from IP bans. Remove the exemption and retry.",
                                                Name );
                    string colorMsg = String.Format( "&SGiven player ({0}&S) is exempt from IP bans. Remove the exemption and retry.",
                                                     ClassyName );
                    throw new PlayerOpException( player, this, PlayerOpExceptionCode.TargetIsExempt, msg, colorMsg );
                }

                // Ban the name
                if( needNameBan ) {
                    Ban( player, reason, announce, raiseEvents );
                }

                PlayerOpException.CheckBanReason( reason, player, this, false );

                // Ban the IP
                if( needIPBan ) {
                    IPBanInfo banInfo = new IPBanInfo( address, Name, player.Name, reason );
                    if( IPBanList.Add( banInfo, raiseEvents ) ) {
                        Logger.Log( "{0} banned {1} (of player {2}). Reason: {3}", LogType.UserActivity,
                                    player.Name, address, Name, reason );

                        // Announce ban on the server
                        if( announce ) {
                            var can = Server.Players.Can( Permission.ViewPlayerIPs );
                            can.Message( "&WPlayer {0}&W was IP-banned ({1}) by {2}",
                                         ClassyName, address, player.ClassyName );
                            var cant = Server.Players.Cant( Permission.ViewPlayerIPs );
                            cant.Message( "&WPlayer {0}&W was IP-banned by {1}",
                                          ClassyName, player.ClassyName );
                            if( ConfigKey.AnnounceKickAndBanReasons.Enabled() && reason.Length > 0 ) {
                                Server.Message( "&WBanIP reason: {0}", reason );
                            }
                        }
                    } else {
                        // IP is already banned
                        string msg, colorMsg;
                        if( player.Can( Permission.ViewPlayerIPs ) ) {
                            msg = String.Format( "IP of player {0} ({1}) is already banned.",
                                                 Name, address );
                            colorMsg = String.Format( "&SIP of player {0}&S ({1}) is already banned.",
                                                      Name, address );
                        } else {
                            msg = String.Format( "IP of player {0} is already banned.",
                                                 Name );
                            colorMsg = String.Format( "&SIP of player {0}&S is already banned.",
                                                      ClassyName );
                        }
                        throw new PlayerOpException( player, null, PlayerOpExceptionCode.NoActionNeeded, msg, colorMsg );
                    }
                }
            }
        }


        /// <summary> Unbans given player and their IP address. Throws PlayerOpException on problems. </summary>
        /// <param name="player"> Player who is unbanning. </param>
        /// <param name="reason"> Reason for unban. May be empty, if permitted by server configuration. </param>
        /// <param name="announce"> Whether unban should be publicly announced on the server. </param>
        /// <param name="raiseEvents"> Whether RemovingIPBan, RemovedIPBan, BanChanging, and BanChanged events should be raised. </param>
        public void UnbanIP( [NotNull] Player player, [NotNull] string reason, bool announce, bool raiseEvents ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( reason == null ) throw new ArgumentNullException( "reason" );
            lock( actionLock ) {
                if( !player.Can( Permission.Ban, Permission.BanIP ) ) {
                    PlayerOpException.ThrowPermissionMissing( player, this, "IP-unban", Permission.Ban, Permission.BanIP );
                }

                IPAddress address = LastIP;

                // Check if player is trying to unban self
                if( player.Info == this || address.Equals( player.IP ) && !player.IsSuper ) {
                    PlayerOpException.ThrowCannotTargetSelf( player, this, "IP-unban" );
                }

                // Check existing unban statuses
                bool needNameUnban = IsBanned;
                bool needIPUnban = (IPBanList.Get( address ) != null);
                if( !needIPUnban && !needNameUnban ) {
                    PlayerOpException.ThrowPlayerAndIPNotBanned( player, this, address );
                }

                PlayerOpException.CheckBanReason( reason, player, this, true );

                // Unban the name
                if( needNameUnban ) {
                    Unban( player, reason, announce, raiseEvents );
                }

                // Unban the IP
                if( needIPUnban ) {
                    if( IPBanList.Remove( address, raiseEvents ) ) {
                        Logger.Log( "{0} unbanned {1} (of player {2}). Reason: {3}", LogType.UserActivity,
                                    player.Name, address, Name, reason );

                        // Announce unban on the server
                        if( announce ) {
                            var can = Server.Players.Can( Permission.ViewPlayerIPs );
                            can.Message( "&WPlayer {0}&W was IP-unbanned ({1}) by {2}",
                                         ClassyName, address, player.ClassyName );
                            var cant = Server.Players.Cant( Permission.ViewPlayerIPs );
                            cant.Message( "&WPlayer {0}&W was IP-unbanned by {1}",
                                          ClassyName, player.ClassyName );
                            if( ConfigKey.AnnounceKickAndBanReasons.Enabled() && reason.Length > 0 ) {
                                Server.Message( "&WUnbanIP reason: {0}", reason );
                            }
                        }
                    } else {
                        PlayerOpException.ThrowPlayerAndIPNotBanned( player, this, address );
                    }
                }
            }
        }


        /// <summary> Bans given player, their IP, and all other accounts on IP.
        /// All players from IP are kicked. Throws PlayerOpException on problems. </summary>
        /// <param name="player"> Player who is banning. </param>
        /// <param name="reason"> Reason for ban. May be empty, if permitted by server configuration. </param>
        /// <param name="announce"> Whether ban should be publicly announced on the server. </param>
        /// <param name="raiseEvents"> Whether AddingIPBan, AddedIPBan, BanChanging, and BanChanged events should be raised. </param>
        public void BanAll( [NotNull] Player player, [NotNull] string reason, bool announce, bool raiseEvents ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( reason == null ) throw new ArgumentNullException( "reason" );
            lock( actionLock ) {
                if( !player.Can( Permission.Ban, Permission.BanIP, Permission.BanAll ) ) {
                    PlayerOpException.ThrowPermissionMissing( player, this, "ban-all",
                                                         Permission.Ban, Permission.BanIP, Permission.BanAll );
                }

                IPAddress address = LastIP;

                // Check if player is trying to ban self
                if( player.Info == this || address.Equals( player.IP ) && !player.IsSuper ) {
                    PlayerOpException.ThrowCannotTargetSelf( player, this, "ban-all" );
                }

                // Check if any high-ranked players use this address
                PlayerInfo[] allPlayersOnIP = PlayerDB.FindPlayers( address );
                PlayerInfo infoWhomPlayerCantBan = allPlayersOnIP.FirstOrDefault( info => !player.Can( Permission.Ban, info.Rank ) );
                if( infoWhomPlayerCantBan != null ) {
                    PlayerOpException.ThrowPermissionLimitIP( player, infoWhomPlayerCantBan, address );
                }

                PlayerOpException.CheckBanReason( reason, player, this, false );
                bool somethingGotBanned = false;

                // Ban the IP
                if( !IPBanList.Contains( address ) ) {
                    IPBanInfo banInfo = new IPBanInfo( address, Name, player.Name, reason );
                    if( IPBanList.Add( banInfo, raiseEvents ) ) {
                        Logger.Log( "{0} banned {1} (BanAll by association with {2}). Reason: {3}", LogType.UserActivity,
                                    player.Name, address, Name, reason );

                        // Announce ban on the server
                        if( announce ) {
                            var can = Server.Players.Can( Permission.ViewPlayerIPs );
                            can.Message( "&WPlayer {0}&W was IP-banned ({1}) by {2}",
                                         ClassyName, address, player.ClassyName );
                            var cant = Server.Players.Cant( Permission.ViewPlayerIPs );
                            cant.Message( "&WPlayer {0}&W was IP-banned by {1}",
                                          ClassyName, player.ClassyName );
                        }
                        somethingGotBanned = true;
                    }
                }

                // Ban individual players
                foreach( PlayerInfo targetAlt in allPlayersOnIP ) {
                    if( targetAlt.BanStatus != BanStatus.NotBanned ) continue;

                    // Raise PlayerInfo.BanChanging event
                    PlayerInfoBanChangingEventArgs e = new PlayerInfoBanChangingEventArgs( targetAlt, player, false, reason );
                    if( raiseEvents ) {
                        RaiseBanChangingEvent( e );
                        if( e.Cancel ) continue;
                        reason = e.Reason;
                    }

                    // Do the ban
                    if( targetAlt.ProcessBan( player, player.Name, reason ) ) {
                        if( raiseEvents ) {
                            RaiseBanChangedEvent( e );
                        }

                        // Log and announce ban
                        if( targetAlt == this ) {
                            Logger.Log( "{0} was banned by {1} (BanAll). Reason: {2}", LogType.UserActivity,
                                        targetAlt.Name, player.Name, reason );
                            if( announce ) {
                                Server.Message( "&WPlayer {0}&W was banned by {1}&W (BanAll)",
                                                targetAlt.ClassyName, player.ClassyName );
                            }
                        } else {
                            Logger.Log( "{0} was banned by {1} (BanAll by association with {2}). Reason: {3}", LogType.UserActivity,
                                        targetAlt.Name, player.Name, Name, reason );
                            if( announce ) {
                                Server.Message( "&WPlayer {0}&W was banned by {1}&W by association with {2}",
                                                targetAlt.ClassyName, player.ClassyName, ClassyName );
                            }
                        }
                        somethingGotBanned = true;
                    }
                }

                // If no one ended up getting banned, quit here
                if( !somethingGotBanned ) {
                    PlayerOpException.ThrowNoOneToBan( player, this, address );
                }

                // Announce banall reason towards the end of all bans
                if( announce && ConfigKey.AnnounceKickAndBanReasons.Enabled() && reason.Length > 0 ) {
                    Server.Message( "&WBanAll reason: {0}", reason );
                }

                // Kick all players from IP
                Player[] targetsOnline = Server.Players.FromIP( address ).ToArray();
                if( targetsOnline.Length > 0 ) {
                    string kickReason;
                    if( reason.Length > 0 ) {
                        kickReason = String.Format( "Banned by {0}: {1}", player.Name, reason );
                    } else {
                        kickReason = String.Format( "Banned by {0}", player.Name );
                    }
                    for( int i = 0; i < targetsOnline.Length; i++ ) {
                        targetsOnline[i].Kick( kickReason, LeaveReason.BanAll );
                    }
                }
            }
        }


        /// <summary> Unbans given player, their IP address, and all other accounts on IP. Throws PlayerOpException on problems. </summary>
        /// <param name="player"> Player who is unbanning. </param>
        /// <param name="reason"> Reason for unban. May be empty, if permitted by server configuration. </param>
        /// <param name="announce"> Whether unban should be publicly announced on the server. </param>
        /// <param name="raiseEvents"> Whether RemovingIPBan, RemovedIPBan, BanChanging, and BanChanged events should be raised. </param>
        public void UnbanAll( [NotNull] Player player, [NotNull] string reason, bool announce, bool raiseEvents ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( reason == null ) throw new ArgumentNullException( "reason" );
            lock( actionLock ) {
                if( !player.Can( Permission.Ban, Permission.BanIP, Permission.BanAll ) ) {
                    PlayerOpException.ThrowPermissionMissing( player, this, "unban-all",
                                                         Permission.Ban, Permission.BanIP, Permission.BanAll );
                }

                IPAddress address = LastIP;

                // Check if player is trying to unban self
                if( player.Info == this || address.Equals( player.IP ) && !player.IsSuper ) {
                    PlayerOpException.ThrowCannotTargetSelf( player, this, "unban-all" );
                }

                PlayerOpException.CheckBanReason( reason, player, this, true );
                bool somethingGotUnbanned = false;

                // Unban the IP
                if( IPBanList.Contains( address ) ) {
                    if( IPBanList.Remove( address, raiseEvents ) ) {
                        Logger.Log( "{0} unbanned {1} (UnbanAll by association with {2}). Reason: {3}", LogType.UserActivity,
                                    player.Name, address, Name, reason );

                        // Announce unban on the server
                        if( announce ) {
                            var can = Server.Players.Can( Permission.ViewPlayerIPs );
                            can.Message( "&WPlayer {0}&W was IP-unbanned ({1}) by {2}",
                                         ClassyName, address, player.ClassyName );
                            var cant = Server.Players.Cant( Permission.ViewPlayerIPs );
                            cant.Message( "&WPlayer {0}&W was IP-unbanned by {1}",
                                          ClassyName, player.ClassyName );
                        }

                        somethingGotUnbanned = true;
                    }
                }

                // Unban individual players
                PlayerInfo[] allPlayersOnIP = PlayerDB.FindPlayers( address );
                foreach( PlayerInfo targetAlt in allPlayersOnIP ) {
                    if( targetAlt.BanStatus != BanStatus.Banned ) continue;

                    // Raise PlayerInfo.BanChanging event
                    PlayerInfoBanChangingEventArgs e = new PlayerInfoBanChangingEventArgs( targetAlt, player, true, reason );
                    if( raiseEvents ) {
                        RaiseBanChangingEvent( e );
                        if( e.Cancel ) continue;
                        reason = e.Reason;
                    }

                    // Do the ban
                    if( targetAlt.ProcessUnban( player.Name, reason ) ) {
                        if( raiseEvents ) {
                            RaiseBanChangedEvent( e );
                        }

                        // Log and announce ban
                        if( targetAlt == this ) {
                            Logger.Log( "{0} was unbanned by {1} (UnbanAll). Reason: {2}", LogType.UserActivity,
                                        targetAlt.Name, player.Name, reason );
                            if( announce ) {
                                Server.Message( "&WPlayer {0}&W was unbanned by {1}&W (UnbanAll)",
                                                targetAlt.ClassyName, player.ClassyName );
                            }
                        } else {
                            Logger.Log( "{0} was unbanned by {1} (UnbanAll by association with {2}). Reason: {3}", LogType.UserActivity,
                                        targetAlt.Name, player.Name, Name, reason );
                            if( announce ) {
                                Server.Message( "&WPlayer {0}&W was unbanned by {1}&W by association with {2}",
                                                targetAlt.ClassyName, player.ClassyName, ClassyName );
                            }
                        }
                        somethingGotUnbanned = true;
                    }
                }

                // If no one ended up getting unbanned, quit here
                if( !somethingGotUnbanned ) {
                    PlayerOpException.ThrowNoOneToUnban( player, this, address );
                }

                // Announce unbanall reason towards the end of all unbans
                if( announce && ConfigKey.AnnounceKickAndBanReasons.Enabled() && reason.Length > 0 ) {
                    Server.Message( "&WUnbanAll reason: {0}", reason );
                }
            }
        }



        internal bool ProcessBan( [CanBeNull] Player bannedBy, [NotNull] string bannedByName, [NotNull] string banReason ) {
            if( bannedByName == null ) throw new ArgumentNullException( "bannedByName" );
            if( banReason == null ) throw new ArgumentNullException( "banReason" );
            lock( actionLock ) {
                if( !IsBanned ) {
                    BanStatus = BanStatus.Banned;
                    BannedBy = bannedByName;
                    BanDate = DateTime.UtcNow;
                    BanReason = banReason;
                    if( bannedBy != null ) {
                        Interlocked.Increment( ref bannedBy.Info.TimesBannedOthers );
                    }
                    Unmute();
                    Unfreeze();
                    IsHidden = false;
                    LastModified = DateTime.UtcNow;
                    return true;
                } else {
                    return false;
                }
            }
        }


        internal bool ProcessUnban( [NotNull] string unbannedByName, [NotNull] string unbanReason ) {
            if( unbannedByName == null ) throw new ArgumentNullException( "unbannedByName" );
            if( unbanReason == null ) throw new ArgumentNullException( "unbanReason" );
            lock( actionLock ) {
                if( IsBanned ) {
                    BanStatus = BanStatus.NotBanned;
                    UnbannedBy = unbannedByName;
                    UnbanDate = DateTime.UtcNow;
                    UnbanReason = unbanReason;
                    LastModified = DateTime.UtcNow;
                    return true;
                } else {
                    return false;
                }
            }
        }

        #endregion

        /// <summary> Changes rank of the player (promotes or demotes). Throws PlayerOpException on problems. </summary>
        /// <param name="player"> Player who originated the promotion/demotion action. </param>
        /// <param name="newRank"> New rank. </param>
        /// <param name="reason"> Reason for promotion/demotion. </param>
        /// <param name="announce"> Whether rank change should be publicly announced or not. </param>
        /// <param name="raiseEvents"> Whether PlayerInfo.RankChanging and PlayerInfo.RankChanged events should be raised. </param>
        /// <param name="auto"> Whether rank change should be marked as "automatic" or manual. </param>
        public void ChangeRank( [NotNull] Player player, [NotNull] Rank newRank, [NotNull] string reason,
                                bool announce, bool raiseEvents, bool auto ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( newRank == null ) throw new ArgumentNullException( "newRank" );
            if( reason == null ) throw new ArgumentNullException( "reason" );

            bool promoting = (newRank > Rank);
            string verb = (promoting ? "promote" : "demote");
            string verbed = (promoting ? "promoted" : "demoted");

            // Check if player is trying to promote/demote self
            if( player.Info == this ) {
                PlayerOpException.ThrowCannotTargetSelf( player, this, verb );
            }

            // Check if target already has the desired rank
            if( newRank == Rank ) {
                string msg = String.Format( "Player {0} is already ranked {1}", Name, Rank.Name );
                string colorMsg = String.Format( "&SPlayer {0}&S is already ranked {1}", ClassyName, Rank.ClassyName );
                throw new PlayerOpException( player, this, PlayerOpExceptionCode.NoActionNeeded, msg, colorMsg );
            }

            // Check if player has permissions in general
            if( promoting && !player.Can( Permission.Promote ) ) {
                PlayerOpException.ThrowPermissionMissing( player, this, verb, Permission.Promote );
            } else if( !promoting && !player.Can( Permission.Demote ) ) {
                PlayerOpException.ThrowPermissionMissing( player, this, verb, Permission.Demote );
            }

            // Check if player's specific permission limits are enough
            if( promoting && !player.Can( Permission.Promote, newRank )) {
                string msg = String.Format( "Cannot promote {0} to {1}: you may only promote players up to rank {2}.",
                                            Name, newRank.Name,
                                            player.Info.Rank.GetLimit( Permission.Promote ).Name );
                string colorMsg = String.Format( "&SCannot promote {0}&S to {1}&S: you may only promote players up to rank {2}&S.",
                                                 ClassyName, newRank.ClassyName,
                                                 player.Info.Rank.GetLimit( Permission.Promote ).ClassyName );
                throw new PlayerOpException( player, this, PlayerOpExceptionCode.PermissionLimitTooLow,
                                             msg, colorMsg );
            } else if( !promoting && !player.Can( Permission.Demote, Rank ) ) {
                string msg = String.Format( "Cannot demote {0} (ranked {1}): you may only demote players ranked {2} or below.",
                                            Name, Rank.Name,
                                            player.Info.Rank.GetLimit( Permission.Demote ).Name );
                string colorMsg = String.Format( "&SCannot demote {0}&S (ranked {1}&S): you may only demote players ranked {2}&S or below.",
                                                 ClassyName, Rank.ClassyName,
                                                 player.Info.Rank.GetLimit( Permission.Demote ).ClassyName );
                throw new PlayerOpException( player, this, PlayerOpExceptionCode.PermissionLimitTooLow,
                                             msg, colorMsg );
            }

            // Check if promotion/demotion reason is required/missing
            PlayerOpException.CheckRankChangeReason( reason, player, this, promoting );

            RankChangeType changeType;
            if( newRank >= Rank ) {
                changeType = (auto ? RankChangeType.AutoPromoted : RankChangeType.Promoted);
            } else {
                changeType = (auto ? RankChangeType.AutoDemoted : RankChangeType.Demoted);
            }

            // Raise PlayerInfo.RankChanging event
            if( raiseEvents && RaiseRankChangingEvent( this, player, newRank, reason, changeType ) ) {
                PlayerOpException.ThrowCancelled( player, this );
            }

            // Log the rank change
            Logger.Log( "{0} {1} {2} from {3} to {4}. Reason: {5}", LogType.UserActivity,
                        player.Name, verbed, Name, Rank.Name, newRank.Name, reason );

            // Actually change rank
            ProcessRankChange( newRank, player.Name, reason, changeType );

            // Make necessary adjustments related to rank change
            Rank oldRank = Rank;
            Player target = PlayerObject;
            if( target == null ) {
                if( raiseEvents ) RaiseRankChangedEvent( this, player, oldRank, reason, changeType );
                if( IsHidden && !Rank.Can( Permission.Hide ) ) {
                    IsHidden = false;
                }
            } else {
                Server.RaisePlayerListChangedEvent();
                if( raiseEvents ) RaiseRankChangedEvent( this, player, oldRank, reason, changeType );

                // reset binds (water, lava, admincrete)
                target.ResetAllBinds();

                // reset admincrete deletion permission
                target.Send( PacketWriter.MakeSetPermission( target ) );

                // cancel selection in progress
                if( target.IsMakingSelection ) {
                    target.Message( "Selection cancelled." );
                    target.SelectionCancel();
                }

                // reset brush to normal, if not allowed to draw advanced
                if( !target.Can( Permission.DrawAdvanced ) ) {
                    target.Brush = NormalBrushFactory.Instance;
                }

                // unhide, if needed
                if( IsHidden && !target.Can( Permission.Hide ) ) {
                    IsHidden = false;
                    player.Message( "You are no longer hidden." );
                }

                // ensure copy slot consistency
                target.InitCopySlots();

                // inform the player of the rank change
                target.Message( "You were {0} to {1}&S by {2}",
                                verbed,
                                newRank.ClassyName,
                                player.ClassyName );
            }

            // Announce the rank change
            if( announce ) {
                if( ConfigKey.AnnounceRankChanges.Enabled() ) {
                    Server.Message( target,
                                    "{0}&S {1} {2}&S from {3}&S to {4}",
                                    player.ClassyName,
                                    verbed,
                                    ClassyName,
                                    oldRank.ClassyName,
                                    newRank.ClassyName );
                    if( ConfigKey.AnnounceRankChangeReasons.Enabled() && reason.Length > 0 ) {
                        Server.Message( "&S{0} reason: {1}",
                                        promoting ? "Promotion" : "Demotion",
                                        reason );
                    }
                } else {
                    player.Message( "You {0} {1}&S from {2}&S to {3}",
                                    verbed,
                                    ClassyName,
                                    oldRank.ClassyName,
                                    newRank.ClassyName );
                    if( target != null && reason.Length > 0 ) {
                        target.Message( "&S{0} reason: {1}",
                                        promoting ? "Promotion" : "Demotion",
                                        reason );
                    }
                }
            }
        }
    }
}