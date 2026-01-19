using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib.Domain.Interact;
using EOLib.Domain.Login;
using EOLib.Net.Handlers;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.PacketHandlers.Npcs
{
    /// <summary>
    /// Server packet handler for NPC source info response
    /// Custom packet type: NPC family with action 20 (not in SDK)
    /// </summary>
    [AutoMappedType]
    public class NpcSourceHandler : InGameOnlyPacketHandler<NpcSourceResponsePacket>
    {
        private readonly INpcSourceRepository _npcSourceRepository;

        public override PacketFamily Family => PacketFamily.Npc;
        public override PacketAction Action => (PacketAction)20;

        public NpcSourceHandler(IPlayerInfoProvider playerInfoProvider,
                                INpcSourceRepository npcSourceRepository)
            : base(playerInfoProvider)
        {
            _npcSourceRepository = npcSourceRepository;
        }

        public override bool HandlePacket(NpcSourceResponsePacket packet)
        {
            _npcSourceRepository.Reset();
            _npcSourceRepository.NpcId = packet.NpcId;

            foreach (var drop in packet.Drops)
                _npcSourceRepository.Drops.Add(drop);

            foreach (var item in packet.ShopItems)
                _npcSourceRepository.ShopItems.Add(item);

            foreach (var craft in packet.CraftRecipes)
                _npcSourceRepository.CraftRecipes.Add(craft);

            foreach (var mapId in packet.SpawnMaps)
                _npcSourceRepository.SpawnMaps.Add(mapId);

            return true;
        }
    }

    /// <summary>
    /// Custom server packet for NPC source data
    /// </summary>
    public class NpcSourceResponsePacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Npc;
        public PacketAction Action => (PacketAction)20;

        public int NpcId { get; private set; }
        public List<NpcDropInfo> Drops { get; } = new List<NpcDropInfo>();
        public List<NpcShopItem> ShopItems { get; } = new List<NpcShopItem>();
        public List<NpcCraftRecipe> CraftRecipes { get; } = new List<NpcCraftRecipe>();
        public List<int> SpawnMaps { get; } = new List<int>();

        public void Serialize(EoWriter writer) { }

        public void Deserialize(EoReader reader)
        {
            NpcId = reader.GetShort();

            // Read drops
            var numDrops = reader.GetChar();
            for (int i = 0; i < numDrops; i++)
            {
                var itemId = reader.GetShort();
                var minAmt = reader.GetShort();
                var maxAmt = reader.GetShort();
                var dropRate = reader.GetShort() / 100.0;
                Drops.Add(new NpcDropInfo(itemId, minAmt, maxAmt, dropRate));
            }

            // Read shop items
            var numShopItems = reader.GetChar();
            for (int i = 0; i < numShopItems; i++)
            {
                var itemId = reader.GetShort();
                var buyPrice = reader.GetInt();
                var sellPrice = reader.GetInt();
                ShopItems.Add(new NpcShopItem(itemId, buyPrice, sellPrice));
            }

            // Read craft recipes
            var numCrafts = reader.GetChar();
            for (int i = 0; i < numCrafts; i++)
            {
                var itemId = reader.GetShort();
                var numIngredients = reader.GetChar();
                var ingredients = new List<(int, int)>();
                for (int j = 0; j < numIngredients; j++)
                {
                    var ingId = reader.GetShort();
                    var ingAmount = reader.GetChar();
                    ingredients.Add((ingId, ingAmount));
                }
                CraftRecipes.Add(new NpcCraftRecipe(itemId, ingredients));
            }

            // Read spawn maps
            var numSpawns = reader.GetChar();
            for (int i = 0; i < numSpawns; i++)
            {
                SpawnMaps.Add(reader.GetShort());
            }
        }
    }
}
