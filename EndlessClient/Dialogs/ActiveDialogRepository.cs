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
        Option<IXNADialog> FriendIgnoreDialog { get; }

        Option<IXNADialog> SessionExpDialog { get; }

        Option<IXNADialog> QuestStatusDialog { get; }

        Option<IXNADialog> PaperdollDialog { get; }

        Option<IXNADialog> BookDialog { get; }

        Option<IXNADialog> ShopDialog { get; }

        Option<IXNADialog> QuestDialog { get; }

        Option<IXNADialog> ChestDialog { get; }

        Option<IXNADialog> LockerDialog { get; }

        Option<IXNADialog> BankAccountDialog { get; }

        Option<IXNADialog> SkillmasterDialog { get; }

        Option<IXNADialog> BardDialog { get; }

        Option<ScrollingListDialog> MessageDialog { get; }

        Option<IXNADialog> TradeDialog { get; }

        Option<EOMessageBox> MessageBox { get; }

        Option<IXNADialog> BoardDialog { get; }

        Option<IXNADialog> JukeboxDialog { get; }

        Option<IXNADialog> InnkeeperDialog { get; }

        Option<IXNADialog> LawDialog { get; }

        Option<IBarberDialog> BarberDialog { get; }

        Option<IXNADialog> HelpDialog { get; }

        Option<IXNADialog> ItemInfoDialog { get; }

        Option<IXNADialog> NpcInfoDialog { get; }

        Option<ISearchResultsDialog> SearchResultsDialog { get; }

        Option<IniEditorDialog> IniEditorDialog { get; }

        IReadOnlyList<Option<IXNADialog>> ActiveDialogs { get; }
    }

    public interface IActiveDialogRepository : IDisposable
    {
        Option<IXNADialog> FriendIgnoreDialog { get; set; }

        Option<IXNADialog> SessionExpDialog { get; set; }

        Option<IXNADialog> QuestStatusDialog { get; set; }

        Option<IXNADialog> PaperdollDialog { get; set; }

        Option<IXNADialog> BookDialog { get; set; }

        Option<IXNADialog> ShopDialog { get; set; }

        Option<IXNADialog> QuestDialog { get; set; }

        Option<IXNADialog> ChestDialog { get; set; }

        Option<IXNADialog> LockerDialog { get; set; }

        Option<IXNADialog> BankAccountDialog { get; set; }

        Option<IXNADialog> SkillmasterDialog { get; set; }

        Option<IXNADialog> BardDialog { get; set; }

        Option<ScrollingListDialog> MessageDialog { get; set; }

        Option<IXNADialog> TradeDialog { get; set; }

        Option<EOMessageBox> MessageBox { get; set; }

        Option<IXNADialog> BoardDialog { get; set; }

        Option<IXNADialog> JukeboxDialog { get; set; }

        Option<IXNADialog> InnkeeperDialog { get; set; }

        Option<IXNADialog> LawDialog { get; set; }

        Option<IBarberDialog> BarberDialog { get; set; }

        Option<IXNADialog> GuildDialog { get; set; }

        Option<IXNADialog> HelpDialog { get; set; }

        Option<IXNADialog> ItemInfoDialog { get; set; }

        Option<IXNADialog> NpcInfoDialog { get; set; }

        Option<ISearchResultsDialog> SearchResultsDialog { get; set; }

        Option<IniEditorDialog> IniEditorDialog { get; set; }

        IReadOnlyList<Option<IXNADialog>> ActiveDialogs { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class ActiveDialogRepository : IActiveDialogRepository, IActiveDialogProvider
    {
        public Option<IXNADialog> FriendIgnoreDialog { get; set; }

        public Option<IXNADialog> SessionExpDialog { get; set; }

        public Option<IXNADialog> QuestStatusDialog { get; set; }

        public Option<IXNADialog> PaperdollDialog { get; set; }

        public Option<IXNADialog> BookDialog { get; set; }

        public Option<IXNADialog> ShopDialog { get; set; }

        public Option<IXNADialog> QuestDialog { get; set; }

        public Option<IXNADialog> ChestDialog { get; set; }

        public Option<IXNADialog> LockerDialog { get; set; }

        public Option<IXNADialog> BankAccountDialog { get; set; }

        public Option<IXNADialog> SkillmasterDialog { get; set; }

        public Option<IXNADialog> BardDialog { get; set; }

        public Option<ScrollingListDialog> MessageDialog { get; set; }

        public Option<IXNADialog> TradeDialog { get; set; }

        public Option<EOMessageBox> MessageBox { get; set; }

        public Option<IXNADialog> BoardDialog { get; set; }

        public Option<IXNADialog> JukeboxDialog { get; set; }

        public Option<IXNADialog> InnkeeperDialog { get; set; }

        public Option<IXNADialog> LawDialog { get; set; }

        public Option<IBarberDialog> BarberDialog { get; set; }

        public Option<IXNADialog> GuildDialog { get; set; }

        public Option<IXNADialog> HelpDialog { get; set; }

        public Option<IXNADialog> ItemInfoDialog { get; set; }

        public Option<IXNADialog> NpcInfoDialog { get; set; }

        public Option<ISearchResultsDialog> SearchResultsDialog { get; set; }

        public Option<IniEditorDialog> IniEditorDialog { get; set; }

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
                    SearchResultsDialog.Map(Map),
                    IniEditorDialog.Map(Map),
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

            FriendIgnoreDialog = Option.None<IXNADialog>();
            SessionExpDialog = Option.None<IXNADialog>();
            QuestStatusDialog = Option.None<IXNADialog>();
            PaperdollDialog = Option.None<IXNADialog>();
            BookDialog = Option.None<IXNADialog>();
            ShopDialog = Option.None<IXNADialog>();
            QuestDialog = Option.None<IXNADialog>();
            ChestDialog = Option.None<IXNADialog>();
            LockerDialog = Option.None<IXNADialog>();
            BankAccountDialog = Option.None<IXNADialog>();
            SkillmasterDialog = Option.None<IXNADialog>();
            BardDialog = Option.None<IXNADialog>();
            MessageDialog = Option.None<ScrollingListDialog>();
            TradeDialog = Option.None<IXNADialog>();
            MessageBox = Option.None<EOMessageBox>();
            BoardDialog = Option.None<IXNADialog>();
            JukeboxDialog = Option.None<IXNADialog>();
            InnkeeperDialog = Option.None<IXNADialog>();
            LawDialog = Option.None<IXNADialog>();
            BarberDialog = Option.None<IBarberDialog>();
            GuildDialog = Option.None<IXNADialog>();
            HelpDialog = Option.None<IXNADialog>();
            ItemInfoDialog = Option.None<IXNADialog>();
            NpcInfoDialog = Option.None<IXNADialog>();
            SearchResultsDialog = Option.None<ISearchResultsDialog>();
            IniEditorDialog = Option.None<IniEditorDialog>();
        }
    }
}
