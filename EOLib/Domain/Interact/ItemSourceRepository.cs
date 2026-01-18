using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Interact
{
    public enum ItemSourceType
    {
        Drop = 1,
        Shop = 2,
        Craft = 3
    }

    public class ItemSourceInfo
    {
        public ItemSourceType Type { get; }
        public int NpcId { get; }
        public string NpcName { get; }
        public int Price { get; }  // For shops
        public double DropRate { get; }  // For drops (percentage)
        public IReadOnlyList<(int ItemId, string ItemName, int Amount)> Ingredients { get; }  // For crafts

        public ItemSourceInfo(ItemSourceType type, int npcId, string npcName,
                              int price = 0, double dropRate = 0,
                              IReadOnlyList<(int, string, int)> ingredients = null)
        {
            Type = type;
            NpcId = npcId;
            NpcName = npcName;
            Price = price;
            DropRate = dropRate;
            Ingredients = ingredients ?? new List<(int, string, int)>();
        }
    }

    public interface IItemSourceRepository
    {
        int ItemId { get; set; }
        string ItemName { get; set; }
        List<ItemSourceInfo> Sources { get; }
        void Reset();
    }

    public interface IItemSourceProvider
    {
        int ItemId { get; }
        string ItemName { get; }
        IReadOnlyList<ItemSourceInfo> Sources { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class ItemSourceRepository : IItemSourceRepository, IItemSourceProvider
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public List<ItemSourceInfo> Sources { get; } = new List<ItemSourceInfo>();

        IReadOnlyList<ItemSourceInfo> IItemSourceProvider.Sources => Sources;

        public void Reset()
        {
            ItemId = 0;
            ItemName = string.Empty;
            Sources.Clear();
        }
    }
}
