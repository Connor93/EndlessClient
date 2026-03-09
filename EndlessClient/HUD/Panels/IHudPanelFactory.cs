namespace EndlessClient.HUD.Panels
{
    public interface IHudPanelFactory
    {
        IHudPanel CreateNewsPanel();

        InventoryPanel CreateInventoryPanel();

        ActiveSpellsPanel CreateActiveSpellsPanel();

        IHudPanel CreatePassiveSpellsPanel();

        IHudPanel CreateChatPanel();

        IHudPanel CreateStatsPanel();

        IHudPanel CreateOnlineListPanel();

        IHudPanel CreatePartyPanel();

        IHudPanel CreateSettingsPanel();

        MacroPanel CreateMacroPanel();

        IHudPanel CreateHelpPanel();
    }
}
