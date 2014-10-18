// Part of FemtoCraft | Copyright 2012-2013 Matvei Stefarov <me@matvei.org> | See LICENSE.txt
using System;
using System.IO;
using System.Net;
using System.Text;
using JetBrains.Annotations;
using Testosterone.Packets;

namespace Testosterone {
    public sealed class PacketReader : BinaryReader {
        IPacketDescriptor packetDescriptor;
        readonly PacketManager manager;


        public PacketReader([NotNull] PacketManager manager, [NotNull] Stream stream)
            : base(stream) {
            if (manager == null) throw new ArgumentNullException("manager");
            this.manager = manager;
        }


        public OpCode ReadOpCode() {
            return (OpCode)ReadByte();
        }


        public override short ReadInt16() {
            return IPAddress.NetworkToHostOrder(base.ReadInt16());
        }


        public override int ReadInt32() {
            return IPAddress.NetworkToHostOrder(base.ReadInt32());
        }


        public override string ReadString() {
            return Encoding.ASCII.GetString(ReadBytes(64)).Trim();
        }


        public IPacket BeginPacket() {
            if (packetDescriptor != null) {
                // todo: track expectedPackets?
            }
            OpCode code = ReadOpCode();
            packetDescriptor = manager.GetDescriptor(code);
            if (packetDescriptor == null) {
                throw new ProtocolViolationException("Unrecognized packet ID: " + code);
            }
            return packetDescriptor.Create();
        }


        public void EndPacket() {
            if (packetDescriptor == null) {
                throw new InvalidOperationException("Not currently reading any packet!");
            }
            packetDescriptor = null;
        }
    }
}