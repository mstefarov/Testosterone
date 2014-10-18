// Part of FemtoCraft | Copyright 2012-2013 Matvei Stefarov <me@matvei.org> | See LICENSE.txt
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Testosterone.Packets;

namespace Testosterone {
    public class Server {
        public readonly Player ConsolePlayer;
        public readonly Heartbeat Heartbeat;
        public readonly Commands Commands;
        public readonly PacketManager PacketManager;

        public const string VersionString = "Testosterone";

        [NotNull]
        public Map Map { get; private set; }
        const string MapFileName = "map.lvl";

        [NotNull]
        public PlayerNameSet Ops { get; private set; }
        const string OpsFileName = "admins.txt";

        [NotNull]
        public PlayerNameSet Whitelist { get; private set; }
        const string WhitelistFileName = "whitelist.txt";

        internal readonly Config config;
        bool isRunning = false;


        public Server(Config config) {
            this.config = config;
            ConsolePlayer = new Player( this, "(console)" );
            Heartbeat = new Heartbeat(this);
            Commands = new Commands(this);
            PacketManager = new PacketManager();
        }


        public void Start() {
            if(isRunning)throw new InvalidOperationException("Already running!");
            isRunning = true;
#if !DEBUG
            try {
#endif
                Logger.Log( "Starting new instance of {0}", VersionString );

                // prepare to accept players and fire up the heartbeat
                for( byte i = 1; i <= SByte.MaxValue; i++ ) {
                    FreePlayerIDs.Push( i );
                }
                UpdatePlayerList();
                Heartbeat.Start();

                // load player and IP lists
                Ops = new PlayerNameSet(this, OpsFileName );
                Logger.Log( "Server: Tracking {2} ops.", Ops.Count );
                if( config.UseWhitelist ) {
                    Whitelist = new PlayerNameSet( this,WhitelistFileName );
                    Logger.Log( "Using a whitelist ({0} players): {1}",
                                Whitelist.Count, Whitelist.GetCopy().JoinToString( ", " ) );
                }

                // load or create map
                if( File.Exists( MapFileName ) ) {
                    Map = LvlMapConverter.Load( MapFileName );
                    Logger.Log( "Loaded map from {0}", MapFileName );
                } else {
                    Logger.Log( "Generating the map..." );
                    Map = NotchyMapGenerator.Generate( 256, 256, 64 );
                    Map.Save( MapFileName );
                }
                Map.IsActive = true;
                ConsolePlayer.Map = Map;

                // start listening for incoming connections
                listener = new TcpListener( config.IP, config.Port );
                listener.Start();

                // start the scheduler thread
                Thread schedulerThread = new Thread( SchedulerLoop ) {IsBackground = true};
                schedulerThread.Start();

                // listen for console input
                while( true ) {
                    string input = Console.ReadLine();
                    if( input == null ) {
                        Shutdown();
                        return;
                    }
                    try {
                        ConsolePlayer.ProcessMessage( input.Trim() );
                    } catch( Exception ex ) {
                        Logger.LogError( "Could not process message: {0}", ex );
                    }
                }

#if !DEBUG
            } catch( Exception ex ) {
                Logger.LogError( "Server crashed: {0}", ex );
            }
#endif
        }


        public void Shutdown() {
            Logger.Log( "Shutting down" );
            lock( PlayerListLock ) {
                foreach( Player player in PlayerIndex ) {
                    player.Kick( "Server shutting down" );
                }
            }
            Map.ChangedSinceSave = false;
            Map.Save( MapFileName );
            Thread.Sleep( 1000 );
        }


        #region Scheduler

        TcpListener listener;
        static readonly TimeSpan MapSaveInterval = TimeSpan.FromSeconds( 60 );
        static readonly TimeSpan PingInterval = TimeSpan.FromSeconds( 5 );
        static TimeSpan physicsInterval;


        void SchedulerLoop() {
            DateTime physicsTick = DateTime.UtcNow;
            DateTime mapTick = DateTime.UtcNow;
            DateTime pingTick = DateTime.UtcNow;
            physicsInterval = TimeSpan.FromMilliseconds( config.PhysicsTick );
            Logger.Log( "{0} is ready to go!", VersionString );

            while( true ) {
                if( listener.Pending() ) {
                    try {
                        listener.BeginAcceptTcpClient( AcceptCallback, null );
                    } catch( Exception ex ) {
                        Logger.LogWarning( "Could not accept incoming connection: {0}", ex );
                    }
                }

                if( DateTime.UtcNow.Subtract( mapTick ) > MapSaveInterval ) {
                    ThreadPool.QueueUserWorkItem( MapSaveCallback );
                    mapTick = DateTime.UtcNow;
                }

                if( DateTime.UtcNow.Subtract( pingTick ) > PingInterval ) {
                    Players.Send( null, new Packet( OpCode.Ping ) );
                    pingTick = DateTime.UtcNow;
                }

                while( DateTime.UtcNow.Subtract( physicsTick ) > physicsInterval ) {
                    Map.Tick();
                    physicsTick += physicsInterval;
                }

                Thread.Sleep( 5 );
            }
        }


        void AcceptCallback( [NotNull] IAsyncResult e ) {
            new Player( this, listener.EndAcceptTcpClient( e ) );
        }


        void MapSaveCallback( [NotNull] object unused ) {
            try {
                if( Map.ChangedSinceSave ) {
                    Map.ChangedSinceSave = false;
                    Map.Save( MapFileName );
                    Logger.Log( "Map saved to {0}", MapFileName );
                }
            } catch( Exception ex ) {
                Logger.LogError( "Failed to save map: {0}", ex );
            }
        }

        #endregion


        #region Player List

        [NotNull]
        public Player[] Players { get; private set; }

         readonly Stack<byte> FreePlayerIDs = new Stack<byte>( 127 );
         readonly List<Player> PlayerIndex = new List<Player>();
         readonly object PlayerListLock = new object();


        public bool RegisterPlayer( [NotNull] Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            lock( PlayerListLock ) {
                // Kick other sessions with same player name
                Player ghost = PlayerIndex.FirstOrDefault( p => p.Name.Equals( player.Name,
                                                                               StringComparison.OrdinalIgnoreCase ) );
                if( ghost != null ) {
                    // Wait for other session to exit/unregister
                    Logger.Log( "Kicked a duplicate connection from {0} for player {1}.",
                                ghost.IP, ghost.Name );
                    ghost.KickSynchronously( "Connected from elsewhere!" );
                }

                // check the number of connections from this IP.
                if( !player.IP.Equals( IPAddress.Loopback ) &&
                    ( !player.IsOp && config.MaxConnections > 0 || player.IsOp && config.OpMaxConnections > 0 ) ) {
                    int connections = PlayerIndex.Count( p => p.IP.Equals( player.IP ) );
                    int maxConnections = ( player.IsOp ? config.OpMaxConnections : config.MaxConnections );
                    if( connections >= config.MaxConnections ) {
                        player.Kick( "Too many connections from your IP address!" );
                        Logger.LogWarning(
                            "Player {0} was not allowed to join: connection limit of {1} reached for {2}.",
                            player.Name, maxConnections, player.IP );
                        return false;
                    }
                }

                // check if server is full
                if( PlayerIndex.Count >= config.MaxPlayers ) {
                    if( config.AdminSlot && player.IsOp ) {
                        // if player has a reserved slot, kick someone to make room
                        Player playerToKick = Players.OrderBy( p => p.LastActiveTime )
                                                     .FirstOrDefault( p => p.IsOp );
                        if( playerToKick != null ) {
                            Logger.Log( "Kicked player {0} to make room for player {1} from {2}.",
                                        playerToKick.Name, player.Name, player.IP );
                            playerToKick.KickSynchronously( "Making room for an op." );
                        } else {
                            Logger.Log( "Player {0} from {1} was not allowed to join (server is full and no one to kick).",
                                        player.Name, player.IP );
                            player.Kick( "Server is full of ops!" );
                            return false;
                        }
                    } else {
                        Logger.Log( "Player {0} from {1} was not allowed to join (server is full).",
                                    player.Name, player.IP );
                        player.Kick( "Server is full!" );
                        return false;
                    }
                }

                // Assign index and spawn player
                player.Id = FreePlayerIDs.Pop();
                if( config.RevealOps && player.IsOp ) {
                    PlayerIndex.Send( null, Packet.MakeAddEntity( player.Id, config.OpColor + player.Name, Map.Spawn ) );
                } else {
                    PlayerIndex.Send( null, Packet.MakeAddEntity( player.Id, player.Name, Map.Spawn ) );
                }
                player.HasRegistered = true;
                player.Map = Map;
                player.ChangeMap( Map );

                // Add player to index
                SpawnPlayers( player );
                PlayerIndex.Add( player );
                Logger.Log( "Player {0} connected from {1}", player.Name, player.IP );
                UpdatePlayerList();
            }
            return true;
        }


        public void SpawnPlayers( [NotNull] Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            lock( PlayerListLock ) {
                foreach( Player other in PlayerIndex ) {
                    if( other != player ) {
                        player.Send( Packet.MakeAddEntity( other.Id, other.Name, other.Position ) );
                    }
                }
            }
        }


        public void UnregisterPlayer( [NotNull] Player player ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            lock( PlayerListLock ) {
                if( !player.HasRegistered ) return;

                // Announce departure
                if( player.HasBeenAnnounced ) {
                    Players.Message( "Player {0} left the server.", player.Name );
                }

                // Despawn player entity
                Players.Send( player, Packet.MakeRemoveEntity( player.Id ) );
                FreePlayerIDs.Push( player.Id );

                // Remove player from index
                PlayerIndex.Remove( player );
                UpdatePlayerList();
            }
        }


        void UpdatePlayerList() {
            Players = PlayerIndex.ToArray();
        }


        [CanBeNull]
        public Player FindPlayerExact( [NotNull] string fullName ) {
            if( fullName == null ) throw new ArgumentNullException( "fullName" );
            return Players.FirstOrDefault( p => p.Name.Equals( fullName, StringComparison.OrdinalIgnoreCase ) );
        }


        [CanBeNull]
        public Player FindPlayer( [NotNull] Player player, [NotNull] string partialName ) {
            if( player == null ) throw new ArgumentNullException( "player" );
            if( partialName == null ) throw new ArgumentNullException( "partialName" );
            List<Player> matches = new List<Player>();
            foreach( Player otherPlayer in Players ) {
                if( otherPlayer.Name.Equals( partialName, StringComparison.OrdinalIgnoreCase ) ) {
                    return otherPlayer;
                } else if( otherPlayer.Name.StartsWith( partialName, StringComparison.OrdinalIgnoreCase ) ) {
                    matches.Add( otherPlayer );
                }
            }
            switch( matches.Count ) {
                case 0:
                    player.Message( "No players found matching \"{0}\"", partialName );
                    return null;
                case 1:
                    return matches[0];
                default:
                    player.Message( "More than one player matched \"{0}\": {1}",
                                    partialName, matches.JoinToString( ", ", p => p.Name ) );
                    return null;
            }
        }

        #endregion


        public void ChangeMap( [NotNull] Map newMap ) {
            if( newMap == null ) throw new ArgumentNullException( "newMap" );
            lock( PlayerListLock ) {
                foreach( Player player in PlayerIndex ) {
                    player.ChangeMap( newMap );
                }
                ConsolePlayer.Map = newMap;
                Map.IsActive = false;
                Map = newMap;
                Map.IsActive = true;
                Map.Save( MapFileName );
            }
        }


        static readonly Regex EmailRegex = new Regex( @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,6}$" );

        static readonly Regex AccountRegex = new Regex( @"^[a-zA-Z0-9._]{2,16}$" );


        public bool IsValidName( [NotNull] string name ) {
            return AccountRegex.IsMatch( name ) || config.AllowEmails && EmailRegex.IsMatch( name );
        }
    }
}
