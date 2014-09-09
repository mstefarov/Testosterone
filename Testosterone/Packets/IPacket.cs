using System.Linq;
using System.Text;

namespace Testosterone.Packets {
    public abstract class IPacket {
        public IPacketDescriptor Descriptor { get; protected set; }
        public byte[] Data { get; protected set; }
        
        public abstract void Write(PacketWriter writer);
        public abstract void Read(PacketReader reader);
    }
}
