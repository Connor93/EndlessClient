using System;
using System.Text;
using AutomaticTypeMapper;
using EOLib.IO;
using EOLib.IO.Pub;

namespace EOLib.Domain.Item
{
    [AutoMappedType]
    public class ItemStringService : IItemStringService
    {
        public string GetStringForMapDisplay(EIFRecord record, int amount)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must not be zero!", nameof(amount));

            return GetStringForInventoryDisplay(record, amount);
        }

        public string GetStringForInventoryDisplay(EIFRecord record, int amount)
        {
            var sb = new StringBuilder();

            // First line: name and amount
            if (record.ID == 1)
                sb.Append($"{amount} {record.Name}");
            else
                sb.Append(amount == 1 ? record.Name : $"{record.Name} x{amount}");

            // Add stat details based on item type
            switch (record.Type)
            {
                case ItemType.Weapon:
                case ItemType.Shield:
                case ItemType.Armor:
                case ItemType.Hat:
                case ItemType.Boots:
                case ItemType.Gloves:
                case ItemType.Accessory:
                case ItemType.Belt:
                case ItemType.Necklace:
                case ItemType.Ring:
                case ItemType.Armlet:
                case ItemType.Bracer:
                    AppendEquipStats(sb, record);
                    break;
                case ItemType.Heal:
                    AppendHealStats(sb, record);
                    break;
            }

            return sb.ToString();
        }

        private static void AppendEquipStats(StringBuilder sb, EIFRecord record)
        {
            if (record.MinDam > 0 || record.MaxDam > 0)
                sb.Append($"\nDmg: {record.MinDam}-{record.MaxDam}");

            if (record.Accuracy > 0)
                sb.Append($"\nAccuracy: {record.Accuracy}");

            if (record.Evade > 0)
                sb.Append($"\nEvade: {record.Evade}");

            if (record.Armor > 0)
                sb.Append($"\nArmor: {record.Armor}");

            if (record.HP > 0)
                sb.Append($"\nHP: +{record.HP}");

            if (record.TP > 0)
                sb.Append($"\nTP: +{record.TP}");

            // Stat bonuses
            if (record.Str > 0) sb.Append($"\nSTR: +{record.Str}");
            if (record.Int > 0) sb.Append($"\nINT: +{record.Int}");
            if (record.Wis > 0) sb.Append($"\nWIS: +{record.Wis}");
            if (record.Agi > 0) sb.Append($"\nAGI: +{record.Agi}");
            if (record.Con > 0) sb.Append($"\nCON: +{record.Con}");
            if (record.Cha > 0) sb.Append($"\nCHA: +{record.Cha}");

            // Elemental attributes
            if (record.Light > 0) sb.Append($"\nLight: +{record.Light}");
            if (record.Dark > 0) sb.Append($"\nDark: +{record.Dark}");
            if (record.Earth > 0) sb.Append($"\nEarth: +{record.Earth}");
            if (record.Air > 0) sb.Append($"\nAir: +{record.Air}");
            if (record.Water > 0) sb.Append($"\nWater: +{record.Water}");
            if (record.Fire > 0) sb.Append($"\nFire: +{record.Fire}");
        }

        private static void AppendHealStats(StringBuilder sb, EIFRecord record)
        {
            if (record.HP > 0)
                sb.Append($"\nHP: +{record.HP}");

            if (record.TP > 0)
                sb.Append($"\nTP: +{record.TP}");
        }
    }

    public interface IItemStringService
    {
        string GetStringForMapDisplay(EIFRecord record, int amount);

        string GetStringForInventoryDisplay(EIFRecord record, int amount);
    }
}
