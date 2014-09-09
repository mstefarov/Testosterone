using System;

namespace Testosterone.Packets {
    [Flags]
    public enum PacketDirection {
        ToServer = 1,
        ToClient = 2,
        Either = ToServer | ToClient
    }
}