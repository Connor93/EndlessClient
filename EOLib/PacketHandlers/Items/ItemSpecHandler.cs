using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib.Domain.Interact;
using EOLib.Domain.Login;
using EOLib.Net.Handlers;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.PacketHandlers.Items
{
    /// <summary>
    /// Server packet handler for item source info response
    /// Custom packet type: ITEM family with action 19 (not in SDK)
    /// </summary>
    [AutoMappedType]
    public class ItemSourceHandler : InGameOnlyPacketHandler<ItemSourceResponsePacket>
    {
        private readonly IItemSourceRepository _itemSourceRepository;

        public override PacketFamily Family => PacketFamily.Item;
        public override PacketAction Action => (PacketAction)19;

        public ItemSourceHandler(IPlayerInfoProvider playerInfoProvider,
                                 IItemSourceRepository itemSourceRepository)
            : base(playerInfoProvider)
        {
            _itemSourceRepository = itemSourceRepository;
        }

        public override bool HandlePacket(ItemSourceResponsePacket packet)
        {
            System.Console.WriteLine($"[DEBUG] ItemSourceHandler.HandlePacket: ItemId={packet.ItemId}, Sources={packet.Sources.Count}");

            _itemSourceRepository.Reset();
            _itemSourceRepository.ItemId = packet.ItemId;

            foreach (var source in packet.Sources)
            {
                _itemSourceRepository.Sources.Add(source);
            }

            System.Console.WriteLine($"[DEBUG] Repository updated: {_itemSourceRepository.Sources.Count} sources");
            return true;
        }
    }

    /// <summary>
    /// Custom server packet for item source data
    /// </summary>
    public class ItemSourceResponsePacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Item;
        public PacketAction Action => (PacketAction)19;

        public int ItemId { get; private set; }
        public List<ItemSourceInfo> Sources { get; } = new List<ItemSourceInfo>();

        public void Serialize(EoWriter writer) { }

        public void Deserialize(EoReader reader)
        {
            ItemId = reader.GetShort();
            var numSources = reader.GetChar();

            for (int i = 0; i < numSources; i++)
            {
                var type = (ItemSourceType)reader.GetChar();
                var npcId = reader.GetShort();

                int price = 0;
                double dropRate = 0;
                var ingredients = new List<(int, int)>();

                if (type == ItemSourceType.Drop)
                {
                    dropRate = reader.GetShort() / 100.0;
                }
                else if (type == ItemSourceType.Shop)
                {
                    price = reader.GetInt();
                }
                else if (type == ItemSourceType.Craft)
                {
                    var numIngredients = reader.GetChar();
                    for (int j = 0; j < numIngredients; j++)
                    {
                        var ingId = reader.GetShort();
                        var ingAmount = reader.GetChar();
                        ingredients.Add((ingId, ingAmount));
                    }
                }

                Sources.Add(new ItemSourceInfo(type, npcId, price, dropRate, ingredients));
            }
        }
    }
}
