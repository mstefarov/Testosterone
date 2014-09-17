// Part of FemtoCraft | Copyright 2012-2013 Matvei Stefarov <me@matvei.org> | See LICENSE.txt
using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using Testosterone.Packets;

namespace Testosterone {
    public sealed class PacketWriter {
        // Minecraft protocol uses fixed-length ASCII strings padded with spaces. Silly, I know.
        const int StringLength = 64;
        const byte ASCIISpace = 0x20;

        // PacketBuffer is used when TransferMode is PacketAtATime
        readonly MemoryStream packetBuffer = new MemoryStream();

        // Used for expanding multi-byte values, including strings
        readonly byte[] buffer = new byte[64];

        [NotNull] readonly Stream outStream;
        [CanBeNull] IPacketDescriptor descriptor;
        bool inPacket;
        PacketTransferMode transferMode;

        [NotNull]
        public Stream OutStream {
            get { return outStream; }
        }

        public PacketTransferMode TransferMode {
            get { return transferMode; }
            set {
                if (transferMode == value) return;
                if (transferMode == PacketTransferMode.PacketAtATime) {
                    FlushPacketBuffer();
                } else if (value == PacketTransferMode.PacketAtATime) {
                    ClearPacketBuffer();
                }
                transferMode = value;
            }
        }


        public PacketWriter([NotNull] Stream stream) {
            if (stream == null) throw new ArgumentNullException("stream");
            outStream = stream;
        }


        public void Write(OpCode value) {
            Write((byte)value);
        }


        public void Write(byte value) {
            if (TransferMode == PacketTransferMode.PacketAtATime) {
                packetBuffer.WriteByte(value);
            } else {
                OutStream.WriteByte(value);
            }
        }


        public void Write(byte[] byteBuffer) {
            if (byteBuffer == null) throw new ArgumentNullException("byteBuffer");
            Write(byteBuffer, 0, byteBuffer.Length);
        }


        public void Write(byte[] byteBuffer, int index, int count) {
            if (byteBuffer == null) throw new ArgumentNullException("byteBuffer");
            switch (TransferMode) {
                case PacketTransferMode.ByteAtATime:
                    foreach (byte b in byteBuffer) {
                        OutStream.WriteByte(b);
                    }
                    break;
                case PacketTransferMode.FieldAtATime:
                    OutStream.Write(byteBuffer, index, count);
                    break;
                case PacketTransferMode.PacketAtATime:
                    packetBuffer.Write(byteBuffer, index, count);
                    break;
            }
        }


        public void Write(short value) {
            buffer[0] = (byte)(value >> 8);
            buffer[1] = (byte)value;
            Write(buffer, 0, 2);
        }


        public void Write(int value) {
            buffer[0] = (byte)(value >> 24);
            buffer[1] = (byte)(value >> 16);
            buffer[2] = (byte)(value >> 8);
            buffer[3] = (byte)value;
            Write(buffer, 0, 4);
        }


        public void Write(long value) {
            buffer[0] = (byte)(value >> 56);
            buffer[1] = (byte)(value >> 48);
            buffer[2] = (byte)(value >> 40);
            buffer[3] = (byte)(value >> 32);
            buffer[4] = (byte)(value >> 24);
            buffer[5] = (byte)(value >> 16);
            buffer[6] = (byte)(value >> 8);
            buffer[7] = (byte)value;
            Write(buffer, 0, 8);
        }


        public void Write([NotNull] string value) {
            if (value == null) throw new ArgumentNullException("value");
            // If value is 64 or fewer characters long, but encodes to 65 or more bytes,
            // we're kinda screwed here. IndexOutOfRangeException will likely be thrown.
            int bytes = Encoding.ASCII.GetBytes(value, 0, Math.Min(StringLength, value.Length), buffer, 0);
            for (int i = bytes; i < StringLength; i++) {
                buffer[i] = ASCIISpace; // pad with spaces
            }
            Write(buffer, 0, buffer.Length);
        }


        public void Flush() {
            OutStream.Flush();
        }


        public IPacket BeginPacket(OpCode opCode) {
            if (inPacket) {
                throw new InvalidOperationException("Already writing a packet (" + descriptor + ")");
            }

            if (TransferMode == PacketTransferMode.PacketAtATime) {
                ClearPacketBuffer();
            }

            inPacket = true;
            descriptor = PacketManager.GetDescriptor(opCode);
            return descriptor.Create();
        }


        public void EndPacket() {
            if (!inPacket) {
                throw new InvalidOperationException("Not currently writing a packet.");
            }
            if (TransferMode == PacketTransferMode.PacketAtATime) {
                FlushPacketBuffer();
            }
            inPacket = false;
        }


        void FlushPacketBuffer() {
            OutStream.Write(packetBuffer.GetBuffer(), 0, (int)packetBuffer.Position);
        }


        void ClearPacketBuffer() {
            packetBuffer.Position = 0;
            packetBuffer.SetLength(0);
        }
    }
}
