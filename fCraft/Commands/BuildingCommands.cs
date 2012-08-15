﻿// Copyright 2009-2012 Matvei Stefarov <me@matvei.org>
using System;
using System.Linq;
using fCraft.Drawing;
using fCraft.MapConversion;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace fCraft {
    /// <summary> Commands for placing specific blocks (solid, water, grass),
    /// and switching block placement modes (paint, bind). </summary>
    static class BuildingCommands {

        public static int MaxUndoCount = 2000000;

        const string GeneralDrawingHelp = " Use &H/Cancel&S to cancel selection mode. " +
                                          "Use &H/Undo&S to stop and undo the last command.";

        internal static void Init() {
            CommandManager.RegisterCommand( CdBind );
            CommandManager.RegisterCommand( CdGrass );
            CommandManager.RegisterCommand( CdLava );
            CommandManager.RegisterCommand( CdPaint );
            CommandManager.RegisterCommand( CdSolid );
            CommandManager.RegisterCommand( CdWater );

            CommandManager.RegisterCommand( CdCancel );
            CommandManager.RegisterCommand( CdMark );
            CommandManager.RegisterCommand( CdUndo );
            CommandManager.RegisterCommand( CdRedo );

            CommandManager.RegisterCommand( CdReplace );
            CommandManager.RegisterCommand( CdReplaceNot );
            CommandManager.RegisterCommand( CdReplaceBrush );
            CdReplace.Help += GeneralDrawingHelp;
            CdReplaceNot.Help += GeneralDrawingHelp;
            CdReplaceBrush.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdCopySlot );
            CommandManager.RegisterCommand( CdCopy );
            CommandManager.RegisterCommand( CdCut );
            CommandManager.RegisterCommand( CdPaste );
            CommandManager.RegisterCommand( CdPasteNot );
            CommandManager.RegisterCommand( CdPasteX );
            CommandManager.RegisterCommand( CdPasteNotX );
            CommandManager.RegisterCommand( CdMirror );
            CommandManager.RegisterCommand( CdRotate );
            CdCut.Help += GeneralDrawingHelp;
            CdPaste.Help += GeneralDrawingHelp;
            CdPasteNot.Help += GeneralDrawingHelp;
            CdPasteX.Help += GeneralDrawingHelp;
            CdPasteNotX.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdRestore );
            CdRestore.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdCuboid );
            CommandManager.RegisterCommand( CdCuboidWireframe );
            CommandManager.RegisterCommand( CdCuboidHollow );
            CdCuboid.Help += GeneralDrawingHelp;
            CdCuboidHollow.Help += GeneralDrawingHelp;
            CdCuboidWireframe.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdEllipsoid );
            CommandManager.RegisterCommand( CdEllipsoidHollow );
            CdEllipsoid.Help += GeneralDrawingHelp;
            CdEllipsoidHollow.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdLine );
            CommandManager.RegisterCommand( CdTriangle );
            CommandManager.RegisterCommand( CdTriangleWireframe );
            CdLine.Help += GeneralDrawingHelp;
            CdTriangle.Help += GeneralDrawingHelp;
            CdTriangleWireframe.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdSphere );
            CommandManager.RegisterCommand( CdSphereHollow );
            CommandManager.RegisterCommand( CdTorus );
            CdSphere.Help += GeneralDrawingHelp;
            CdSphereHollow.Help += GeneralDrawingHelp;
            CdTorus.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdFill2D );
            CdFill2D.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdUndoArea );
            CommandManager.RegisterCommand( CdUndoPlayer );
            CdUndoArea.Help += GeneralDrawingHelp;

            CommandManager.RegisterCommand( CdStatic );

            //CommandManager.RegisterCommand( CdTree );
        }


        #region DrawOperations & Brushes

        static readonly CommandDescriptor CdCuboid = new CommandDescriptor {
            Name = "Cuboid",
            Aliases = new[] { "blb", "c", "z" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Fills a rectangular area (cuboid) with blocks.",
            Handler = CuboidHandler
        };

        static void CuboidHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new CuboidDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdCuboidWireframe = new CommandDescriptor {
            Name = "CuboidW",
            Aliases = new[] { "cubw", "cw", "bfb" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Draws a wireframe box (a frame) around the selected rectangular area.",
            Handler = CuboidWireframeHandler
        };

        static void CuboidWireframeHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new CuboidWireframeDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdCuboidHollow = new CommandDescriptor {
            Name = "CuboidH",
            Aliases = new[] { "cubh", "ch", "h", "bhb" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Surrounds the selected rectangular area with a box of blocks. " +
                   "Unless two blocks are specified, leaves the inside untouched.",
            Handler = CuboidHollowHandler
        };

        static void CuboidHollowHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new CuboidHollowDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdEllipsoid = new CommandDescriptor {
            Name = "Ellipsoid",
            Aliases = new[] { "e" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Fills an ellipsoid-shaped area (elongated sphere) with blocks.",
            Handler = EllipsoidHandler
        };

        static void EllipsoidHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new EllipsoidDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdEllipsoidHollow = new CommandDescriptor {
            Name = "EllipsoidH",
            Aliases = new[] { "eh" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Surrounds the selected an ellipsoid-shaped area (elongated sphere) with a shell of blocks.",
            Handler = EllipsoidHollowHandler
        };

        static void EllipsoidHollowHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new EllipsoidHollowDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdSphere = new CommandDescriptor {
            Name = "Sphere",
            Aliases = new[] { "sp", "spheroid" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw, Permission.DrawAdvanced },
            RepeatableSelection = true,
            Help = "Fills a spherical area with blocks. " +
                   "The first mark denotes the CENTER of the sphere, and " +
                   "distance to the second mark denotes the radius.",
            Handler = SphereHandler
        };

        static void SphereHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new SphereDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdSphereHollow = new CommandDescriptor {
            Name = "SphereH",
            Aliases = new[] { "sph", "hsphere" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw, Permission.DrawAdvanced },
            RepeatableSelection = true,
            Help = "Surrounds a spherical area with a shell of blocks. " +
                   "The first mark denotes the CENTER of the sphere, and " +
                   "distance to the second mark denotes the radius.",
            Handler = SphereHollowHandler
        };

        static void SphereHollowHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new SphereHollowDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdLine = new CommandDescriptor {
            Name = "Line",
            Aliases = new[] { "ln" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Draws a continuous line between two points with blocks. " +
                   "Marks do not have to be aligned.",
            Handler = LineHandler
        };

        static void LineHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new LineDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdTriangleWireframe = new CommandDescriptor {
            Name = "TriangleW",
            Aliases = new[] { "tw" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Draws lines between three points, to form a triangle.",
            Handler = TriangleWireframeHandler
        };

        static void TriangleWireframeHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new TriangleWireframeDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdTriangle = new CommandDescriptor {
            Name = "Triangle",
            Aliases = new[] { "t" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Help = "Draws a triangle between three points.",
            Handler = TriangleHandler
        };

        static void TriangleHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new TriangleDrawOperation( player ) );
        }



        static readonly CommandDescriptor CdTorus = new CommandDescriptor {
            Name = "Torus",
            Aliases = new[] { "donut", "bagel" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw, Permission.DrawAdvanced },
            RepeatableSelection = true,
            Help = "Draws a horizontally-oriented torus. The first mark denotes the CENTER of the torus, horizontal " +
                   "distance to the second mark denotes the ring radius, and the vertical distance to the second mark denotes the " +
                   "tube radius",
            Handler = TorusHandler
        };

        static void TorusHandler( Player player, CommandReader cmd ) {
            DrawOperationBegin( player, cmd, new TorusDrawOperation( player ) );
        }



        static void DrawOperationBegin( Player player, CommandReader cmd, DrawOperation op ) {
            // try to create instance of player's currently selected brush
            // all command parameters are passed to the brush
            IBrushInstance brush = player.Brush.MakeInstance( player, cmd, op );

            // MakeInstance returns null if there were problems with syntax, abort
            if( brush == null ) return;
            op.Brush = brush;
            player.SelectionStart( op.ExpectedMarks, DrawOperationCallback, op, Permission.Draw );
            player.Message( "{0}: Click or &H/Mark&S {1} blocks.",
                            op.Description, op.ExpectedMarks );
        }


        static void DrawOperationCallback( Player player, Vector3I[] marks, object tag ) {
            DrawOperation op = (DrawOperation)tag;
            if( !op.Prepare( marks ) ) return;
            if( !player.CanDraw( op.BlocksTotalEstimate ) ) {
                player.MessageNow( "You are only allowed to run draw commands that affect up to {0} blocks. This one would affect {1} blocks.",
                                   player.Info.Rank.DrawLimit,
                                   op.Bounds.Volume );
                op.Cancel();
                return;
            }
            player.Message( "{0}: Processing ~{1} blocks.",
                            op.Description, op.BlocksTotalEstimate );
            op.Begin();
        }

        #endregion


        #region Fill

        static readonly CommandDescriptor CdFill2D = new CommandDescriptor {
            Name = "Fill2D",
            Aliases = new[] { "f2d" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw, Permission.DrawAdvanced },
            RepeatableSelection = true,
            Help = "Fills a continuous area with blocks, in 2D. " +
                   "Takes just 1 mark, and replaces blocks of the same type as the block you clicked. " +
                   "Works similar to \"Paint Bucket\" tool in Photoshop. " +
                   "Direction of effect is determined by where the player is looking.",
            Handler = Fill2DHandler
        };

        static void Fill2DHandler( Player player, CommandReader cmd ) {
            Fill2DDrawOperation op = new Fill2DDrawOperation( player );
            if( !op.ReadParams( cmd ) ) return;
            player.SelectionStart( 1, Fill2DCallback, op, Permission.Draw );
            player.Message( "{0}: Click a block to start filling.", op.Description );
        }


        static void Fill2DCallback( Player player, Vector3I[] marks, object tag ) {
            DrawOperation op = (DrawOperation)tag;
            if( !op.Prepare( marks ) ) return;
            if( player.WorldMap.GetBlock( marks[0] ) == Block.Air ) {
                player.Confirm( Fill2DConfirmCallback, op, "{0}: Replace air?", op.Description );
            } else {
                Fill2DConfirmCallback( player, op, false );
            }
        }


        static void Fill2DConfirmCallback( Player player, object tag, bool fromConsole ) {
            Fill2DDrawOperation op = (Fill2DDrawOperation)tag;
            player.Message( "{0}: Filling in a {1}x{1} area...",
                            op.Description, player.Info.Rank.FillLimit );
            op.Begin();
        }

        #endregion


        #region Block Commands

        static readonly CommandDescriptor CdSolid = new CommandDescriptor {
            Name = "Solid",
            Aliases = new[] { "s" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.PlaceAdmincrete },
            Help = "Toggles the admincrete placement mode. When enabled, any stone block you place is replaced with admincrete.",
            Handler = SolidHandler
        };

        static void SolidHandler( Player player, CommandReader cmd ) {
            if( player.GetBind( Block.Stone ) == Block.Admincrete ) {
                player.ResetBind( Block.Stone );
                player.Message( "Solid: OFF" );
            } else {
                player.Bind( Block.Stone, Block.Admincrete );
                player.Message( "Solid: ON. Stone blocks are replaced with admincrete." );
            }
        }



        static readonly CommandDescriptor CdPaint = new CommandDescriptor {
            Name = "Paint",
            Aliases = new[] { "p" },
            Category = CommandCategory.Building,
            Help = "When paint mode is on, any block you delete will be replaced with the block you are holding. " +
                   "Paint command toggles this behavior on and off.",
            Handler = PaintHandler
        };

        static void PaintHandler( Player player, CommandReader cmd ) {
            player.IsPainting = !player.IsPainting;
            if( player.IsPainting ) {
                player.Message( "Paint mode: ON" );
            } else {
                player.Message( "Paint mode: OFF" );
            }
        }



        static readonly CommandDescriptor CdGrass = new CommandDescriptor {
            Name = "Grass",
            Aliases = new[] { "g" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.PlaceGrass },
            Help = "Toggles the grass placement mode. When enabled, any dirt block you place is replaced with a grass block.",
            Handler = GrassHandler
        };

        static void GrassHandler( Player player, CommandReader cmd ) {
            if( player.GetBind( Block.Dirt ) == Block.Grass ) {
                player.ResetBind( Block.Dirt );
                player.Message( "Grass: OFF" );
            } else {
                player.Bind( Block.Dirt, Block.Grass );
                player.Message( "Grass: ON. Dirt blocks are replaced with grass." );
            }
        }



        static readonly CommandDescriptor CdWater = new CommandDescriptor {
            Name = "Water",
            Aliases = new[] { "w" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.PlaceWater },
            Help = "Toggles the water placement mode. When enabled, any blue or cyan block you place is replaced with water.",
            Handler = WaterHandler
        };

        static void WaterHandler( Player player, CommandReader cmd ) {
            if( player.GetBind( Block.Aqua ) == Block.Water ||
                player.GetBind( Block.Cyan ) == Block.Water ||
                player.GetBind( Block.Blue ) == Block.Water ) {
                player.ResetBind( Block.Aqua, Block.Cyan, Block.Blue );
                player.Message( "Water: OFF" );
            } else {
                player.Bind( Block.Aqua, Block.Water );
                player.Bind( Block.Cyan, Block.Water );
                player.Bind( Block.Blue, Block.Water );
                player.Message( "Water: ON. Blue blocks are replaced with water." );
            }
        }



        static readonly CommandDescriptor CdLava = new CommandDescriptor {
            Name = "Lava",
            Aliases = new[] { "l" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.PlaceLava },
            Help = "Toggles the lava placement mode. When enabled, any red block you place is replaced with lava.",
            Handler = LavaHandler
        };

        static void LavaHandler( Player player, CommandReader cmd ) {
            if( player.GetBind( Block.Red ) == Block.Lava ) {
                player.ResetBind( Block.Red );
                player.Message( "Lava: OFF" );
            } else {
                player.Bind( Block.Red, Block.Lava );
                player.Message( "Lava: ON. Red blocks are replaced with lava." );
            }
        }



        static readonly CommandDescriptor CdBind = new CommandDescriptor {
            Name = "Bind",
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Build },
            Help = "Assigns one blocktype to another. " +
                   "Allows to build blocktypes that are not normally buildable directly: admincrete, lava, water, grass, double step. " +
                   "Calling &H/Bind BlockType&S without second parameter resets the binding. If used with no params, ALL bindings are reset.",
            Usage = "/Bind OriginalBlockType ReplacementBlockType",
            Handler = BindHandler
        };

        static void BindHandler( Player player, CommandReader cmd ) {
            if( !cmd.HasNext ) {
                player.Message( "All bindings have been reset." );
                player.ResetAllBinds();
                return;
            }

            Block originalBlock;
            if( !cmd.NextBlock( player, false, out originalBlock ) ) return;

            if( !cmd.HasNext ) {
                if( player.GetBind( originalBlock ) != originalBlock ) {
                    player.Message( "{0} is no longer bound to {1}",
                                    originalBlock,
                                    player.GetBind( originalBlock ) );
                    player.ResetBind( originalBlock );
                } else {
                    player.Message( "{0} is not bound to anything.",
                                    originalBlock );
                }
                return;
            }

            Block replacementBlock;
            if( !cmd.NextBlock( player, false, out replacementBlock ) ) return;

            if( cmd.HasNext ) {
                CdBind.PrintUsage( player );
                return;
            }

            Permission permission = Permission.Build;
            switch( replacementBlock ) {
                case Block.Grass:
                    permission = Permission.PlaceGrass;
                    break;
                case Block.Admincrete:
                    permission = Permission.PlaceAdmincrete;
                    break;
                case Block.Water:
                    permission = Permission.PlaceWater;
                    break;
                case Block.Lava:
                    permission = Permission.PlaceLava;
                    break;
            }
            if( player.Can( permission ) ) {
                player.Bind( originalBlock, replacementBlock );
                player.Message( "{0} is now replaced with {1}", originalBlock, replacementBlock );
            } else {
                player.Message( "&WYou do not have {0} permission.", permission );
            }
        }

        #endregion


        static void DrawOneBlock( [NotNull] Player player, [NotNull] Map map, Block drawBlock, Vector3I coord,
                                  BlockChangeContext context, ref int blocks, ref int blocksDenied, UndoState undoState ) {
            if( player == null ) throw new ArgumentNullException( "player" );

            if( !map.InBounds( coord ) ) return;
            Block block = map.GetBlock( coord );
            if( block == drawBlock ) return;

            if( player.CanPlace( map, coord, drawBlock, context ) != CanPlaceResult.Allowed ) {
                blocksDenied++;
                return;
            }

            map.QueueUpdate( new BlockUpdate( null, coord, drawBlock ) );
            Player.RaisePlayerPlacedBlockEvent( player, map, coord, block, drawBlock, context );

            if( !undoState.IsTooLargeToUndo ) {
                if( !undoState.Add( coord, block ) ) {
                    player.MessageNow( "NOTE: This draw command is too massive to undo." );
                    player.LastDrawOp = null;
                }
            }
            blocks++;
        }


        static void DrawingFinished( [NotNull] Player player, string verb, int blocks, int blocksDenied ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( blocks == 0 ) {
                if( blocksDenied > 0 ) {
                    player.MessageNow( "No blocks could be {0} due to permission issues.", verb.ToLower() );
                } else {
                    player.MessageNow( "No blocks were {0}.", verb.ToLower() );
                }
            } else {
                if( blocksDenied > 0 ) {
                    player.MessageNow( "{0} {1} blocks ({2} blocks skipped due to permission issues)... " +
                                       "The map is now being updated.", verb, blocks, blocksDenied );
                } else {
                    player.MessageNow( "{0} {1} blocks... The map is now being updated.", verb, blocks );
                }
            }
            if( blocks > 0 ) {
                player.Info.ProcessDrawCommand( blocks );
                Server.RequestGC();
            }
        }


        #region Replace

        static void ReplaceHandlerInternal( IBrush factory, Player player, CommandReader cmd ) {
            CuboidDrawOperation op = new CuboidDrawOperation( player );
            IBrushInstance brush = factory.MakeInstance( player, cmd, op );
            if( brush == null ) return;
            op.Brush = brush;

            player.SelectionStart( 2, DrawOperationCallback, op, Permission.Draw );
            player.MessageNow( "{0}: Click or &H/Mark&S 2 blocks.",
                               op.Brush.InstanceDescription );
        }


        static readonly CommandDescriptor CdReplace = new CommandDescriptor {
            Name = "Replace",
            Aliases = new[] { "r" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection=true,
            Usage = "/Replace BlockToReplace [AnotherOne, ...] ReplacementBlock",
            Help = "Replaces all blocks of specified type(s) in an area.",
            Handler = ReplaceHandler
        };

        static void ReplaceHandler( Player player, CommandReader cmd ) {
            var replaceBrush = ReplaceBrushFactory.Instance.MakeBrush( player, cmd );
            if( replaceBrush == null ) return;
            ReplaceHandlerInternal( replaceBrush, player, cmd );
        }



        static readonly CommandDescriptor CdReplaceNot = new CommandDescriptor {
            Name = "ReplaceNot",
            Aliases = new[] { "rn" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            RepeatableSelection = true,
            Usage = "/ReplaceNot (ExcludedBlock [AnotherOne]) ReplacementBlock",
            Help = "Replaces all blocks EXCEPT specified type(s) in an area.",
            Handler = ReplaceNotHandler
        };

        static void ReplaceNotHandler( Player player, CommandReader cmd ) {
            var replaceBrush = ReplaceNotBrushFactory.Instance.MakeBrush( player, cmd );
            if( replaceBrush == null ) return;
            ReplaceHandlerInternal( replaceBrush, player, cmd );
        }



        static readonly CommandDescriptor CdReplaceBrush = new CommandDescriptor {
            Name = "ReplaceBrush",
            Aliases = new[] { "rb" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw, Permission.DrawAdvanced },
            RepeatableSelection = true,
            Usage = "/ReplaceBrush Block BrushName [Params]",
            Help = "Replaces all blocks of specified type(s) in an area with output of a given brush. " +
                   "See &H/Help brush&S for a list of available brushes.",
            Handler = ReplaceBrushHandler
        };

        static void ReplaceBrushHandler( Player player, CommandReader cmd ) {
            var replaceBrush = ReplaceBrushBrushFactory.Instance.MakeBrush( player, cmd );
            if( replaceBrush == null ) return;
            ReplaceHandlerInternal( replaceBrush, player, cmd );
        }
        #endregion


        #region Undo / Redo

        static readonly CommandDescriptor CdUndo = new CommandDescriptor {
            Name = "Undo",
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            Help = "Selectively removes changes from your last drawing command. " +
                   "Note that commands involving over 2 million blocks cannot be undone due to memory restrictions.",
            Handler = UndoHandler
        };

        static void UndoHandler( Player player, CommandReader cmd ) {
            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );
            if( cmd.HasNext ) {
                player.Message( "Undo command takes no parameters. Did you mean to do &H/UndoPlayer&S or &H/UndoArea&S?" );
                return;
            }

            string msg = "Undo: ";
            UndoState undoState = player.UndoPop();
            if( undoState == null ) {
                player.MessageNow( "There is currently nothing to undo." );
                return;
            }

            // Cancel the last DrawOp, if still in progress
            if( undoState.Op != null && !undoState.Op.IsDone && !undoState.Op.IsCancelled ) {
                undoState.Op.Cancel();
                msg += String.Format( "Cancelled {0} (was {1}% done). ",
                                     undoState.Op.Description,
                                     undoState.Op.PercentDone );
            }

            // Check if command was too massive.
            if( undoState.IsTooLargeToUndo ) {
                if( undoState.Op != null ) {
                    player.MessageNow( "Cannot undo {0}: too massive.", undoState.Op.Description );
                } else {
                    player.MessageNow( "Cannot undo: too massive." );
                }
                return;
            }

            // no need to set player.drawingInProgress here because this is done on the user thread
            Logger.Log( LogType.UserActivity,
                        "Player {0} initiated /Undo affecting {1} blocks (on world {2})",
                        player.Name,
                        undoState.Buffer.Count,
                        playerWorld.Name );

            msg += String.Format( "Restoring {0} blocks. Type &H/Redo&S to reverse.",
                                  undoState.Buffer.Count );
            player.MessageNow( msg );

            var op = new UndoDrawOperation( player, undoState, false );
            op.Prepare( new Vector3I[0] );
            op.Begin();
        }


        static readonly CommandDescriptor CdRedo = new CommandDescriptor {
            Name = "Redo",
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.Draw },
            Help = "Selectively removes changes from your last drawing command. " +
                   "Note that commands involving over 2 million blocks cannot be undone due to memory restrictions.",
            Handler = RedoHandler
        };

        static void RedoHandler( Player player, CommandReader cmd ) {
            if( cmd.HasNext ) {
                CdRedo.PrintUsage( player );
                return;
            }

            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );

            UndoState redoState = player.RedoPop();
            if( redoState == null ) {
                player.MessageNow( "There is currently nothing to redo." );
                return;
            }

            string msg = "Redo: ";
            if( redoState.Op != null && !redoState.Op.IsDone ) {
                redoState.Op.Cancel();
                msg += String.Format( "Cancelled {0} (was {1}% done). ",
                                     redoState.Op.Description,
                                     redoState.Op.PercentDone );
            }

            // no need to set player.drawingInProgress here because this is done on the user thread
            Logger.Log( LogType.UserActivity,
                        "Player {0} initiated /Redo affecting {1} blocks (on world {2})",
                        player.Name,
                        redoState.Buffer.Count,
                        playerWorld.Name );

            msg += String.Format( "Restoring {0} blocks. Type &H/Undo&S to reverse.",
                                  redoState.Buffer.Count );
            player.MessageNow( msg );

            var op = new UndoDrawOperation( player, redoState, true );
            op.Prepare( new Vector3I[0] );
            op.Begin();
        }

        #endregion


        #region Copy and Paste

        static readonly CommandDescriptor CdCopySlot = new CommandDescriptor {
            Name = "CopySlot",
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            Usage = "/CopySlot [#]",
            Help = "Selects a slot to copy to/paste from. The maximum number of slots is limited per-rank.",
            Handler = CopySlotHandler
        };

        static void CopySlotHandler( Player player, CommandReader cmd ) {
            int slotNumber;
            if( cmd.NextInt( out slotNumber ) ) {
                if( cmd.HasNext ) {
                    CdCopySlot.PrintUsage( player );
                    return;
                }
                if( slotNumber < 1 || slotNumber > player.Info.Rank.CopySlots ) {
                    player.Message( "CopySlot: Select a number between 1 and {0}", player.Info.Rank.CopySlots );
                } else {
                    player.CopySlot = slotNumber - 1;
                    CopyState info = player.GetCopyInformation();
                    if( info == null ) {
                        player.Message( "Selected copy slot {0} (unused).", slotNumber );
                    } else {
                        player.Message( "Selected copy slot {0}: {1} blocks from {2}, {3} old.",
                                        slotNumber, info.Buffer.Length,
                                        info.OriginWorld, DateTime.UtcNow.Subtract( info.CopyTime ).ToMiniString() );
                    }
                }
            } else {
                CopyState[] slots = player.CopyInformation;
                player.Message( "Using {0} of {1} slots. Selected slot: {2}",
                                slots.Count( info => info != null ), player.Info.Rank.CopySlots, player.CopySlot + 1 );
                for( int i = 0; i < slots.Length; i++ ) {
                    if( slots[i] != null ) {
                        player.Message( "  {0}: {1} blocks from {2}, {3} old",
                                        i + 1, slots[i].Buffer.Length,
                                        slots[i].OriginWorld,
                                        DateTime.UtcNow.Subtract( slots[i].CopyTime ).ToMiniString() );
                    }
                }
            }
        }



        static readonly CommandDescriptor CdCopy = new CommandDescriptor {
            Name = "Copy",
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            Help = "Copy blocks for pasting. " +
                   "Used together with &H/Paste&S and &H/PasteNot&S commands. " +
                   "Note that pasting starts at the same corner that you started &H/Copy&S from.",
            Handler = CopyHandler
        };

        static void CopyHandler( Player player, CommandReader cmd ) {
            if( cmd.HasNext ) {
                CdCopy.PrintUsage( player );
                return;
            }
            player.SelectionStart( 2, CopyCallback, null, CdCopy.Permissions );
            player.MessageNow( "Copy: Click or &H/Mark&S 2 blocks." );
        }


        static void CopyCallback( Player player, Vector3I[] marks, object tag ) {
            int sx = Math.Min( marks[0].X, marks[1].X );
            int ex = Math.Max( marks[0].X, marks[1].X );
            int sy = Math.Min( marks[0].Y, marks[1].Y );
            int ey = Math.Max( marks[0].Y, marks[1].Y );
            int sz = Math.Min( marks[0].Z, marks[1].Z );
            int ez = Math.Max( marks[0].Z, marks[1].Z );
            BoundingBox bounds = new BoundingBox( sx, sy, sz, ex, ey, ez );

            int volume = bounds.Volume;
            if( !player.CanDraw( volume ) ) {
                player.MessageNow( String.Format( "You are only allowed to run commands that affect up to {0} blocks. This one would affect {1} blocks.",
                                               player.Info.Rank.DrawLimit, volume ) );
                return;
            }

            // remember dimensions and orientation
            CopyState copyInfo = new CopyState( marks[0], marks[1] );

            Map map = player.WorldMap;
            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );

            for( int x = sx; x <= ex; x++ ) {
                for( int y = sy; y <= ey; y++ ) {
                    for( int z = sz; z <= ez; z++ ) {
                        copyInfo.Buffer[x - sx, y - sy, z - sz] = map.GetBlock( x, y, z );
                    }
                }
            }

            copyInfo.OriginWorld = playerWorld.Name;
            copyInfo.CopyTime = DateTime.UtcNow;
            player.SetCopyInformation( copyInfo );

            player.MessageNow( "{0} blocks copied into slot #{1}, origin at {2} corner. You can now &H/Paste",
                               volume,
                               player.CopySlot + 1,
                               copyInfo.OriginCorner );

            Logger.Log( LogType.UserActivity,
                        "{0} copied {1} blocks from world {2} (between {3} and {4}).",
                        player.Name, volume, playerWorld.Name,
                        bounds.MinVertex, bounds.MaxVertex );
        }



        static readonly CommandDescriptor CdCut = new CommandDescriptor {
            Name = "Cut",
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            RepeatableSelection = true,
            Help = "Copies and removes blocks for pasting. Unless a different block type is specified, the area is filled with air. " +
                   "Used together with &H/Paste&S and &H/PasteNot&S commands. " +
                   "Note that pasting starts at the same corner that you started &H/Cut&S from.",
            Usage = "/Cut [FillBlock]",
            Handler = CutHandler
        };

        static void CutHandler( Player player, CommandReader cmd ) {
            Block fillBlock = Block.Air;
            if( cmd.HasNext ) {
                if( !cmd.NextBlock( player, false, out fillBlock ) ) return;
                if( cmd.HasNext ) {
                    CdCut.PrintUsage( player );
                    return;
                }
            }

            CutDrawOperation op = new CutDrawOperation( player ) {
                Brush = new NormalBrush( fillBlock )
            };

            player.SelectionStart( 2, DrawOperationCallback, op, Permission.Draw );
            player.Message( "{0}: Click 2 or &H/Mark&S 2 blocks.",
                            op.Description );
        }


        static readonly CommandDescriptor CdMirror = new CommandDescriptor {
            Name = "Mirror",
            Aliases = new[] { "flip" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            Help = "Flips copied blocks along specified axis/axes. " +
                   "The axes are: X = horizontal (east-west), Y = horizontal (north-south), Z = vertical. " +
                   "You can mirror more than one axis at a time, e.g. &H/Mirror X Y",
            Usage = "/Mirror [X] [Y] [Z]",
            Handler = MirrorHandler
        };

        static void MirrorHandler( Player player, CommandReader cmd ) {
            CopyState originalInfo = player.GetCopyInformation();
            if( originalInfo == null ) {
                player.MessageNow( "Nothing to flip! Copy something first." );
                return;
            }

            // clone to avoid messing up any paste-in-progress
            CopyState info = new CopyState( originalInfo );

            bool flipX = false, flipY = false, flipH = false;
            string axis;
            while( (axis = cmd.Next()) != null ) {
                foreach( char c in axis.ToLower() ) {
                    if( c == 'x' ) flipX = true;
                    if( c == 'y' ) flipY = true;
                    if( c == 'z' ) flipH = true;
                }
            }

            if( !flipX && !flipY && !flipH ) {
                CdMirror.PrintUsage( player );
                return;
            }

            Block block;

            if( flipX ) {
                int left = 0;
                int right = info.Dimensions.X - 1;
                while( left < right ) {
                    for( int y = info.Dimensions.Y - 1; y >= 0; y-- ) {
                        for( int z = info.Dimensions.Z - 1; z >= 0; z-- ) {
                            block = info.Buffer[left, y, z];
                            info.Buffer[left, y, z] = info.Buffer[right, y, z];
                            info.Buffer[right, y, z] = block;
                        }
                    }
                    left++;
                    right--;
                }
            }

            if( flipY ) {
                int left = 0;
                int right = info.Dimensions.Y - 1;
                while( left < right ) {
                    for( int x = info.Dimensions.X - 1; x >= 0; x-- ) {
                        for( int z = info.Dimensions.Z - 1; z >= 0; z-- ) {
                            block = info.Buffer[x, left, z];
                            info.Buffer[x, left, z] = info.Buffer[x, right, z];
                            info.Buffer[x, right, z] = block;
                        }
                    }
                    left++;
                    right--;
                }
            }

            if( flipH ) {
                int left = 0;
                int right = info.Dimensions.Z - 1;
                while( left < right ) {
                    for( int x = info.Dimensions.X - 1; x >= 0; x-- ) {
                        for( int y = info.Dimensions.Y - 1; y >= 0; y-- ) {
                            block = info.Buffer[x, y, left];
                            info.Buffer[x, y, left] = info.Buffer[x, y, right];
                            info.Buffer[x, y, right] = block;
                        }
                    }
                    left++;
                    right--;
                }
            }

            if( flipX ) {
                if( flipY ) {
                    if( flipH ) {
                        player.Message( "Flipped copy along all axes." );
                    } else {
                        player.Message( "Flipped copy along X (east/west) and Y (north/south) axes." );
                    }
                } else {
                    if( flipH ) {
                        player.Message( "Flipped copy along X (east/west) and Z (vertical) axes." );
                    } else {
                        player.Message( "Flipped copy along X (east/west) axis." );
                    }
                }
            } else {
                if( flipY ) {
                    if( flipH ) {
                        player.Message( "Flipped copy along Y (north/south) and Z (vertical) axes." );
                    } else {
                        player.Message( "Flipped copy along Y (north/south) axis." );
                    }
                } else {
                    player.Message( "Flipped copy along Z (vertical) axis." );
                }
            }

            player.SetCopyInformation( info );
        }



        static readonly CommandDescriptor CdRotate = new CommandDescriptor {
            Name = "Rotate",
            Aliases = new[] { "spin" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            Help = "Rotates copied blocks around specifies axis/axes. If no axis is given, rotates around Z (vertical).",
            Usage = "/Rotate (-90|90|180|270) (X|Y|Z)",
            Handler = RotateHandler
        };

        static void RotateHandler( Player player, CommandReader cmd ) {
            CopyState originalInfo = player.GetCopyInformation();
            if( originalInfo == null ) {
                player.MessageNow( "Nothing to rotate! Copy something first." );
                return;
            }

            int degrees;
            if( !cmd.NextInt( out degrees ) || (degrees != 90 && degrees != -90 && degrees != 180 && degrees != 270) ) {
                CdRotate.PrintUsage( player );
                return;
            }

            string axisName = cmd.Next();
            Axis axis = Axis.Z;
            if( axisName != null ) {
                switch( axisName.ToLower() ) {
                    case "x":
                        axis = Axis.X;
                        break;
                    case "y":
                        axis = Axis.Y;
                        break;
                    case "z":
                    case "h":
                        axis = Axis.Z;
                        break;
                    default:
                        CdRotate.PrintUsage( player );
                        return;
                }
            }

            // allocate the new buffer
            Block[, ,] oldBuffer = originalInfo.Buffer;
            Block[, ,] newBuffer;

            if( degrees == 180 ) {
                newBuffer = new Block[oldBuffer.GetLength( 0 ), oldBuffer.GetLength( 1 ), oldBuffer.GetLength( 2 )];

            } else if( axis == Axis.X ) {
                newBuffer = new Block[oldBuffer.GetLength( 0 ), oldBuffer.GetLength( 2 ), oldBuffer.GetLength( 1 )];

            } else if( axis == Axis.Y ) {
                newBuffer = new Block[oldBuffer.GetLength( 2 ), oldBuffer.GetLength( 1 ), oldBuffer.GetLength( 0 )];

            } else { // axis == Axis.Z
                newBuffer = new Block[oldBuffer.GetLength( 1 ), oldBuffer.GetLength( 0 ), oldBuffer.GetLength( 2 )];
            }

            // clone to avoid messing up any paste-in-progress
            CopyState info = new CopyState( originalInfo, newBuffer );

            // construct the rotation matrix
            int[,] matrix = new[,]{
                {1,0,0},
                {0,1,0},
                {0,0,1}
            };

            int a, b;
            switch( axis ) {
                case Axis.X:
                    a = 1;
                    b = 2;
                    break;
                case Axis.Y:
                    a = 0;
                    b = 2;
                    break;
                default:
                    a = 0;
                    b = 1;
                    break;
            }

            switch( degrees ) {
                case 90:
                    matrix[a, a] = 0;
                    matrix[b, b] = 0;
                    matrix[a, b] = -1;
                    matrix[b, a] = 1;
                    break;
                case 180:
                    matrix[a, a] = -1;
                    matrix[b, b] = -1;
                    break;
                case -90:
                case 270:
                    matrix[a, a] = 0;
                    matrix[b, b] = 0;
                    matrix[a, b] = 1;
                    matrix[b, a] = -1;
                    break;
            }

            // apply the rotation matrix
            for( int x = oldBuffer.GetLength( 0 ) - 1; x >= 0; x-- ) {
                for( int y = oldBuffer.GetLength( 1 ) - 1; y >= 0; y-- ) {
                    for( int z = oldBuffer.GetLength( 2 ) - 1; z >= 0; z-- ) {
                        int nx = (matrix[0, 0] < 0 ? oldBuffer.GetLength( 0 ) - 1 - x : (matrix[0, 0] > 0 ? x : 0)) +
                                 (matrix[0, 1] < 0 ? oldBuffer.GetLength( 1 ) - 1 - y : (matrix[0, 1] > 0 ? y : 0)) +
                                 (matrix[0, 2] < 0 ? oldBuffer.GetLength( 2 ) - 1 - z : (matrix[0, 2] > 0 ? z : 0));
                        int ny = (matrix[1, 0] < 0 ? oldBuffer.GetLength( 0 ) - 1 - x : (matrix[1, 0] > 0 ? x : 0)) +
                                 (matrix[1, 1] < 0 ? oldBuffer.GetLength( 1 ) - 1 - y : (matrix[1, 1] > 0 ? y : 0)) +
                                 (matrix[1, 2] < 0 ? oldBuffer.GetLength( 2 ) - 1 - z : (matrix[1, 2] > 0 ? z : 0));
                        int nz = (matrix[2, 0] < 0 ? oldBuffer.GetLength( 0 ) - 1 - x : (matrix[2, 0] > 0 ? x : 0)) +
                                 (matrix[2, 1] < 0 ? oldBuffer.GetLength( 1 ) - 1 - y : (matrix[2, 1] > 0 ? y : 0)) +
                                 (matrix[2, 2] < 0 ? oldBuffer.GetLength( 2 ) - 1 - z : (matrix[2, 2] > 0 ? z : 0));
                        newBuffer[nx, ny, nz] = oldBuffer[x, y, z];
                    }
                }
            }

            player.Message( "Rotated copy (slot {0}) by {1} degrees around {2} axis.",
                            info.Slot + 1, degrees, axis );
            player.SetCopyInformation( info );
        }



        static readonly CommandDescriptor CdPasteX = new CommandDescriptor {
            Name = "PasteX",
            Aliases = new[] { "px" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            RepeatableSelection = true,
            Help = "Pastes previously copied blocks, aligned. Used together with &H/Copy&S command. " +
                   "If one or more optional IncludedBlock parameters are specified, ONLY pastes blocks of specified type(s). " +
                   "Takes 2 marks: first sets the origin of pasting, and second sets the direction where to paste.",
            Usage = "/PasteX [IncludedBlock [AnotherOne etc]]",
            Handler = PasteXHandler
        };

        static void PasteXHandler( Player player, CommandReader cmd ) {
            PasteOpHandler( player, cmd, 2, new PasteDrawOperation( player, false ) );
        }



        static readonly CommandDescriptor CdPasteNotX = new CommandDescriptor {
            Name = "PasteNotX",
            Aliases = new[] { "pnx", "pxn" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            RepeatableSelection = true,
            Help = "Pastes previously copied blocks, aligned, except the given block type(s). " +
                    "Used together with &H/Copy&S command. " +
                   "Takes 2 marks: first sets the origin of pasting, and second sets the direction where to paste.",
            Usage = "/PasteNotX ExcludedBlock [AnotherOne etc]",
            Handler = PasteNotXHandler
        };

        static void PasteNotXHandler( Player player, CommandReader cmd ) {
            PasteOpHandler( player, cmd, 2, new PasteDrawOperation( player, true ) );
        }



        static readonly CommandDescriptor CdPaste = new CommandDescriptor {
            Name = "Paste",
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            RepeatableSelection = true,
            Help = "Pastes previously copied blocks. Used together with &H/Copy&S command. " +
                   "If one or more optional IncludedBlock parameters are specified, ONLY pastes blocks of specified type(s). " +
                   "Alignment semantics are... complicated.",
            Usage = "/Paste [IncludedBlock [AnotherOne etc]]",
            Handler = PasteHandler
        };

        static void PasteHandler( Player player, CommandReader cmd ) {
            PasteOpHandler( player, cmd, 1, new QuickPasteDrawOperation( player, false ) );
        }



        static readonly CommandDescriptor CdPasteNot = new CommandDescriptor {
            Name = "PasteNot",
            Aliases = new[] { "pn" },
            Category = CommandCategory.Building,
            Permissions = new[] { Permission.CopyAndPaste },
            RepeatableSelection = true,
            Help = "Pastes previously copied blocks, except the given block type(s). " +
                   "Used together with &H/Copy&S command. " +
                   "Alignment semantics are... complicated.",
            Usage = "/PasteNot ExcludedBlock [AnotherOne etc]",
            Handler = PasteNotHandler
        };

        static void PasteNotHandler( Player player, CommandReader cmd ) {
            PasteOpHandler( player, cmd, 1, new QuickPasteDrawOperation( player, true ) );
        }


        static void PasteOpHandler( Player player, CommandReader cmd, int expectedMarks, DrawOpWithBrush op ) {
            if( !op.ReadParams( cmd ) ) return;
            player.SelectionStart( expectedMarks, DrawOperationCallback, op, Permission.Draw, Permission.CopyAndPaste );
            CopyState copyInfo = player.GetCopyInformation();
            if( copyInfo != null ) {
                player.MessageNow( "{0}: Click or &H/Mark&S the {1} corner.",
                                   op.Description, copyInfo.OriginCorner );
            } else {
                player.MessageNow( "{0}: Click or &H/Mark&S a block.",
                                   op.Description );
            }
        }

        #endregion


        #region Restore

        const BlockChangeContext RestoreContext = BlockChangeContext.Drawn | BlockChangeContext.Restored;


        static readonly CommandDescriptor CdRestore = new CommandDescriptor {
            Name = "Restore",
            Category = CommandCategory.World,
            Permissions = new[] {
                Permission.Draw,
                Permission.DrawAdvanced,
                Permission.CopyAndPaste,
                Permission.ManageWorlds
            },
            RepeatableSelection = true,
            Usage = "/Restore FileName",
            Help = "Selectively restores/pastes part of mapfile into the current world. "+
                   "If the filename contains spaces, surround it with quote marks.",
            Handler = RestoreHandler
        };

        static void RestoreHandler( Player player, CommandReader cmd ) {
            string fileName = cmd.Next();
            if( fileName == null ) {
                CdRestore.PrintUsage( player );
                return;
            }
            if( cmd.HasNext ) {
                CdRestore.PrintUsage( player );
                return;
            }

            string fullFileName = WorldManager.FindMapFile( player, fileName );
            if( fullFileName == null ) return;

            Map map;
            if( !MapUtility.TryLoad( fullFileName, out map ) ) {
                player.Message( "Could not load the given map file ({0})", fileName );
                return;
            }

            Map playerMap = player.WorldMap;
            if( playerMap.Width != map.Width || playerMap.Length != map.Length || playerMap.Height != map.Height ) {
                player.Message( "Mapfile dimensions must match your current world's dimensions ({0}x{1}x{2})",
                                playerMap.Width,
                                playerMap.Length,
                                playerMap.Height );
                return;
            }

            map.Metadata["fCraft.Temp", "FileName"] = fullFileName;
            player.SelectionStart( 2, RestoreCallback, map, CdRestore.Permissions );
            player.MessageNow( "Restore: Click or &H/Mark&S 2 blocks." );
        }


        static void RestoreCallback( Player player, Vector3I[] marks, object tag ) {
            BoundingBox selection = new BoundingBox( marks[0], marks[1] );
            Map map = (Map)tag;

            if( !player.CanDraw( selection.Volume ) ) {
                player.MessageNow(
                    "You are only allowed to restore up to {0} blocks at a time. This would affect {1} blocks.",
                    player.Info.Rank.DrawLimit,
                    selection.Volume );
                return;
            }

            int blocksDrawn = 0,
                blocksSkipped = 0;
            UndoState undoState = player.DrawBegin( null );

            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );
            Map playerMap = player.WorldMap;
            for( int x = selection.XMin; x <= selection.XMax; x++ ) {
                for( int y = selection.YMin; y <= selection.YMax; y++ ) {
                    for( int z = selection.ZMin; z <= selection.ZMax; z++ ) {
                        DrawOneBlock( player, playerMap, map.GetBlock( x, y, z ), new Vector3I( x, y, z ),
                                      RestoreContext,
                                      ref blocksDrawn, ref blocksSkipped, undoState );
                    }
                }
            }

            Logger.Log( LogType.UserActivity,
                        "{0} restored {1} blocks on world {2} (@{3},{4},{5} - {6},{7},{8}) from file {9}.",
                        player.Name, blocksDrawn,
                        playerWorld.Name,
                        selection.XMin, selection.YMin, selection.ZMin,
                        selection.XMax, selection.YMax, selection.ZMax,
                        map.Metadata["fCraft.Temp", "FileName"] );

            DrawingFinished( player, "Restored", blocksDrawn, blocksSkipped );
        }

        #endregion


        #region Mark, Cancel

        static readonly CommandDescriptor CdMark = new CommandDescriptor {
            Name = "Mark",
            Aliases = new[] { "m" },
            Category = CommandCategory.Building,
            Usage = "/Mark&S or &H/Mark X Y H",
            Help = "When making a selection (for drawing or zoning) use this to make a marker at your position in the world. " +
                   "If three numbers are given, those coordinates are used instead.",
            Handler = MarkHandler
        };

        static void MarkHandler( Player player, CommandReader cmd ) {
            Map map = player.WorldMap;
            int x, y, z;
            Vector3I coords;
            if( cmd.NextInt( out x ) && cmd.NextInt( out y ) && cmd.NextInt( out z ) ) {
                if( cmd.HasNext ) {
                    CdMark.PrintUsage( player );
                    return;
                }
                coords = new Vector3I( x, y, z );
            } else {
                coords = player.Position.ToBlockCoords();
            }
            coords.X = Math.Min( map.Width - 1, Math.Max( 0, coords.X ) );
            coords.Y = Math.Min( map.Length - 1, Math.Max( 0, coords.Y ) );
            coords.Z = Math.Min( map.Height - 1, Math.Max( 0, coords.Z ) );

            if( player.SelectionMarksExpected > 0 ) {
                player.SelectionAddMark( coords, true );
            } else {
                player.MessageNow( "Cannot mark - no selection in progress." );
            }
        }



        static readonly CommandDescriptor CdCancel = new CommandDescriptor {
            Name = "Cancel",
            Category = CommandCategory.Building,
            NotRepeatable = true,
            Help = "Cancels current selection (for drawing or zoning) operation, for instance if you misclicked on the first block. " +
                   "If you wish to stop a drawing in-progress, use &H/Undo&S instead.",
            Handler = CancelHandler
        };

        static void CancelHandler( Player player, CommandReader cmd ) {
            if( cmd.HasNext ) {
                CdCancel.PrintUsage( player );
                return;
            }
            if( player.IsMakingSelection ) {
                player.SelectionCancel();
                player.MessageNow( "Selection cancelled." );
            } else {
                player.MessageNow( "There is currently nothing to cancel." );
            }
        }

        #endregion


        #region UndoPlayer and UndoArea

        struct UndoAreaCountArgs {
            [NotNull]
            public PlayerInfo Target;

            [NotNull]
            public World World;

            public int MaxBlocks;

            [NotNull]
            public BoundingBox Area;
        }

        struct UndoAreaTimeArgs {
            [NotNull]
            public PlayerInfo Target;

            [NotNull]
            public World World;

            public TimeSpan Time;

            [NotNull]
            public BoundingBox Area;
        }

        static readonly CommandDescriptor CdUndoArea = new CommandDescriptor {
            Name = "UndoArea",
            Aliases = new[] { "ua" },
            Category = CommandCategory.Moderation,
            Permissions = new[] { Permission.UndoOthersActions },
            RepeatableSelection = true,
            Usage = "/UndoArea PlayerName [TimeSpan|BlockCount]",
            Help = "Reverses changes made by a given player in the current world, in the given area.",
            Handler = UndoAreaHandler
        };

        static void UndoAreaHandler( Player player, CommandReader cmd ) {

            if( !BlockDB.IsEnabledGlobally ) {
                player.Message( "&WBlockDB is disabled on this server." );
                return;
            }

            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );
            if( !playerWorld.BlockDB.IsEnabled ) {
                player.Message( "&WBlockDB is disabled in this world." );
                return;
            }

            string name = cmd.Next();
            string range = cmd.Next();
            if( name == null || range == null ) {
                CdUndoArea.PrintUsage( player );
                return;
            }
            if( cmd.HasNext ) {
                CdUndoArea.PrintUsage( player );
                return;
            }

            PlayerInfo target = PlayerDB.FindPlayerInfoOrPrintMatches( player, name );
            if( target == null ) return;

            if( player.Info != target && !player.Can( Permission.UndoOthersActions, target.Rank ) ) {
                player.Message( "You may only undo actions of players ranked {0}&S or lower.",
                                player.Info.Rank.GetLimit( Permission.UndoOthersActions ).ClassyName );
                player.Message( "Player {0}&S is ranked {1}", target.ClassyName, target.Rank.ClassyName );
                return;
            }

            int count;
            TimeSpan span;
            if( Int32.TryParse( range, out count ) ) {
                UndoAreaCountArgs args = new UndoAreaCountArgs {
                    Target = target,
                    World = playerWorld,
                    MaxBlocks = count
                };
                player.SelectionStart( 2, UndoAreaCountSelectionCallback, args, Permission.UndoOthersActions );

            } else if( range.TryParseMiniTimespan( out span ) ) {
                if( span > DateTimeUtil.MaxTimeSpan ) {
                    player.MessageMaxTimeSpan();
                    return;
                }
                UndoAreaTimeArgs args = new UndoAreaTimeArgs {
                    Target = target,
                    Time = span,
                    World = playerWorld
                };
                player.SelectionStart( 2, UndoAreaTimeSelectionCallback, args, Permission.UndoOthersActions );

            } else {
                CdUndoArea.PrintUsage( player );
                return;
            }

            player.MessageNow( "UndoArea: Click or &H/Mark&S 2 blocks." );
        }


        static void UndoAreaCountSelectionCallback( Player player, Vector3I[] marks, object tag ) {
            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );

            UndoAreaCountArgs args = (UndoAreaCountArgs)tag;
            args.World = playerWorld;
            args.Area = new BoundingBox( marks[0], marks[1] );
            BlockDBEntry[] changes = playerWorld.BlockDB.Lookup( args.Target, args.Area, args.MaxBlocks );
            if( changes.Length > 0 ) {
                player.Confirm( UndoAreaCountConfirmCallback, args, "Undo last {0} changes made by player {1}&S in this area?",
                                changes.Length, args.Target.ClassyName );
            } else {
                player.Message( "UndoArea: Nothing to undo in this area." );
            }
        }


        static void UndoAreaTimeSelectionCallback( Player player, Vector3I[] marks, object tag ) {
            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );

            UndoAreaTimeArgs args = (UndoAreaTimeArgs)tag;
            args.World = playerWorld;
            args.Area = new BoundingBox( marks[0], marks[1] );
            BlockDBEntry[] changes = playerWorld.BlockDB.Lookup( args.Target, args.Area, args.Time );
            if( changes.Length > 0 ) {
                player.Confirm( UndoAreaTimeConfirmCallback, args, "Undo changes ({0}) made by {1}&S in this area in the last {2}?",
                                changes.Length, args.Target.ClassyName, args.Time.ToMiniString() );
            } else {
                player.Message( "UndoArea: Nothing to undo in this area." );
            }
        }


        static void UndoAreaCountConfirmCallback( Player player, object tag, bool fromConsole ) {
            UndoAreaCountArgs args = (UndoAreaCountArgs)tag;
            BlockDBEntry[] changes = args.World.BlockDB.Lookup( args.Target, args.Area, args.MaxBlocks );

            BlockChangeContext context = BlockChangeContext.Drawn;
            if( player.Info == args.Target ) {
                context |= BlockChangeContext.UndoneSelf;
            } else {
                context |= BlockChangeContext.UndoneOther;
            }

            int blocks = 0,
                blocksDenied = 0;

            UndoState undoState = player.DrawBegin( null );
            Map map = player.WorldMap;

            for( int i = 0; i < changes.Length; i++ ) {
                DrawOneBlock( player, map, changes[i].OldBlock,
                              changes[i].Coord, context,
                              ref blocks, ref blocksDenied, undoState );
            }

            Logger.Log( LogType.UserActivity,
                        "{0} undid {1} blocks changed by player {2} (in a selection on world {3})",
                        player.Name,
                        blocks,
                        args.Target.Name,
                        args.World.Name );

            DrawingFinished( player, "UndoArea'd", blocks, blocksDenied );
        }


        static void UndoAreaTimeConfirmCallback( Player player, object tag, bool fromConsole ) {
            UndoAreaTimeArgs args = (UndoAreaTimeArgs)tag;
            BlockDBEntry[] changes = args.World.BlockDB.Lookup( args.Target, args.Area, args.Time );

            BlockChangeContext context = BlockChangeContext.Drawn;
            if( player.Info == args.Target ) {
                context |= BlockChangeContext.UndoneSelf;
            } else {
                context |= BlockChangeContext.UndoneOther;
            }

            int blocks = 0,
                blocksDenied = 0;

            UndoState undoState = player.DrawBegin( null );
            Map map = player.WorldMap;

            for( int i = 0; i < changes.Length; i++ ) {
                DrawOneBlock( player, map, changes[i].OldBlock,
                              changes[i].Coord, context,
                              ref blocks, ref blocksDenied, undoState );
            }

            Logger.Log( LogType.UserActivity,
                        "{0} undid {1} blocks changed by player {2} (in a selection on world {3})",
                        player.Name,
                        blocks,
                        args.Target.Name,
                        args.World.Name );

            DrawingFinished( player, "UndoArea'd", blocks, blocksDenied );
        }



        static readonly CommandDescriptor CdUndoPlayer = new CommandDescriptor {
            Name = "UndoPlayer",
            Aliases = new[] { "up", "undox" },
            Category = CommandCategory.Moderation,
            Permissions = new[] { Permission.UndoOthersActions },
            Usage = "/UndoPlayer PlayerName [TimeSpan|BlockCount]",
            Help = "Reverses changes made by a given player in the current world.",
            Handler = UndoPlayerHandler
        };

        static void UndoPlayerHandler( Player player, CommandReader cmd ) {
            World playerWorld = player.World;
            if( playerWorld == null ) PlayerOpException.ThrowNoWorld( player );

            if( !BlockDB.IsEnabledGlobally ) {
                player.Message( "&WBlockDB is disabled on this server." );
                return;
            }

            if( !playerWorld.BlockDB.IsEnabled ) {
                player.Message( "&WBlockDB is disabled in this world." );
                return;
            }

            string range = cmd.Next();
            if( range == null ) {
                CdUndoPlayer.PrintUsage( player );
                return;
            }

            int countLimit = 0;
            TimeSpan ageLimit = TimeSpan.Zero;
            if( !Int32.TryParse( range, out countLimit ) && !range.TryParseMiniTimespan( out ageLimit ) ) {
                player.Message( "UndoPlayer: First parameter should be a number or a timespan." );
                return;
            }

            HashSet<PlayerInfo> targets = new HashSet<PlayerInfo>();
            bool allPlayers = false;
            while( cmd.HasNext ) {
                string name = cmd.Next();
                if( name == "*" ) {
                    if( allPlayers ) {
                        player.Message( "UndoPlayer: \"*\" was listed twice." );
                        return;
                    }
                    allPlayers = true;
                } else {
                    PlayerInfo target = PlayerDB.FindPlayerInfoOrPrintMatches( player, name );
                    if( target == null ) {
                        return;
                    }
                    if( targets.Contains( target ) ) {
                        player.Message( "UndoPlayer: Player {0}&S was listed twice.", target.ClassyName );
                        return;
                    }
                    if( player.Info != target && !player.Can( Permission.UndoAll ) &&
                        !player.Can( Permission.UndoOthersActions, target.Rank ) ) {
                        player.Message( "You may only undo actions of players ranked {0}&S or lower.",
                                        player.Info.Rank.GetLimit( Permission.UndoOthersActions ).ClassyName );
                        player.Message( "Player {0}&S is ranked {1}", target.ClassyName, target.Rank.ClassyName );
                        return;
                    }
                    targets.Add( target );
                }
            }

            if( targets.Count == 0 && !allPlayers ) {
                player.Message( "UndoPlayer: Specify at least one player name, or \"*\" to undo everyone." );
                return;
            }
            if( targets.Count > 0 && allPlayers ) {
                player.Message( "UndoPlayer: Cannot mix player names and \"*\"." );
                return;
            }
            
            PlayerInfo[] targetArray = targets.ToArray();

            string targetList;
            if( allPlayers ) {
                targetList = "everyone";
            } else {
                targetList = targetArray.JoinToClassyString();
            }
            BlockDBEntry[] changes;

            if( countLimit > 0 ) {
                if( !cmd.IsConfirmed ) {
                    player.Message( "Searching for last {0} changes made by {1}&S...",
                                    countLimit, targetList );
                }
                if( targetArray.Length == 0 ) {
                    changes = playerWorld.BlockDB.Lookup( countLimit );
                } else {
                    changes = playerWorld.BlockDB.Lookup( targetArray, countLimit );
                }
                if( changes.Length > 0 && !cmd.IsConfirmed ) {
                    player.Confirm( cmd, "Undo last {0} changes made by {1}&S?",
                                    changes.Length, targetList );
                    return;
                }

            } else if( ageLimit > TimeSpan.Zero ) {
                if( ageLimit > DateTimeUtil.MaxTimeSpan ) {
                    player.MessageMaxTimeSpan();
                    return;
                }
                if( !cmd.IsConfirmed ) {
                    player.Message( "Searching for changes made by {0}&S in the last {1}...",
                                    targetList, ageLimit.ToMiniString() );
                }
                if( targetArray.Length == 0 ) {
                    changes = playerWorld.BlockDB.Lookup( ageLimit );
                } else {
                    changes = playerWorld.BlockDB.Lookup( targetArray, ageLimit );
                }
                if( changes.Length > 0 && !cmd.IsConfirmed ) {
                    player.Confirm( cmd, "Undo changes ({0}) made by {1}&S in the last {2}?",
                                    changes.Length, targetList, ageLimit.ToMiniString() );
                    return;
                }

            } else {
                // should never happen
                player.Message( "UndoPlayer: First parameter should be a number or a timespan." );
                CdUndoPlayer.PrintUsage( player );
                return;
            }

            if( changes.Length == 0 ) {
                player.Message( "UndoPlayer: Found nothing to undo." );
                return;
            }

            var op = new UndoPlayerDrawOperation( player, changes );
            op.Prepare( new Vector3I[0] );
            op.Begin();
        }

        #endregion



        static readonly CommandDescriptor CdStatic = new CommandDescriptor {
            Name = "Static",
            Category = CommandCategory.Building,
            Help = "Toggles repetition of last selection on or off.",
            Handler = StaticHandler
        };

        static void StaticHandler( Player player, CommandReader cmd ) {
            if( cmd.HasNext ) {
                CdStatic.PrintUsage( player );
                return;
            }
            if( player.IsRepeatingSelection ) {
                player.Message( "Static: Off" );
                player.IsRepeatingSelection = false;
                player.SelectionCancel();
            } else {
                player.Message( "Static: On" );
                player.IsRepeatingSelection = true;
            }
        }
    }
}