using System.Collections.Generic;
using AutomaticTypeMapper;
using Optional;

namespace EndlessClient.HUD.Macros
{
    public interface IMacroSlotDataRepository
    {
        /// <summary>
        /// Array of macro slots by their position (0-15 for F1-F8 and Shift+F1-F8).
        /// </summary>
        Option<MacroSlot>[] MacroSlots { get; set; }

        /// <summary>
        /// Currently selected macro slot index (if any).
        /// </summary>
        Option<int> SelectedMacroSlot { get; set; }
    }

    public interface IMacroSlotDataProvider
    {
        /// <summary>
        /// Read-only access to macro slots.
        /// </summary>
        IReadOnlyList<Option<MacroSlot>> MacroSlots { get; }

        /// <summary>
        /// Currently selected macro slot index (if any).
        /// </summary>
        Option<int> SelectedMacroSlot { get; }

        /// <summary>
        /// Gets the macro slot info for the selected slot.
        /// </summary>
        Option<MacroSlot> SelectedMacroInfo { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class MacroSlotDataRepository : IMacroSlotDataRepository, IMacroSlotDataProvider
    {
        public const int MacroRows = 2;
        public const int MacroRowLength = 8;
        public const int TotalMacroSlots = MacroRows * MacroRowLength;

        public Option<MacroSlot>[] MacroSlots { get; set; }

        public Option<int> SelectedMacroSlot { get; set; }

        public Option<MacroSlot> SelectedMacroInfo =>
            SelectedMacroSlot.FlatMap(slot => MacroSlots[slot]);

        IReadOnlyList<Option<MacroSlot>> IMacroSlotDataProvider.MacroSlots => MacroSlots;

        public MacroSlotDataRepository()
        {
            MacroSlots = new Option<MacroSlot>[TotalMacroSlots];
        }
    }
}
