namespace EndlessClient.HUD.Panels
{
    public interface IHudPanelFactory
    {
        NewsPanel CreateNewsPanel();

        InventoryPanel CreateInventoryPanel();

        ActiveSpellsPanel CreateActiveSpellsPanel();

        PassiveSpellsPanel CreatePassiveSpellsPanel();

        ChatPanel CreateChatPanel();

        DraggableHudPanel CreateStatsPanel();

        OnlineListPanel CreateOnlineListPanel();

        PartyPanel CreatePartyPanel();

        SettingsPanel CreateSettingsPanel();

        MacroPanel CreateMacroPanel();

        HelpPanel CreateHelpPanel();
    }
}
