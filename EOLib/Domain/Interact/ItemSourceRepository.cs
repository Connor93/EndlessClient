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
        public int Price { get; }  // For shops
        public double DropRate { get; }  // For drops (percentage, e.g. 5.5)
        public IReadOnlyList<(int ItemId, int Amount)> Ingredients { get; }  // For crafts

        public ItemSourceInfo(ItemSourceType type, int npcId,
                              int price = 0, double dropRate = 0,
                              IReadOnlyList<(int, int)> ingredients = null)
        {
            Type = type;
            NpcId = npcId;
            Price = price;
            DropRate = dropRate;
            Ingredients = ingredients ?? new List<(int, int)>();
        }
    }

    public interface IItemSourceRepository
    {
        int ItemId { get; set; }
        List<ItemSourceInfo> Sources { get; }
        void Reset();
    }

    public interface IItemSourceProvider
    {
        int ItemId { get; }
        IReadOnlyList<ItemSourceInfo> Sources { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class ItemSourceRepository : IItemSourceRepository, IItemSourceProvider
    {
        public int ItemId { get; set; }
        public List<ItemSourceInfo> Sources { get; } = new List<ItemSourceInfo>();

        IReadOnlyList<ItemSourceInfo> IItemSourceProvider.Sources => Sources;

        public void Reset()
        {
            ItemId = 0;
            Sources.Clear();
        }
    }
}
