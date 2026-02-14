using System;
using AutomaticTypeMapper;

namespace EOLib.Domain.Character
{
    public interface ICharacterSessionRepository : IResettable
    {
        DateTime SessionStartTime { get; set; }

        int BestKillExp { get; set; }

        int LastKillExp { get; set; }

        ulong TodayTotalExp { get; set; }

        int KillCount { get; set; }

        int StartingGold { get; set; }

        int StartingExp { get; set; }

        bool GrindSessionActive { get; set; }

        TimeSpan GrindSessionPausedElapsed { get; set; }

        DateTime GrindSessionResumeTime { get; set; }
    }

    public interface ICharacterSessionProvider : IResettable
    {
        DateTime SessionStartTime { get; }

        int BestKillExp { get; }

        int LastKillExp { get; }

        ulong TodayTotalExp { get; }

        int KillCount { get; }

        int StartingGold { get; }

        int StartingExp { get; }

        bool GrindSessionActive { get; }

        TimeSpan GrindSessionPausedElapsed { get; }

        DateTime GrindSessionResumeTime { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class CharacterSessionRepository : ICharacterSessionRepository, ICharacterSessionProvider
    {
        public DateTime SessionStartTime { get; set; }

        public int BestKillExp { get; set; }

        public int LastKillExp { get; set; }

        public ulong TodayTotalExp { get; set; }

        public int KillCount { get; set; }

        public int StartingGold { get; set; }

        public int StartingExp { get; set; }

        public bool GrindSessionActive { get; set; }

        public TimeSpan GrindSessionPausedElapsed { get; set; }

        public DateTime GrindSessionResumeTime { get; set; }

        public CharacterSessionRepository()
        {
            ResetState();
        }

        public void ResetState()
        {
            SessionStartTime = DateTime.Now;
            BestKillExp = 0;
            LastKillExp = 0;
            TodayTotalExp = 0;
            KillCount = 0;
            StartingGold = 0;
            StartingExp = 0;
            GrindSessionActive = false;
            GrindSessionPausedElapsed = TimeSpan.Zero;
            GrindSessionResumeTime = DateTime.Now;
        }
    }
}
