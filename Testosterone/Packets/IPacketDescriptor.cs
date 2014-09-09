namespace Testosterone.Packets {
    public interface IPacketDescriptor {
        OpCode OpCode { get; }
        int Size { get; }
        PacketDirection Direction { get; }
        IPacket Create();
        IPacket Read(Player player, PacketReader reader);
    }
}