namespace EndlessClient.HUD.Macros
{
    public enum MacroSlotType
    {
        Empty,
        Spell,
        Item
    }

    public record MacroSlot(MacroSlotType Type, int Id)
    {
        public static MacroSlot Empty => new MacroSlot(MacroSlotType.Empty, 0);

        public static MacroSlot ForSpell(int spellId) => new MacroSlot(MacroSlotType.Spell, spellId);

        public static MacroSlot ForItem(int itemId) => new MacroSlot(MacroSlotType.Item, itemId);
    }
}
