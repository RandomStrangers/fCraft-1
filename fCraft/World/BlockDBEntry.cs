﻿// Copyright 2009, 2010, 2011 Matvei Stefarov <me@matvei.org>
using System;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace fCraft{
    /// <summary> Struct representing a single block change.
    /// You may safely cast byte* pointers directly to BlockDBEntry* and vice versa. </summary>
    [StructLayout( LayoutKind.Sequential, Pack = 1 )]
    public struct BlockDBEntry {
        public const int Size = 20; // sizeof(BlockDBEntry)

        /// <summary> UTC Unix timestamp of the change. </summary>
        public readonly int Timestamp;

        /// <summary> Numeric PlayerDB id of the player who made the change. </summary>
        public readonly int PlayerID;

        /// <summary> X coordinate (horizontal), in terms of blocks. </summary>
        public readonly short X;

        /// <summary> Y coordinate (horizontal), in terms of blocks. </summary>
        public readonly short Y;

        /// <summary> Z coordinate (vertical), in terms of blocks. </summary>
        public readonly short Z;

        /// <summary> Block that previously occupied this coordinate </summary>
        public readonly Block OldBlock;

        /// <summary> Block that now occupies this coordinate </summary>
        public readonly Block NewBlock;

        public readonly BlockChangeContext Context;

        public BlockDBEntry( int timestamp, int playerID, short x, short y, short z, Block oldBlock, Block newBlock, BlockChangeContext flags ) {
            Timestamp = timestamp;
            PlayerID = playerID;
            X = x;
            Y = y;
            Z = z;
            OldBlock = oldBlock;
            NewBlock = newBlock;
            Context = flags;
        }

        public void Serialize( BinaryWriter writer ) {
            writer.Write( Timestamp );
            writer.Write( PlayerID );
            writer.Write( X );
            writer.Write( Y );
            writer.Write( Z );
            writer.Write( (byte)OldBlock );
            writer.Write( (byte)NewBlock );
            writer.Write( (int)Context );
        }
    }

    [Flags]
    public enum BlockChangeContext {
        Unknown = 0,
        Manual = 1,
        Drawn = 2,
        Replaced = 4,
        Pasted = 8,
        Cut = 16,
        UndoneSelf = 32,
        UndoneOther = 64,
        Restored = 64
    }
}
