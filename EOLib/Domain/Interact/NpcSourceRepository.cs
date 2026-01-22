using System.Collections.Generic;
using AutomaticTypeMapper;

namespace EOLib.Domain.Interact
{
    public enum NpcSourceType
    {
        Drop = 1,
        Shop = 2,
        Craft = 3
    }

    public class NpcDropInfo
    {
        public int ItemId { get; }
        public int MinAmount { get; }
        public int MaxAmount { get; }
        public double DropRate { get; }

        public NpcDropInfo(int itemId, int minAmount, int maxAmount, double dropRate)
        {
            ItemId = itemId;
            MinAmount = minAmount;
            MaxAmount = maxAmount;
            DropRate = dropRate;
        }
    }

    public class NpcShopItem
    {
        public int ItemId { get; }
        public int BuyPrice { get; }
        public int SellPrice { get; }

        public NpcShopItem(int itemId, int buyPrice, int sellPrice)
        {
            ItemId = itemId;
            BuyPrice = buyPrice;
            SellPrice = sellPrice;
        }
    }

    public class NpcCraftRecipe
    {
        public int ItemId { get; }
        public List<(int ItemId, int Amount)> Ingredients { get; }

        public NpcCraftRecipe(int itemId, List<(int, int)> ingredients)
        {
            ItemId = itemId;
            Ingredients = ingredients;
        }
    }

    public interface INpcSourceProvider
    {
        int NpcId { get; }
        IReadOnlyList<NpcDropInfo> Drops { get; }
        IReadOnlyList<NpcShopItem> ShopItems { get; }
        IReadOnlyList<NpcCraftRecipe> CraftRecipes { get; }
        IReadOnlyList<int> SpawnMaps { get; }
    }

    public interface INpcSourceRepository : INpcSourceProvider
    {
        new int NpcId { get; set; }
        new List<NpcDropInfo> Drops { get; }
        new List<NpcShopItem> ShopItems { get; }
        new List<NpcCraftRecipe> CraftRecipes { get; }
        new List<int> SpawnMaps { get; }

        void Reset();
    }

    [AutoMappedType(IsSingleton = true)]
    public class NpcSourceRepository : INpcSourceRepository, INpcSourceProvider
    {
        public int NpcId { get; set; }
        public List<NpcDropInfo> Drops { get; } = new List<NpcDropInfo>();
        public List<NpcShopItem> ShopItems { get; } = new List<NpcShopItem>();
        public List<NpcCraftRecipe> CraftRecipes { get; } = new List<NpcCraftRecipe>();
        public List<int> SpawnMaps { get; } = new List<int>();

        IReadOnlyList<NpcDropInfo> INpcSourceProvider.Drops => Drops;
        IReadOnlyList<NpcShopItem> INpcSourceProvider.ShopItems => ShopItems;
        IReadOnlyList<NpcCraftRecipe> INpcSourceProvider.CraftRecipes => CraftRecipes;
        IReadOnlyList<int> INpcSourceProvider.SpawnMaps => SpawnMaps;

        public void Reset()
        {
            NpcId = 0;
            Drops.Clear();
            ShopItems.Clear();
            CraftRecipes.Clear();
            SpawnMaps.Clear();
        }
    }
}
