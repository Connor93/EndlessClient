using System;
using AutomaticTypeMapper;
using EOLib.Domain.Character;
using EOLib.Domain.Map;
using EOLib.IO.Repositories;

namespace EOLib.Domain.Item
{
    [AutoMappedType]
    public class ItemPickupValidator : IItemPickupValidator
    {
        private readonly IEIFFileProvider _eifFileProvider;

        public ItemPickupValidator(IEIFFileProvider eifFileProvider)
        {
            _eifFileProvider = eifFileProvider;
        }

        public ItemPickupResult ValidateItemPickup(Character.Character mainCharacter, MapItem item)
        {
            var xDif = Math.Abs(item.X - mainCharacter.RenderProperties.MapX);
            var yDif = Math.Abs(item.Y - mainCharacter.RenderProperties.MapY);
            if (xDif > 2 || yDif > 2)
                return ItemPickupResult.TooFar;

            var itemData = _eifFileProvider.EIFFile[item.ItemID];
            var totalWeight = itemData.Weight * item.Amount;
            if (totalWeight + mainCharacter.Stats[CharacterStat.Weight] > mainCharacter.Stats[CharacterStat.MaxWeight])
                return ItemPickupResult.TooHeavy;

            return ItemPickupResult.Ok;
        }
    }

    public interface IItemPickupValidator
    {
        ItemPickupResult ValidateItemPickup(Character.Character mainCharacter, MapItem item);
    }
}
