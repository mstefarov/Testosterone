using System.Collections.Generic;

namespace Testosterone.Packets {
    public class PacketManager {
        readonly Dictionary<OpCode, IPacketDescriptor> PacketDescriptors
            = new Dictionary<OpCode, IPacketDescriptor>();


        public void Register(IPacketDescriptor newDescriptor) {
            IPacketDescriptor oldDescriptor;
            if (PacketDescriptors.TryGetValue(newDescriptor.OpCode, out oldDescriptor)) {
                Logger.LogWarning("Redefining opcode {0} from {1} to {2}",
                                  newDescriptor.OpCode, oldDescriptor, newDescriptor);
            }
            PacketDescriptors[newDescriptor.OpCode] = newDescriptor;
        }


        public IPacketDescriptor GetDescriptor(OpCode opCode) {
            return PacketDescriptors[opCode];
        }
    }
}