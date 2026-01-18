using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.Net.Packets
{
    /// <summary>
    /// Client packet to request item source information (drops, shops, crafts)
    /// Custom packet type: ITEM_SPEC (family=14, action=16)
    /// </summary>
    public class ItemSpecClientPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Item;
        public PacketAction Action => PacketAction.Spec;

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
