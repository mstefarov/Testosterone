using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Testosterone {
    public static class BroadcastExtensions {
        [StringFormatMethod( "message" )]
        public static void Message( [NotNull] this IEnumerable<Player> source,
                                    [NotNull] string message, [NotNull] params object[] formatArgs ) {
            Message( source, null, true, message, formatArgs );
        }


        [StringFormatMethod( "message" )]
        public static void Message( [NotNull] this IEnumerable<Player> source,
                                    [CanBeNull] Player except, bool sentToConsole,
                                    [NotNull] string message, [NotNull] params object[] formatArgs ) {
            if( source == null ) throw new ArgumentNullException( "source" );
            if( message == null ) throw new ArgumentNullException( "message" );
            if( formatArgs == null ) throw new ArgumentNullException( "formatArgs" );
            if( formatArgs.Length > 0 ) {
                message = String.Format( message, formatArgs );
            }
            Packet[] packets = new LineWrapper( "&E" + message ).ToArray();
            foreach( Player player in source ) {
                if( player == except ) continue;
                for( int i = 0; i < packets.Length; i++ ) {
                    player.Send( packets[i] );
                }
            }
            /* TODO logger
            if( except != server.ConsolePlayer && sentToConsole ) {
                Logger.Log( message );
            }
             * */
        }


        public static void Send( [NotNull] this IEnumerable<Player> source, [CanBeNull] Player except,
                                 Packet packet ) {
            if( source == null ) throw new ArgumentNullException( "source" );
            foreach( Player player in source ) {
                if( player == except ) continue;
                player.Send( packet );
            }
        } 
    }
}