using System;
using Amadevus.RecordGenerator;
using EOLib.IO.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Optional;

namespace EOLib.Domain.Map
{
    [Record]
    public sealed partial class MapItem : IMapEntity
    {
        public int UniqueID { get; }

        public int ItemID { get; }

        public int X { get; }

        public int Y { get; }

        public int Amount { get; }

        public bool IsNPCDrop { get; }

        public Option<int> OwningPlayerID { get; }

        public Option<DateTime> DropTime { get; }

        public static MapItem None => new MapItem();

        public MapItem(int uid, int id, int x, int y, int amount)
        {
            UniqueID = uid;
            ItemID = id;
            X = x;
            Y = y;
            Amount = amount;
        }

        private MapItem() { }

        // Override Record-generated GetHashCode/Equals to use only UniqueID.
        // MapEntityCollectionHashSet uses GetHashCode() as its internal storage key.
        // Without this override, items with different metadata (DropTime, OwningPlayerID, IsNPCDrop)
        // get different hash codes even though they represent the same map item, breaking
        // coordinate-based lookups and making items visible but not clickable.
        public override int GetHashCode() => UniqueID.GetHashCode();

        public override bool Equals(object obj) => obj is MapItem other && UniqueID == other.UniqueID;

        public static MapItem FromNearby(ItemMapInfo itemMapInfo)
        {
            return new Builder
            {
                UniqueID = itemMapInfo.Uid,
                ItemID = itemMapInfo.Id,
                X = itemMapInfo.Coords.X,
                Y = itemMapInfo.Coords.Y,
                Amount = itemMapInfo.Amount,
            }.ToImmutable();
        }
    }
}
