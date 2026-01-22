namespace EndlessClient.HUD.Panels
{
    public interface IHudPanelFactory
    {
        NewsPanel CreateNewsPanel();

        InventoryPanel CreateInventoryPanel();

        ActiveSpellsPanel CreateActiveSpellsPanel();

        PassiveSpellsPanel CreatePassiveSpellsPanel();

        DraggableHudPanel CreateChatPanel();

        DraggableHudPanel CreateStatsPanel();

        DraggableHudPanel CreateOnlineListPanel();

        DraggableHudPanel CreatePartyPanel();

        DraggableHudPanel CreateSettingsPanel();

        MacroPanel CreateMacroPanel();

        HelpPanel CreateHelpPanel();
    }
}
