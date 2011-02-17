﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;

namespace fCraft {

    /// <summary>
    /// Callback for a chat command.
    /// </summary>
    /// <param name="source">Player who called the command.</param>
    /// <param name="message">Command and its arguments.</param>
    public delegate void CommandHandler( Player source, Command message );

    /// <summary>
    /// Callback for displaying help information for chat commands that require a non-static/personalized help message.
    /// </summary>
    /// <param name="source">Player who is asking for help.</param>
    /// <returns>String to print to player.</returns>
    public delegate string HelpHandler( Player source );

    /// <summary>
    /// Describes a chat command handler. Defined properties and usage/help information, and specifies a callback.
    /// </summary>
    public sealed class CommandDescriptor {
        public string name;                 // main name
        public string[] aliases;            // list of aliases
        public bool consoleSafe;            // if true, command can be called from console (defaults to false)
        public Permission[] permissions;    // list of required permissions
        public string usage;                // short help
        public string help;                 // full help
        public CommandHandler handler;      // callback function to execute the command
        public HelpHandler helpHandler;     // callback function to provide custom help (optional)
        public bool hidden;                 // hidden command does not show up in /help


        public void PrintUsage( Player player ) {
            if( usage != null ) {
                player.Message( "Usage: &H{0}", usage );
            } else {
                player.Message( "Usage: &H/{0}", name );
            }
        }


        #region Events

        public event EventHandler<CommandRegisteredEventArgs> Registered;

        public event EventHandler<CommandCallingEventArgs> Calling;

        public event EventHandler<CommandCalledEventArgs> Called;


        internal void RaiseRegisteredEvent() {
            var h = Registered;
            var e = new CommandRegisteredEventArgs( this );
            if( h != null ) h( this, e );
        }


        internal bool RaiseCallingEvent( Command cmd, Player player ) {
            var h = Calling;
            var e = new CommandCallingEventArgs( cmd, this, player );
            if( h == null ) return false;
            h( this.Calling, e );
            return e.Cancel;
        }


        internal void RaiseCalledEvent( Command cmd, Player player ) {
            var h = Called;
            var e = new CommandCalledEventArgs( cmd, this, player );
            if( h != null ) h( this, e );
        }

        #endregion


        public override string ToString() {
            return String.Format( "CommandDescriptor({0})", name );
        }
    }
}