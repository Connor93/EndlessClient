using AutomaticTypeMapper;

namespace EOLib.Domain.Interact.Quest
{
    public interface IStatusLabelNotifier
    {
        void ShowWarning(string message);

        void NotifyGuildBounty(string playerName, string bountyName, int guildPoints);
    }

    [AutoMappedType]
    public class NoOpStatusLabelNotifier : IStatusLabelNotifier
    {
        public void ShowWarning(string message) { }
        public void NotifyGuildBounty(string playerName, string bountyName, int guildPoints) { }
    }
}
