namespace Testosterone.Packets {
    public interface IPacketDescriptor {
        string Name { get; }
        OpCode OpCode { get; }
        int Size { get; }
        PacketDirection Direction { get; }
        IPacket Create();
        IPacket Read(Player player, PacketReader reader);
    }
}