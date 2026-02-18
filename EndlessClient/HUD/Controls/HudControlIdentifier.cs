using System;

namespace EndlessClient.HUD.Controls
{
    public enum HudControlIdentifier
    {
        CurrentUserInputTracker = int.MinValue, //this should always be first!

        MapRenderer = 0,
        StatusIcons,
        MiniMapRenderer,

        ClickDispatcher,

        HudBackground,
        LeftButtonGroupBackground,
        RightButtonGroupBackground,

        //buttons and panels
        InventoryButton,
        InventoryPanel,

        ViewMapButton,

        ActiveSpellsButton,
        ActiveSpellsPanel,

        PassiveSpellsButton,
        PassiveSpellsPanel,

        ChatButton,
        ChatPanel,

        StatsButton,
        StatsPanel,
        PaperdollButton,

        OnlineListButton,
        OnlineListPanel,

        PartyButton,
        PartyPanel,

        MacroButton,
        MacroPanel,

        SettingsButton,
        SettingsPanel,

        HelpButton,
        HelpPanel,

        NewsPanel,

        //top bar
        ExpTrackerButton,
        QuestWindowButton,
        ExpTrackerWindow,
        QuestWindow,
        QuestTrackerWindow,
        BountyTrackerButton,
        BountyTrackerWindow,
        GuildInfoButton,
        GuildInfoWindow,
        GuildPanelButton,
        GuildPanel,
        AchievementButton,
        AchievementWindow,

        HPStatusBar,
        TPStatusBar,
        SPStatusBar,
        TNLStatusBar,
        BossHealthBarHUD,

        //mid stuff
        ChatModePictureBox,
        ChatTextBox,

        FriendList,
        IgnoreList,

        //lower stuff
        StatusLabel,
        ClockLabel,
        ToastManager,

        //not displayed
        PeriodicStatUpdater,
        UserInputHandler,
        CharacterAnimator,
        NPCAnimator,
        UnknownEntitiesRequester,
        PeriodicEmoteHandler,

        PreviousUserInputTracker = Int32.MaxValue, //this should always be last!
    }
}
