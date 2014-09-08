using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Testosterone.Packets {
    internal interface IPacket {
        OpCode OpCode { get; }
        byte[] Data { get; }
    }
}
