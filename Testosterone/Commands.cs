// Part of FemtoCraft | Copyright 2012-2013 Matvei Stefarov <me@matvei.org> | See LICENSE.txt
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using JetBrains.Annotations;

namespace Testosterone {
   public class Commands {
       readonly Server server;
       readonly Config config;

       public Commands([NotNull] Server server) {
           if (server == null) throw new ArgumentNullException("server");
           this.server = server;
           config = server.config;
       }


       public void Parse( [NotNull] Player player, [NotNull] string message ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( message == null ) throw new ArgumentNullException( "message" );
            string command, param;
            int spaceIndex = message.IndexOf( ' ' );
            if( spaceIndex == -1 ) {
                command = message.Substring( 1 ).ToLower();
                param = null;
            } else {
                command = message.Substring( 1, spaceIndex - 1 ).ToLower();
                param = message.Substring( spaceIndex + 1 ).Trim();
            }
            Logger.LogCommand( "{0}: {1}", player.Name, message );

            switch( command ) {
                case "ops":
                    OpsHandler( player );
                    break;
                case "op":
                    OpHandler( player, param );
                    break;
                case "deop":
                    DeopHandler( player, param );
                    break;

                case "kick":
                case "k":
                    KickHandler( player, param );
                    break;

                case "solid":
                case "s":
                    SolidHandler( player );
                    break;
                case "water":
                case "w":
                    WaterHandler( player );
                    break;
                case "lava":
                case "l":
                    LavaHandler( player );
                    break;
                case "grass":
                case "g":
                    GrassHandler( player );
                    break;

                case "say":
                case "broadcast":
                    SayHandler( player, param );
                    break;

                case "tp":
                case "teleport":
                    TeleportHandler( player, param );
                    break;
                case "bring":
                    BringHandler( player, param );
                    break;

                case "setspawn":
                    SetSpawnHandler( player );
                    break;

                case "whitelist":
                    WhitelistHandler( player );
                    break;
                case "whitelistadd":
                    WhitelistAddHandler( player, param );
                    break;
                case "whitelistremove":
                    WhitelistRemoveHandler( player, param );
                    break;

                case "load":
                    LoadHandler( player, param );
                    break;
                case "save":
                    SaveHandler( player, param );
                    break;

                case "physics":
                    PhysicsHandler( player, param );
                    break;

                case "p":
                case "paint":
                    PaintHandler( player );
                    break;

                case "players":
                    PlayersHandler( player );
                    break;

                case "gen":
                    GenHandler( false, player, param );
                    break;
                case "genflat":
                    GenHandler( true, player, param );
                    break;

                default:
                    player.Message( "Unknown command \"{0}\"", command );
                    break;
            }
        }


        void OpsHandler( [NotNull] Player player ) {
            if( !config.RevealOps && !player.CheckIfOp() ) return;
            if( server.Ops.Count > 0 ) {
                string[] opNames = server.Ops.GetCopy();
                Array.Sort( opNames, StringComparer.OrdinalIgnoreCase );
                player.Message( "Ops: {0}", opNames.JoinToString( ", " ) );
            } else {
                player.Message( "There are no ops." );
            }
        }


        void OpHandler( [NotNull] Player player, [CanBeNull] string targetName ) {
            if( !player.CheckIfOp() || !player.CheckPlayerName( targetName ) ) return;
            if( server.Ops.Add( targetName ) ) {
                Player target = server.FindPlayerExact( targetName );
                if( target != null ) {
                    target.IsOp = true;
                    target.Message( "You are now op!" );
                    server.Players.Message( "Player {0} was opped by {1}",
                                            target.Name, player.Name );
                } else {
                    server.Players.Message( "Player {0} (offline) was opped by {1}",
                                            targetName, player.Name );
                }
            } else {
                player.Message( "Player {0} is already op", targetName );
            }
        }


        void DeopHandler( [NotNull] Player player, [CanBeNull] string targetName ) {
            if( !player.CheckIfOp() || !player.CheckPlayerName( targetName ) ) return;
            if( server.Ops.Remove( targetName ) ) {
                Player target = server.FindPlayerExact( targetName );
                if( target != null ) {
                    targetName = target.Name;
                    target.IsOp = false;
                    target.PlaceSolid = false;
                    target.PlaceWater = false;
                    target.PlaceLava = false;
                    target.PlaceGrass = false;
                    target.Message( "You are no longer op." );
                    server.Players.Message( "Player {0} was deopped by {1}",
                                            targetName, player.Name );
                } else {
                    server.Players.Message( "Player {0} (offline) was deopped by {1}",
                                            targetName, player.Name );
                }
            } else {
                player.Message( "Player {0} is not an op.", targetName );
            }
        }


        void KickHandler( [NotNull] Player player, [CanBeNull] string targetName ) {
            if( !player.CheckIfOp() || !player.CheckPlayerName( targetName ) ) return;
            Player target = server.FindPlayer( player, targetName );
            if( target == null ) return;
            target.Kick( "Kicked by " + player.Name );
            server.Players.Message( "Player {0} was kicked by {1}",
                                    target.Name, player.Name );
        }


         void SolidHandler( [NotNull] Player player ) {
            if( !player.CheckIfAllowed( config.AllowSolidBlocks, config.OpAllowSolidBlocks ) ) return;
            player.Message( player.PlaceSolid ? "Solid: OFF" : "Solid: ON" );
            player.PlaceSolid = !player.PlaceSolid;
        }


         void WaterHandler( [NotNull] Player player ) {
            if( !player.CheckIfAllowed( config.AllowWaterBlocks, config.OpAllowWaterBlocks ) ) return;
            player.Message( player.PlaceWater ? "Water: OFF" : "Water: ON" );
            player.PlaceWater = !player.PlaceWater;
        }


         void LavaHandler( [NotNull] Player player ) {
            if( !player.CheckIfAllowed( config.AllowLavaBlocks, config.OpAllowLavaBlocks ) ) return;
            player.Message( player.PlaceLava ? "Lava: OFF" : "Lava: ON" );
            player.PlaceLava = !player.PlaceLava;
        }


         void GrassHandler( [NotNull] Player player ) {
            if( !player.CheckIfAllowed( config.AllowGrassBlocks, config.OpAllowGrassBlocks ) ) return;
            player.Message( player.PlaceGrass ? "Grass: OFF" : "Grass: ON" );
            player.PlaceGrass = !player.PlaceGrass;
        }


         void SayHandler( [NotNull] Player player, [CanBeNull] string message ) {
            if( !player.CheckIfOp() ) return;
            if( message == null ) message = "";
            server.Players.Message( null, false, "&C" + message );
        }


         void TeleportHandler( [NotNull] Player player, [CanBeNull] string targetName ) {
            if( !player.CheckIfOp() || !player.CheckPlayerName( targetName ) ) return;
            if( player == server.ConsolePlayer ) {
                player.Message( "Can't teleport from console!" );
                return;
            }
            Player target = server.FindPlayer( player, targetName );
            if( target == null ) return;
            player.Send( Packet.MakeSelfTeleport( target.Position ) );
        }


         void BringHandler( [NotNull] Player player, [CanBeNull] string targetName ) {
            if( !player.CheckIfOp() || !player.CheckPlayerName( targetName ) ) return;
            if( player == server.ConsolePlayer ) {
                player.Message( "Can't bring from console!" );
                return;
            }
            Player target = server.FindPlayer( player, targetName );
            if( target == null ) return;
            target.Send( Packet.MakeSelfTeleport( player.Position ) );
        }


         void SetSpawnHandler( [NotNull] Player player ) {
            if( !player.CheckIfOp() ) return;
            if( player == server.ConsolePlayer ) {
                player.Message( "Can't set spawn from console!" );
                return;
            }
            player.Map.Spawn = player.Position;
            player.Map.ChangedSinceSave = true;
            player.Send( Packet.MakeAddEntity( 255, player.Name, player.Map.Spawn.GetFixed() ) );
            player.Send( Packet.MakeSelfTeleport( player.Map.Spawn ) );
            server.Players.Message( "Player {0} set a new spawn point.", player.Name );
        }


         void WhitelistHandler( [NotNull] Player player ) {
            if( config.UseWhitelist ) {
                string[] whitelistNames = server.Whitelist.GetCopy();
                Array.Sort( whitelistNames, StringComparer.OrdinalIgnoreCase );
                player.Message( "Whitelist: {0}", whitelistNames.JoinToString( ", " ) );
            } else {
                player.Message( "Whitelist is disabled." );
            }
        }


         void WhitelistAddHandler( [NotNull] Player player, [CanBeNull] string targetName ) {
            if( !player.CheckIfOp() || !player.CheckPlayerName( targetName ) ) return;
            if( !config.UseWhitelist ) {
                player.Message( "Whitelist is disabled." );
                return;
            }
            if( server.Whitelist.Add( targetName ) ) {
                server.Players.Message("Player {0} was whitelisted by {1}",
                               targetName, player.Name );
            } else {
                player.Message( "Player {0} is already whitelisted.", targetName );
            }
        }


         void WhitelistRemoveHandler( [NotNull] Player player, [CanBeNull] string targetName ) {
            if( !player.CheckIfOp() || !player.CheckPlayerName( targetName ) ) return;
            if( !config.UseWhitelist ) {
                player.Message( "Whitelist is disabled." );
                return;
            }
            if( server.Whitelist.Add( targetName ) ) {
                Player target = server.FindPlayerExact( targetName );
                if( target != null ) {
                    targetName = target.Name;
                    target.Kick( "Removed from whitelist by " + player.Name );
                }
                server.Players.Message( "Player {0} was removed from whitelist by {1}",
                                        targetName, player.Name );
            } else {
                player.Message( "Player {0} is not whitelisted.", targetName );
            }
        }


         void LoadHandler( [NotNull] Player player, [CanBeNull] string fileName ) {
            if( !player.CheckIfOp() ) return;
            if( fileName == null ) {
                player.Message( "Load: Filename required." );
                return;
            }
            try {
                player.MessageNow( "Loading map, please wait..." );
                Map map;
                if( fileName.EndsWith( ".lvl", StringComparison.OrdinalIgnoreCase ) ) {
                    map = LvlMapConverter.Load( fileName );
                } else {
                    player.Message( "Load: Unsupported map format." );
                    return;
                }
                server.Players.Message( "Player {0} changed map to {1}",
                                        player.Name, Path.GetFileName( fileName ) );
                server.ChangeMap( map );
            } catch( Exception ex ) {
                player.Message( "Could not load map: {0}: {1}", ex.GetType().Name, ex.Message );
            }
        }


         void SaveHandler( [NotNull] Player player, [CanBeNull] string fileName ) {
            if( !player.CheckIfOp() ) return;
            if( fileName == null || !fileName.EndsWith( ".lvl" ) ) {
                player.Message( "Load: Filename must end with .lvl" );
                return;
            }
            try {
                player.Map.Save( fileName );
                player.Message( "Map saved to {0}", Path.GetFileName( fileName ) );
            } catch( Exception ex ) {
                player.Message( "Could not save map: {0}: {1}", ex.GetType().Name, ex.Message );
                Logger.LogError( "Failed to save map: {0}", ex );
            }
        }


         void PhysicsHandler( [NotNull] Player player, [CanBeNull] string param ) {
            if( !player.CheckIfOp() ) return;
            if( param == null ) param = "";
            switch( param.ToLower() ) {
                case "":
                    // print info
                    player.Message( "Physics are: {0}", config.Physics ? "ON" : "OFF" );
                    List<string> modules = new List<string>();
                    if( config.PhysicsGrass ) modules.Add( "grass" );
                    if( config.PhysicsLava ) modules.Add( "lava" );
                    if( config.PhysicsPlants ) modules.Add( "plants" );
                    if( config.PhysicsSand ) modules.Add( "sand" );
                    if( config.PhysicsSnow ) modules.Add( "snow" ); // TODO: CPE-ify
                    if( config.PhysicsTrees ) modules.Add( "trees" );
                    if( config.PhysicsWater ) modules.Add( "water" );
                    if( modules.Count == 0 ) {
                        player.Message( "None of the modules are enabled." );
                    } else {
                        player.Message( "Following modules are enabled: {0}",
                                        modules.JoinToString( ", " ) );
                    }
                    break;

                case "on":
                    // toggle selected things on
                    if( server.config.Physics ) {
                        player.Message( "Physics are already enabled." );
                    } else {
                        player.MessageNow( "Enabling physics, please wait..." );
                        server.config.Physics = true;
                        player.Map.EnablePhysics();
                        Logger.Log( "Player {0} enabled physics.",
                                    player.Name );
                        player.Message( "Selected physics modules: ON" );
                    }
                    break;

                case "off":
                    // toggle everything off
                    if( !config.Physics ) {
                        player.Message( "Physics are already disabled." );
                    } else {
                        config.Physics = false;
                        player.Map.DisablePhysics();
                        Logger.Log( "Player {0} enabled physics.",
                                    player.Name );
                        player.Message( "All physics: OFF" );
                    }
                    break;

                case "grass":
                    config.PhysicsGrass = !config.PhysicsGrass;
                    Logger.Log( "Player {0} turned {1} grass physics.",
                                player.Name, config.PhysicsGrass ? "on" : "off" );
                    player.Message( "Grass physics: {0}",
                                    config.PhysicsGrass ? "ON" : "OFF" );
                    break;

                case "lava":
                    config.PhysicsLava = !config.PhysicsLava;
                    Logger.Log( "Player {0} turned {1} lava physics.",
                                player.Name, config.PhysicsLava ? "on" : "off" );
                    player.Message( "Lava physics: {0}",
                                    config.PhysicsLava ? "ON" : "OFF" );
                    break;

                case "plant":
                case "plants":
                    config.PhysicsPlants = !config.PhysicsPlants;
                    Logger.Log( "Player {0} turned {1} plant physics.",
                                player.Name, config.PhysicsPlants ? "on" : "off" );
                    player.Message( "Plant physics: {0}",
                                    config.PhysicsPlants ? "ON" : "OFF" );
                    break;

                case "sand":
                    config.PhysicsSand = !config.PhysicsSand;
                    Logger.Log( "Player {0} turned {1} sand/gravel physics.",
                                player.Name, config.PhysicsSand ? "on" : "off" );
                    player.Message( "Sand physics: {0}",
                                    config.PhysicsSand ? "ON" : "OFF" );
                    break;

                case "snow":
                    // TODO: CPE
                    config.PhysicsSnow = !config.PhysicsSnow;
                    Logger.Log( "Player {0} turned {1} snow physics.",
                                player.Name, config.PhysicsSnow ? "on" : "off" );
                    player.Message( "Snow physics: {0}",
                                    config.PhysicsSnow ? "ON" : "OFF" );
                    break;

                case "tree":
                case "trees":
                    config.PhysicsTrees = !config.PhysicsTrees;
                    Logger.Log( "Player {0} turned {1} tree physics.",
                                player.Name, config.PhysicsTrees ? "on" : "off" );
                    player.Message( "Tree physics: {0}",
                                    config.PhysicsTrees ? "ON" : "OFF" );
                    break;

                case "water":
                    config.PhysicsWater = !config.PhysicsWater;
                    Logger.Log( "Player {0} turned {1} water physics.",
                                player.Name, config.PhysicsWater ? "on" : "off" );
                    player.Message( "Water physics: {0}",
                                    config.PhysicsWater ? "ON" : "OFF" );
                    break;

                default:
                    player.Message( "Unknown /physics option \"{0}\"", param );
                    break;
            }
        }


        static void PaintHandler( [NotNull] Player player ) {
            if( player.CheckIfConsole() ) return;
            player.IsPainting = !player.IsPainting;
            player.Message( "Paint: {0}", player.IsPainting ? "ON" : "OFF" );
        }


        public void PlayersHandler( [NotNull] Player player ) {
            Player[] players = server.Players;
            Array.Sort( players, ( p1, p2 ) => StringComparer.OrdinalIgnoreCase.Compare( p1.Name, p2.Name ) );
            if( players.Length == 0 ) {
                player.Message( "There are no players online." );
            } else {
                string playerList;
                if( player.IsOp || config.RevealOps ) {
                    playerList = players.JoinToString( ", ", p => ( p.IsOp ? config.OpColor : "&F" ) + p.Name );
                } else {
                    playerList = players.JoinToString( ", ", p => p.Name );
                }
                if( players.Length % 10 == 1 ) {
                    player.Message( "There is {0} player online: {1}",
                                    players.Length, playerList );
                } else {
                    player.Message( "There are {0} players online: {1}",
                                    players.Length, playerList );
                }
            }
        }


        static void GenHandler( bool flat, [NotNull] Player player, [CanBeNull] string param ) {
            if( !player.CheckIfOp() ) return;

            string cmdName = ( flat ? "GenFlat" : "Gen" );
            if( String.IsNullOrEmpty( param ) ) {
                PrintGenUsage(cmdName, player );
                return;
            }

            string[] args = param.Split( ' ' );

            ushort width, length, height;
            if( args.Length != 4 ||
                !UInt16.TryParse( args[0], out width ) ||
                !UInt16.TryParse( args[1], out length ) ||
                !UInt16.TryParse( args[2], out height ) ) {
                PrintGenUsage( cmdName, player );
                return;
            }

            if( !IsPowerOfTwo( width ) || !IsPowerOfTwo( length ) || !IsPowerOfTwo( height ) ||
                width < 16 || length < 16 || height < 16 ||
                width > 1024 || length > 1024 || height > 1024 ) {
                    player.Message( "{0}: Map dimensions should be powers-of-2 between 16 and 1024", cmdName );
                    return;
            }

            string fileName = args[3];
            if( !fileName.EndsWith( ".lvl" ) ) {
                player.Message( "Load: Filename must end with .lvl" );
                return;
            }

            player.MessageNow( "Generating a {0}x{1}x{2} map...", width, length, height );
            Map map;
            if( flat ) {
                map = Map.CreateFlatgrass( width, length, height );
            } else {
                map = NotchyMapGenerator.Generate( width, length, height );
            }
            try {
                map.Save( fileName );
                player.Message( "Map saved to {0}", Path.GetFileName( fileName ) );
            } catch( Exception ex ) {
                player.Message( "Could not save map: {0}: {1}", ex.GetType().Name, ex.Message );
                Logger.LogError( "Failed to save map: {0}", ex );
            }
        }

        [Pure]
        static bool IsPowerOfTwo( int x ) {
            return ( x != 0 ) && ( ( x & ( x - 1 ) ) == 0 );
        }

        static void PrintGenUsage( [NotNull] string cmdName, [NotNull] Player player ) {
            if( cmdName == null ) throw new ArgumentNullException( "cmdName" );
            if( player == null ) throw new ArgumentNullException( "player" );
            player.Message( "Usage: /{0} Width Length Height filename.lvl", cmdName );
        }
    }
}