// Part of FemtoCraft | Copyright 2012-2013 Matvei Stefarov <me@matvei.org> | See LICENSE.txt
using System;
using System.IO;
using System.Net;
using System.Text;
using JetBrains.Annotations;
using Testosterone.Packets;

namespace Testosterone {
    public sealed class PacketWriter : BinaryWriter {
        public PacketTransferMode TransferMode { get; set; }


        public PacketWriter( [NotNull] Stream stream ) :
            base( stream ) {}


        public void Write( OpCode value ) {
            Write( (byte)value );
        }


        public override void Write( short value ) {
            base.Write( IPAddress.HostToNetworkOrder( value ) );
        }


        public override void Write( string value ) {
            if( value == null ) throw new ArgumentNullException( "value" );
            Write( Encoding.ASCII.GetBytes( value.PadRight( 64 ).Substring( 0, 64 ) ) );
        }


        IPacketDescriptor descriptor;
        bool inPacket;
        byte[] buffer;

        public IPacket BeginPacket(OpCode opCode) {
            if (inPacket) {
                throw new InvalidOperationException("Already writing a packet (" + descriptor + ")");
            }
            inPacket = true;
            descriptor = PacketManager.GetDescriptor(opCode);
            return descriptor.Create();
        }

        public void EndPacket() {
            inPacket = false;
        }
    }


    public enum PacketTransferMode {
        PacketAtATime,
        FieldAtATime,
        ByteAtATime
    }
}