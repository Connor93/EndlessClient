namespace EndlessClient.HUD.Windows
{
    /// <summary>
    /// Shared interface for the EXP tracker window. Start/pause/reset grind session controls.
    /// </summary>
    public interface IExpTrackerWindow
    {
        void Toggle();
    }

    /// <summary>
    /// Shared interface for the guild info window. Shows guild stats at a glance.
    /// </summary>
    public interface IGuildInfoWindow
    {
        void Toggle();
    }

    /// <summary>
    /// Shared interface for the achievement window. Filter tabs, progress, badges, leaderboard.
    /// </summary>
    public interface IAchievementWindow
    {
        void Toggle();
    }

    /// <summary>
    /// Shared interface for the guild management panel. Tabs for overview, bounties, perks, buffs.
    /// </summary>
    public interface IGuildPanel
    {
        void Toggle();
    }
}
