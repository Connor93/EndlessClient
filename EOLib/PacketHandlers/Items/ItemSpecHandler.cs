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
    /// Custom packet type: ITEM_SPEC (family=14, action=16)
    /// </summary>
    [AutoMappedType]
    public class ItemSpecHandler : InGameOnlyPacketHandler<ItemSpecServerPacket>
    {
        private readonly IItemSourceRepository _itemSourceRepository;
        private readonly IEnumerable<IItemSourceNotifier> _notifiers;

        public override PacketFamily Family => PacketFamily.Item;
        public override PacketAction Action => PacketAction.Spec;

        public ItemSpecHandler(IPlayerInfoProvider playerInfoProvider,
                               IItemSourceRepository itemSourceRepository,
                               IEnumerable<IItemSourceNotifier> notifiers)
            : base(playerInfoProvider)
        {
            _itemSourceRepository = itemSourceRepository;
            _notifiers = notifiers;
        }

        public override bool HandlePacket(ItemSpecServerPacket packet)
        {
            _itemSourceRepository.Reset();
            _itemSourceRepository.ItemId = packet.ItemId;
            _itemSourceRepository.ItemName = packet.ItemName;

            foreach (var source in packet.Sources)
            {
                _itemSourceRepository.Sources.Add(source);
            }

            foreach (var notifier in _notifiers)
            {
                notifier.NotifyItemSourceReceived();
            }

            return true;
        }
    }

    /// <summary>
    /// Custom server packet for item source data
    /// </summary>
    public class ItemSpecServerPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Item;
        public PacketAction Action => PacketAction.Spec;

        public int ItemId { get; private set; }
        public string ItemName { get; private set; }
        public List<ItemSourceInfo> Sources { get; } = new List<ItemSourceInfo>();

        public void Serialize(EoWriter writer)
        {
            // Server packet - not sent by client
        }

        public void Deserialize(EoReader reader)
        {
            ItemId = reader.GetShort();
            ItemName = reader.GetBreakString();

            var numSources = reader.GetChar();
            for (int i = 0; i < numSources; i++)
            {
                var type = (ItemSourceType)reader.GetChar();
                var npcId = reader.GetShort();
                var npcName = reader.GetBreakString();

                int price = 0;
                double dropRate = 0;
                var ingredients = new List<(int, string, int)>();

                if (type == ItemSourceType.Drop)
                {
                    dropRate = reader.GetShort() / 100.0; // Sent as percentage * 100
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
                        var ingName = reader.GetBreakString();
                        var ingAmount = reader.GetChar();
                        ingredients.Add((ingId, ingName, ingAmount));
                    }
                }

                Sources.Add(new ItemSourceInfo(type, npcId, npcName, price, dropRate, ingredients));
            }
        }
    }

    public interface IItemSourceNotifier
    {
        void NotifyItemSourceReceived();
    }
}
