using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.Net.Packets
{
    /// <summary>
    /// Client packet to request item source information (drops, shops, crafts)
    /// Custom packet type: ITEM family with action 19 (unused in SDK)
    /// </summary>
    public class ItemSourceRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Item;
        public PacketAction Action => (PacketAction)19;  // Custom action not in SDK

        public int ItemId { get; set; }

        public void Serialize(EoWriter writer)
        {
            writer.AddShort(ItemId);
        }

        public void Deserialize(EoReader reader)
        {
            // Client packet - not received by client
        }
    }
}
