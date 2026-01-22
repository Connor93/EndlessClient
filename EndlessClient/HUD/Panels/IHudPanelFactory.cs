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

        OnlineListPanel CreateOnlineListPanel();

        PartyPanel CreatePartyPanel();

        DraggableHudPanel CreateSettingsPanel();

        MacroPanel CreateMacroPanel();

        HelpPanel CreateHelpPanel();
    }
}
