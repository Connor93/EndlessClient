using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib.Domain.Character;

namespace EOLib.Domain.Map
{
    public enum LockerContext
    {
        Default,
        GuildStorage,
        DeliveryInbox,
    }

    public interface ILockerDataRepository : IResettable
    {
        MapCoordinate Location { get; set; }

        HashSet<InventoryItem> Items { get; set; }

        LockerContext Context { get; set; }

        bool SuppressDialog { get; set; }
    }

    public interface ILockerDataProvider : IResettable
    {
        MapCoordinate Location { get; }

        IReadOnlyCollection<InventoryItem> Items { get; }

        LockerContext Context { get; }

        bool SuppressDialog { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class LockerDataRepository : ILockerDataProvider, ILockerDataRepository
    {
        public MapCoordinate Location { get; set; }

        public HashSet<InventoryItem> Items { get; set; }

        public LockerContext Context { get; set; }

        public bool SuppressDialog { get; set; }

        IReadOnlyCollection<InventoryItem> ILockerDataProvider.Items => Items;

        public LockerDataRepository()
        {
            ResetState();
        }

        public void ResetState()
        {
            Location = MapCoordinate.Zero;
            Items = new HashSet<InventoryItem>();
            Context = LockerContext.Default;
            SuppressDialog = false;
        }
    }
}
