﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace fCraft {
    public sealed class Rank : IClassy, IComparable<Rank> {

        /// <summary> Rank color code. Should not be left blank. </summary>
        [NotNull]
        public string Color { get; set; }

        /// <summary> String that prefixes the username of all members of this rank </summary>
        [NotNull]
        public string Prefix { get; set; }

        /// <summary> Rank's displayed name.
        /// Use rank.FullName instead for serializing (to improve backwards compatibility). </summary>
        [NotNull]
        public string Name { get; internal set; }

        /// <summary> Unique rank ID. Generated by Rank.GenerateID. Assigned once at creation.
        /// Used to preserve compatibility in case a rank gets renamed. </summary>
        [NotNull]
        public string ID { get; private set; }

        /// <summary> Set of permissions given to this rank. Use Rank.Can() to access. </summary>
        [NotNull]
        public bool[] Permissions { get; private set; }

        /// <summary> Whether players of this rank are allowed to remove restrictions that affect themselves.
        /// Affects /WMain, /WAccess, /WBuild, /ZAdd, /ZEdit, and /ZRemove. </summary>
        public bool AllowSecurityCircumvention;

        /// <summary>
        /// Maximum number of buffered copies this rank is allowed to have.
        /// </summary>
        public int CopySlots = 2;

        /// <summary> Maximum number of blocks away the origin that a fill is allowed to travel. </summary>
        /// <example> A limit of 32, means that the maximum fill dimensions are (32 * 2 + 1),
        /// which is 65 x 65 x 65. </example>
        public int FillLimit = 32;

        public int DrawLimit;

        /// <summary> Time until the idle kicker will kick this Rank from the server. </summary>
        public int IdleKickTimer;

        /// <summary> Number of blocks that need to be modified in AntiGriefSeconds for the AntiGrief to kick in for this Rank. </summary>
        public int AntiGriefBlocks;

        /// <summary> The interval in seconds for which to count number of blocks broken for use in AntiGrief for this Rank. </summary>
        public int AntiGriefSeconds;

        /// <summary> Whether this rank has a reserved slot (is allowed to join the server even if it's full). </summary>
        public bool HasReservedSlot { get; set; }

        /// <summary> Rank's relative index on the hierarchy. Index of the top rank is always 0.
        /// Subordinate ranks start at 1. Higher index = lower rank. </summary>
        public int Index;

        /// <summary> The Rank immediately above this Rank. </summary>
        [CanBeNull]
        public Rank NextRankUp { get; internal set; }

        /// <summary> The Rank immediately below this Rank. </summary>
        [CanBeNull]
        public Rank NextRankDown { get; internal set; }

        /// <summary> The main world for this Rank. </summary>
        [CanBeNull]
        public World MainWorld { get; set; }


        #region Constructors

        private Rank() {
            Permissions = new bool[Enum.GetValues( typeof( Permission ) ).Length];
            PermissionLimits = new Rank[Permissions.Length];
            PermissionLimitStrings = new string[Permissions.Length];
            Color = fCraft.Color.White;
            Prefix = "";
        }

        /// <summary> Sets the name and ID of this Rank. </summary>
        /// <param name="name"> Name to assign to this Rank. </param>
        /// <param name="id"> ID to assing to this Rank. </param>
        public Rank( [NotNull] string name, [NotNull] string id )
            : this() {
            if( name == null ) throw new ArgumentNullException( "name" );
            if( id == null ) throw new ArgumentNullException( "id" );
            Name = name;
            ID = id;
            FullName = Name + "#" + ID;
        }

        /// <summary> Sets the name and ID of this Rank from a XML serialised object. </summary>
        /// <param name="el"> Serialised Rank. </param>
        public Rank( [NotNull] XElement el )
            : this() {
            if( el == null ) throw new ArgumentNullException( "el" );

            // Name
            XAttribute attr = el.Attribute( "name" );
            if( attr == null ) {
                throw new RankDefinitionException( null, "Rank definition with no name was ignored." );

            } else if( !IsValidRankName( attr.Value.Trim() ) ) {
                throw new RankDefinitionException( Name, 
                                                   "Invalid name specified for rank \"{0}\". " +
                                                   "Rank names can only contain letters, digits, and underscores. " +
                                                   "Rank definition was ignored.", Name );

            } else {
                // duplicate Name check is done in RankManager.AddRank()
                Name = attr.Value.Trim();
            }


            // ID
            attr = el.Attribute( "id" );
            if( attr == null ) {
                ID = RankManager.GenerateID();
                Logger.Log( LogType.Warning,
                            "Rank({0}): No ID specified; issued a new unique ID: {1}",
                            Name, ID );

            } else if( !IsValidID( attr.Value.Trim() ) ) {
                ID = RankManager.GenerateID();
                Logger.Log( LogType.Warning,
                            "Rank({0}): Invalid ID specified (must be alphanumeric, and exactly 16 characters long); issued a new unique ID: {1}",
                            Name, ID );

            } else {
                ID = attr.Value.Trim();
                // duplicate ID check is done in RankManager.AddRank()
            }

            FullName = Name + "#" + ID;


            // Color (optional)
            if( ( attr = el.Attribute( "color" ) ) != null ) {
                string color = fCraft.Color.Parse( attr.Value );
                if( color == null ) {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse rank color. Assuming default (none).", Name );
                    Color = fCraft.Color.White;
                } else {
                    Color = color;
                }
            } else {
                Color = fCraft.Color.White;
            }


            // Prefix (optional)
            if( ( attr = el.Attribute( "prefix" ) ) != null ) {
                if( IsValidPrefix( attr.Value ) ) {
                    Prefix = attr.Value;
                } else {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Invalid prefix format. Expecting 1 character.",Name );
                }
            }


            // AntiGrief block limit (assuming unlimited if not given)
            int value;
            XAttribute agBlocks = el.Attribute( "antiGriefBlocks" );
            XAttribute agSeconds = el.Attribute( "antiGriefSeconds" );
            if( agBlocks != null && agSeconds != null ) {
                if( Int32.TryParse( agBlocks.Value, out value ) ) {
                    if( value >= 0 && value < 1000 ) {
                        AntiGriefBlocks = value;

                    } else {
                        Logger.Log( LogType.Warning,
                                    "Rank({0}): Value for antiGriefBlocks is not within valid range (0-1000). Assuming default ({1}).",
                                    Name, AntiGriefBlocks );
                    }
                } else {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse the value for antiGriefBlocks. Assuming default ({1}).",
                                Name, AntiGriefBlocks );
                }

                if( Int32.TryParse( agSeconds.Value, out value ) ) {
                    if( value >= 0 && value < 100 ) {
                        AntiGriefSeconds = value;
                    } else {
                        Logger.Log( LogType.Warning,
                                    "Rank({0}): Value for antiGriefSeconds is not within valid range (0-100). Assuming default ({1}).",
                                    Name, AntiGriefSeconds );
                    }
                } else {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse the value for antiGriefSeconds. Assuming default ({1}).",
                                Name, AntiGriefSeconds );
                }
            }


            // Draw command limit, in number-of-blocks (assuming unlimited if not given)
            if( ( attr = el.Attribute( "drawLimit" ) ) != null ) {
                if( Int32.TryParse( attr.Value, out value ) ) {
                    if( value >= 0 && value < 100000000 ) {
                        DrawLimit = value;
                    } else {
                        Logger.Log( LogType.Warning,
                                    "Rank({0}): Value for drawLimit is not within valid range (0-100000000). Assuming default ({1}).",
                                    Name, DrawLimit );
                    }
                } else {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse the value for drawLimit. Assuming default ({1}).",
                                Name, DrawLimit );
                }
            }


            // Idle kick timer, in minutes. (assuming 'never' if not given)
            if( ( attr = el.Attribute( "idleKickAfter" ) ) != null ) {
                if( !Int32.TryParse( attr.Value, out IdleKickTimer ) ) {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse the value for idleKickAfter. Assuming 0 (never).",
                                Name );
                    IdleKickTimer = 0;
                }
            } else {
                IdleKickTimer = 0;
            }


            // Reserved slot. (assuming 'no' if not given)
            if( ( attr = el.Attribute( "reserveSlot" ) ) != null ) {
                bool reservedSlot;
                if( Boolean.TryParse( attr.Value, out reservedSlot ) ) {
                    HasReservedSlot = reservedSlot;
                } else {
                    Logger.Log( LogType.Warning,
                                    "Rank({0}): Could not parse value for reserveSlot. Assuming \"false\".", Name );
                    HasReservedSlot = false;
                }
            } else {
                HasReservedSlot = false;
            }


            // Security circumvention. (assuming 'no' if not given)
            if( ( attr = el.Attribute( "allowSecurityCircumvention" ) ) != null ) {
                if( !Boolean.TryParse( attr.Value, out AllowSecurityCircumvention ) ) {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse the value for allowSecurityCircumvention. Assuming \"false\".",
                                Name );
                    AllowSecurityCircumvention = false;
                }
            } else {
                AllowSecurityCircumvention = false;
            }


            // Copy slots (assuming default 2 if not given)
            if( ( attr = el.Attribute( "copySlots" ) ) != null ) {
                if( Int32.TryParse( attr.Value, out value ) ) {
                    if( value > 0 && value < 256 ) {
                        CopySlots = value;
                    } else {
                        Logger.Log( LogType.Warning,
                                    "Rank({0}): Value for copySlots is not within valid range (1-255). Assuming default ({1}).",
                                    Name, CopySlots );
                    }
                } else {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse the value for copySlots. Assuming default ({1}).",
                                Name, CopySlots );
                }
            }

            // Fill limit (assuming default 32 if not given)
            if( ( attr = el.Attribute( "fillLimit" ) ) != null ) {
                if( Int32.TryParse( attr.Value, out value ) ) {
                    if( value < 1 ) {
                        Logger.Log( LogType.Warning,
                                    "Rank({0}): Value for fillLimit may not be negative. Assuming default ({1}).",
                                    Name, FillLimit );
                    } else if( value > 2048 ) {
                        FillLimit = 2048;
                    } else {
                        FillLimit = value;
                    }
                } else {
                    Logger.Log( LogType.Warning,
                                "Rank({0}): Could not parse the value for fillLimit. Assuming default ({1}).",
                                Name, FillLimit );
                }
            }

            // Permissions
            for( int i = 0; i < Enum.GetValues( typeof( Permission ) ).Length; i++ ) {
                string permission = ( (Permission)i ).ToString();
                XElement temp;
                if( ( temp = el.Element( permission ) ) != null ) {
                    Permissions[i] = true;
                    if( ( attr = temp.Attribute( "max" ) ) != null ) {
                        PermissionLimitStrings[i] = attr.Value;
                    }
                }
            }

            // check consistency of ban permissions
            if( !Can( Permission.Ban ) && ( Can( Permission.BanAll ) || Can( Permission.BanIP ) ) ) {
                Logger.Log( LogType.Warning,
                            "Rank({0}): Rank is allowed to BanIP and/or BanAll but not allowed to Ban. " +
                            "Assuming that all ban permissions were meant to be off.", Name );
                Permissions[(int)Permission.BanIP] = false;
                Permissions[(int)Permission.BanAll] = false;
            }

            // check consistency of patrol permissions
            if( !Can( Permission.Teleport ) && Can( Permission.Patrol ) ) {
                Logger.Log( LogType.Warning,
                            "Rank({0}): Rank is allowed to Patrol but not allowed to Teleport. " +
                            "Assuming that Patrol permission was meant to be off.", Name );
                Permissions[(int)Permission.Patrol] = false;
            }

            // check consistency of draw permissions
            if( !Can( Permission.Draw ) && Can( Permission.DrawAdvanced ) ) {
                Logger.Log( LogType.Warning,
                            "Rank({0}): Rank is allowed to DrawAdvanced but not allowed to Draw. " +
                            "Assuming that Draw permission were meant to be off.", Name );
                Permissions[(int)Permission.DrawAdvanced] = false;
            }
        }

        #endregion


        public XElement Serialize() {
            XElement rankTag = new XElement( "Rank" );
            rankTag.Add( new XAttribute( "name", Name ) );
            rankTag.Add( new XAttribute( "id", ID ) );
            string colorName = fCraft.Color.GetName( Color );
            if( colorName != null ) {
                rankTag.Add( new XAttribute( "color", colorName ) );
            }
            if( Prefix.Length > 0 ) rankTag.Add( new XAttribute( "prefix", Prefix ) );
            rankTag.Add( new XAttribute( "antiGriefBlocks", AntiGriefBlocks ) );
            rankTag.Add( new XAttribute( "antiGriefSeconds", AntiGriefSeconds ) );
            if( DrawLimit > 0 ) rankTag.Add( new XAttribute( "drawLimit", DrawLimit ) );
            if( IdleKickTimer > 0 ) rankTag.Add( new XAttribute( "idleKickAfter", IdleKickTimer ) );
            if( HasReservedSlot ) rankTag.Add( new XAttribute( "reserveSlot", HasReservedSlot ) );
            if( AllowSecurityCircumvention ) rankTag.Add( new XAttribute( "allowSecurityCircumvention", AllowSecurityCircumvention ) );
            rankTag.Add( new XAttribute( "copySlots", CopySlots ) );
            rankTag.Add( new XAttribute( "fillLimit", FillLimit ) );

            for( int i = 0; i < Enum.GetValues( typeof( Permission ) ).Length; i++ ) {
                if( Permissions[i] ) {
                    XElement temp = new XElement( ( (Permission)i ).ToString() );

                    if( PermissionLimits[i] != null ) {
                        temp.Add( new XAttribute( "max", GetLimit( (Permission)i ).FullName ) );
                    }
                    rankTag.Add( temp );
                }
            }
            return rankTag;
        }

        /// <remark> Somewhat counterintuitive, but lower index number = higher up on the list = higher rank. </remark>
        #region Rank Comparison Operators

        // Somewhat counterintuitive, but lower index number = higher up on the list = higher rank

        public int CompareTo( [NotNull] Rank other ) {
            if( other == null ) throw new ArgumentNullException( "other" );
            return other.Index - Index;
        }

        public static bool operator >( Rank a, Rank b ) {
            return a.Index < b.Index;
        }

        public static bool operator <( Rank a, Rank b ) {
            return a.Index > b.Index;
        }

        public static bool operator >=( Rank a, Rank b ) {
            return a.Index <= b.Index;
        }

        public static bool operator <=( Rank a, Rank b ) {
            return a.Index >= b.Index;
        }

        #endregion


        #region Permissions

        public bool Can( Permission permission ) {
            return Permissions[(int)permission];
        }

        public bool Can( Permission permission, [NotNull] Rank other ) {
            if( other == null ) throw new ArgumentNullException( "other" );
            return Permissions[(int)permission] && GetLimit( permission ) >= other;
        }


        public bool CanSee( [NotNull] Rank other ) {
            if( other == null ) throw new ArgumentNullException( "other" );
            return this > other.GetLimit( Permission.Hide );
        }

        #endregion


        #region Permission Limits

        public Rank[] PermissionLimits {
            get;
            private set;
        }
        public readonly string[] PermissionLimitStrings;

        public Rank GetLimit( Permission permission ) {
            return PermissionLimits[(int)permission] ?? this;
        }


        public void SetLimit( Permission permission, [CanBeNull] Rank limit ) {
            PermissionLimits[(int)permission] = limit;
        }


        public void ResetLimit( Permission permission ) {
            SetLimit( permission, null );
        }


        public int GetLimitIndex( Permission permission ) {
            if( PermissionLimits[(int)permission] == null ) {
                return 0;
            } else {
                return PermissionLimits[(int)permission].Index + 1;
            }
        }

        #endregion


        #region Validation

        public static bool IsValidRankName( [NotNull] string rankName ) {
            if( rankName == null ) throw new ArgumentNullException( "rankName" );
            if( rankName.Length < 1 || rankName.Length > 16 ) return false;
            for( int i = 0; i < rankName.Length; i++ ) {
                char ch = rankName[i];
                if( ch < '0' || ( ch > '9' && ch < 'A' ) || ( ch > 'Z' && ch < '_' ) || ( ch > '_' && ch < 'a' ) ||
                    ch > 'z' ) {
                    return false;
                }
            }
            return true;
        }


        public static bool IsValidID( [NotNull] string id ) {
            if( id == null ) throw new ArgumentNullException( "id" );
            if( id.Length != 16 ) return false;
            for( int i = 0; i < id.Length; i++ ) {
                char ch = id[i];
                if( ch < '0' || ( ch > '9' && ch < 'A' ) || ( ch > 'Z' && ch < 'a' ) || ch > 'z' ) {
                    return false;
                }
            }
            return true;
        }


        public static bool IsValidPrefix( [NotNull] string prefix ) {
            if( prefix == null ) throw new ArgumentNullException( "prefix" );
            if( prefix.Length == 0 ) return true;
            if( prefix.Length > 1 ) return false;
            return !Chat.ContainsInvalidChars( prefix );
        }

        #endregion


        public override string ToString() {
            return String.Format( "Rank({0})", Name );
        }


        /// <summary> Fully qualified name of the rank. Format: "Name#ID".
        /// Should be used wherever rank name needs to be serialized. </summary>
        public string FullName { get; internal set; }


        /// <summary> Decorated name of the rank, including color and prefix
        /// (if enabled by the configuration). </summary>
        public string ClassyName {
            get {
                string displayedName = Name;
                if( ConfigKey.RankPrefixesInChat.Enabled() ) {
                    displayedName = Prefix + displayedName;
                }
                if( ConfigKey.RankColorsInChat.Enabled() ) {
                    displayedName = Color + displayedName;
                }
                return displayedName;
            }
        }


        internal bool ParsePermissionLimits() {
            bool ok = true;
            for( int i = 0; i < PermissionLimits.Length; i++ ) {
                if( PermissionLimitStrings[i] == null ) continue;
                SetLimit( (Permission)i, Parse( PermissionLimitStrings[i] ) );
                ok &= ( GetLimit( (Permission)i ) != null );
            }
            return ok;
        }

        /// <summary> Shortcut to the list of all online players of this rank. </summary>
        [NotNull]
        public IEnumerable<Player> Players {
            get {
                return Server.Players.Ranked( this );
            }
        }

        /// <summary> Total number of players of this rank (online and offline). </summary>
        public int PlayerCount {
            get {
                return PlayerDB.PlayerInfoList.Count( t => t.Rank == this );
            }
        }


        /// <summary> Parses serialized rank. Accepts either the "name" or "name#ID" format.
        /// Uses legacy rank mapping table for unrecognized ranks. Does not autocomplete.
        /// Name part is case-insensitive. ID part is case-sensitive. </summary>
        /// <param name="name"> Full rank name, or name and ID. </param>
        /// <returns> If name could be parsed, returns the corresponding Rank object. Otherwise returns null. </returns>
        [CanBeNull]
        public static Rank Parse( [CanBeNull] string name ) {
            if( name == null ) return null;

            if( RankManager.RanksByFullName.ContainsKey( name ) ) {
                return RankManager.RanksByFullName[name];
            }

            if( name.Contains( "#" ) ) {
                // new format
                string id = name.Substring( name.IndexOf("#", StringComparison.Ordinal) + 1 );

                if( RankManager.RanksByID.ContainsKey( id ) ) {
                    // current class
                    return RankManager.RanksByID[id];

                } else {
                    // unknown class
                    int tries = 0;
                    while( RankManager.LegacyRankMapping.ContainsKey( id ) ) {
                        id = RankManager.LegacyRankMapping[id];
                        if( RankManager.RanksByID.ContainsKey( id ) ) {
                            return RankManager.RanksByID[id];
                        }
                        // avoid infinite loops due to recursive definitions
                        tries++;
                        if( tries > 100 ) {
                            throw new RankDefinitionException( name, "Recursive legacy rank definition" );
                        }
                    }
                    string plainName = name.Substring( 0, name.IndexOf( '#' ) ).ToLower();
                    // try to fall back to name-only
                    return RankManager.RanksByName.ContainsKey( plainName ) ?
                           RankManager.RanksByName[plainName] : null;
                }

            } else if( RankManager.RanksByName.ContainsKey( name.ToLower() ) ) {
                // old format
                return RankManager.RanksByName[name.ToLower()]; // LEGACY

            } else {
                // totally unknown rank
                return null;
            }
        }
    }


    public sealed class RankDefinitionException : Exception {
        public RankDefinitionException( string rankName, string message )
            : base( message ) {
            RankName = rankName;
        }

        [StringFormatMethod( "message" )]
        public RankDefinitionException( string rankName, string message, params object[] args ) :
            base( String.Format( message, args ) ) {
            RankName=rankName;
        }


        public string RankName { get; private set; }
    }
}