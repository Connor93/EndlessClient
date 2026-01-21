using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using Optional;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public interface IActiveDialogProvider : IDisposable
    {
        Option<FriendIgnoreListDialog> FriendIgnoreDialog { get; }

        Option<SessionExpDialog> SessionExpDialog { get; }

        Option<QuestStatusDialog> QuestStatusDialog { get; }

        Option<IXNADialog> PaperdollDialog { get; }

        Option<BookDialog> BookDialog { get; }

        Option<IXNADialog> ShopDialog { get; }

        Option<IXNADialog> QuestDialog { get; }

        Option<IXNADialog> ChestDialog { get; }

        Option<IXNADialog> LockerDialog { get; }

        Option<BankAccountDialog> BankAccountDialog { get; }

        Option<SkillmasterDialog> SkillmasterDialog { get; }

        Option<BardDialog> BardDialog { get; }

        Option<ScrollingListDialog> MessageDialog { get; }

        Option<IXNADialog> TradeDialog { get; }

        Option<EOMessageBox> MessageBox { get; }

        Option<BoardDialog> BoardDialog { get; }

        Option<JukeboxDialog> JukeboxDialog { get; }

        Option<InnkeeperDialog> InnkeeperDialog { get; }

        Option<LawDialog> LawDialog { get; }

        Option<BarberDialog> BarberDialog { get; }

        Option<ScrollingListDialog> HelpDialog { get; }

        Option<ItemInfoDialog> ItemInfoDialog { get; }

        Option<NpcInfoDialog> NpcInfoDialog { get; }

        IReadOnlyList<Option<IXNADialog>> ActiveDialogs { get; }
    }

    public interface IActiveDialogRepository : IDisposable
    {
        Option<FriendIgnoreListDialog> FriendIgnoreDialog { get; set; }

        Option<SessionExpDialog> SessionExpDialog { get; set; }

        Option<QuestStatusDialog> QuestStatusDialog { get; set; }

        Option<IXNADialog> PaperdollDialog { get; set; }

        Option<BookDialog> BookDialog { get; set; }

        Option<IXNADialog> ShopDialog { get; set; }

        Option<IXNADialog> QuestDialog { get; set; }

        Option<IXNADialog> ChestDialog { get; set; }

        Option<IXNADialog> LockerDialog { get; set; }

        Option<BankAccountDialog> BankAccountDialog { get; set; }

        Option<SkillmasterDialog> SkillmasterDialog { get; set; }

        Option<BardDialog> BardDialog { get; set; }

        Option<ScrollingListDialog> MessageDialog { get; set; }

        Option<IXNADialog> TradeDialog { get; set; }

        Option<EOMessageBox> MessageBox { get; set; }

        Option<BoardDialog> BoardDialog { get; set; }

        Option<JukeboxDialog> JukeboxDialog { get; set; }

        Option<InnkeeperDialog> InnkeeperDialog { get; set; }

        Option<LawDialog> LawDialog { get; set; }

        Option<BarberDialog> BarberDialog { get; set; }

        Option<GuildDialog> GuildDialog { get; set; }

        Option<ScrollingListDialog> HelpDialog { get; set; }

        Option<ItemInfoDialog> ItemInfoDialog { get; set; }

        Option<NpcInfoDialog> NpcInfoDialog { get; set; }

        IReadOnlyList<Option<IXNADialog>> ActiveDialogs { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class ActiveDialogRepository : IActiveDialogRepository, IActiveDialogProvider
    {
        public Option<FriendIgnoreListDialog> FriendIgnoreDialog { get; set; }

        public Option<SessionExpDialog> SessionExpDialog { get; set; }

        public Option<QuestStatusDialog> QuestStatusDialog { get; set; }

        public Option<IXNADialog> PaperdollDialog { get; set; }

        public Option<BookDialog> BookDialog { get; set; }

        public Option<IXNADialog> ShopDialog { get; set; }

        public Option<IXNADialog> QuestDialog { get; set; }

        public Option<IXNADialog> ChestDialog { get; set; }

        public Option<IXNADialog> LockerDialog { get; set; }

        public Option<BankAccountDialog> BankAccountDialog { get; set; }

        public Option<SkillmasterDialog> SkillmasterDialog { get; set; }

        public Option<BardDialog> BardDialog { get; set; }

        public Option<ScrollingListDialog> MessageDialog { get; set; }

        public Option<IXNADialog> TradeDialog { get; set; }

        public Option<EOMessageBox> MessageBox { get; set; }

        public Option<BoardDialog> BoardDialog { get; set; }

        public Option<JukeboxDialog> JukeboxDialog { get; set; }

        public Option<InnkeeperDialog> InnkeeperDialog { get; set; }

        public Option<LawDialog> LawDialog { get; set; }

        public Option<BarberDialog> BarberDialog { get; set; }

        public Option<GuildDialog> GuildDialog { get; set; }

        public Option<ScrollingListDialog> HelpDialog { get; set; }

        public Option<ItemInfoDialog> ItemInfoDialog { get; set; }

        public Option<NpcInfoDialog> NpcInfoDialog { get; set; }

        IReadOnlyList<Option<IXNADialog>> ActiveDialogs
        {
            get
            {
                return new Option<IXNADialog>[]
                {
                    FriendIgnoreDialog.Map(Map),
                    SessionExpDialog.Map(Map),
                    QuestStatusDialog.Map(Map),
                    PaperdollDialog.Map(Map),
                    BookDialog.Map(Map),
                    ShopDialog.Map(Map),
                    QuestDialog.Map(Map),
                    ChestDialog.Map(Map),
                    LockerDialog.Map(Map),
                    BankAccountDialog.Map(Map),
                    SkillmasterDialog.Map(Map),
                    BardDialog.Map(Map),
                    MessageDialog.Map(Map),
                    TradeDialog.Map(Map),
                    MessageBox.Map(Map),
                    BoardDialog.Map(Map),
                    JukeboxDialog.Map(Map),
                    InnkeeperDialog.Map(Map),
                    LawDialog.Map(Map),
                    BarberDialog.Map(Map),
                    GuildDialog.Map(Map),
                    HelpDialog.Map(Map),
                    ItemInfoDialog.Map(Map),
                    NpcInfoDialog.Map(Map),
                }.ToList();

                static IXNADialog Map(object d)
                {
                    return (IXNADialog)d;
                }
            }
        }

        IReadOnlyList<Option<IXNADialog>> IActiveDialogRepository.ActiveDialogs => ActiveDialogs;

        IReadOnlyList<Option<IXNADialog>> IActiveDialogProvider.ActiveDialogs => ActiveDialogs;

        public void Dispose()
        {
            foreach (var dlg in ActiveDialogs)
                dlg.MatchSome(d => d.Dispose());

            FriendIgnoreDialog = Option.None<FriendIgnoreListDialog>();
            SessionExpDialog = Option.None<SessionExpDialog>();
            QuestStatusDialog = Option.None<QuestStatusDialog>();
            PaperdollDialog = Option.None<IXNADialog>();
            BookDialog = Option.None<BookDialog>();
            ShopDialog = Option.None<IXNADialog>();
            QuestDialog = Option.None<IXNADialog>();
            ChestDialog = Option.None<IXNADialog>();
            LockerDialog = Option.None<IXNADialog>();
            BankAccountDialog = Option.None<BankAccountDialog>();
            SkillmasterDialog = Option.None<SkillmasterDialog>();
            BardDialog = Option.None<BardDialog>();
            MessageDialog = Option.None<ScrollingListDialog>();
            TradeDialog = Option.None<IXNADialog>();
            MessageBox = Option.None<EOMessageBox>();
            BoardDialog = Option.None<BoardDialog>();
            JukeboxDialog = Option.None<JukeboxDialog>();
            InnkeeperDialog = Option.None<InnkeeperDialog>();
            LawDialog = Option.None<LawDialog>();
            BarberDialog = Option.None<BarberDialog>();
            GuildDialog = Option.None<GuildDialog>();
            HelpDialog = Option.None<ScrollingListDialog>();
            ItemInfoDialog = Option.None<ItemInfoDialog>();
            NpcInfoDialog = Option.None<NpcInfoDialog>();
        }
    }
}
