using System.Collections.Generic;
using EOLib.IO;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace EOLib.Domain.Extensions
{
    public static class PaperdollExtensions
    {
        public static IReadOnlyDictionary<EquipLocation, int> GetPaperdoll(this EquipmentWelcome equipment)
        {
            // Note: The SDK's EquipmentWelcome deserializes bytes into properties with a different
            // ordering than EquipLocation. The server sends data in EquipLocation order:
            // Boots(0), Accessory(1), Gloves(2), Belt(3), Armor(4)...
            // But SDK's EquipmentWelcome deserializes into:
            // Boots(0), Gloves(1), Accessory(2), Armor(3), Belt(4)...
            // We must map the SDK properties to their actual EquipLocation meaning.
            return new Dictionary<EquipLocation, int>
            {
                [EquipLocation.Boots] = equipment.Boots,
                [EquipLocation.Accessory] = equipment.Gloves,     // SDK's Gloves is at byte pos 1 = Accessory
                [EquipLocation.Gloves] = equipment.Accessory,     // SDK's Accessory is at byte pos 2 = Gloves
                [EquipLocation.Belt] = equipment.Armor,           // SDK's Armor is at byte pos 3 = Belt
                [EquipLocation.Armor] = equipment.Belt,           // SDK's Belt is at byte pos 4 = Armor
                [EquipLocation.Necklace] = equipment.Necklace,
                [EquipLocation.Hat] = equipment.Hat,
                [EquipLocation.Shield] = equipment.Shield,
                [EquipLocation.Weapon] = equipment.Weapon,
                [EquipLocation.Ring1] = equipment.Ring[0],
                [EquipLocation.Ring2] = equipment.Ring[1],
                [EquipLocation.Armlet1] = equipment.Armlet[0],
                [EquipLocation.Armlet2] = equipment.Armlet[1],
                [EquipLocation.Bracer1] = equipment.Bracer[0],
                [EquipLocation.Bracer2] = equipment.Bracer[1],
            };
        }

        public static IReadOnlyDictionary<EquipLocation, int> GetPaperdoll(this EquipmentPaperdoll equipment)
        {
            return new Dictionary<EquipLocation, int>
            {
                [EquipLocation.Boots] = equipment.Boots,
                [EquipLocation.Accessory] = equipment.Accessory,
                [EquipLocation.Gloves] = equipment.Gloves,
                [EquipLocation.Belt] = equipment.Belt,
                [EquipLocation.Armor] = equipment.Armor,
                [EquipLocation.Necklace] = equipment.Necklace,
                [EquipLocation.Hat] = equipment.Hat,
                [EquipLocation.Shield] = equipment.Shield,
                [EquipLocation.Weapon] = equipment.Weapon,
                [EquipLocation.Ring1] = equipment.Ring[0],
                [EquipLocation.Ring2] = equipment.Ring[1],
                [EquipLocation.Armlet1] = equipment.Armlet[0],
                [EquipLocation.Armlet2] = equipment.Armlet[1],
                [EquipLocation.Bracer1] = equipment.Bracer[0],
                [EquipLocation.Bracer2] = equipment.Bracer[1],
            };
        }
    }
}
