using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.Net.Packets
{
    /// <summary>
    /// Client packet to request NPC source information
    /// Uses NPC family with action 20 (custom, not in SDK)
    /// </summary>
    public class NpcSourceRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Npc;
        public PacketAction Action => (PacketAction)20;

        public int NpcId { get; set; }

        public void Serialize(EoWriter writer)
        {
            writer.AddShort(NpcId);
        }

        public void Deserialize(EoReader reader) { }
    }
}
