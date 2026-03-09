using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Guild;
using EOLib.Extensions;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraGuildDialog : MyraScrollingListDialog
    {
        private enum GuildDialogState
        {
            Initial,
            Information,
            Administration,
            Management,
            BankAccount,
            Lookup,
            ViewMembers,
            JoinGuild,
            LeaveGuild,
            RegisterGuild,
            WaitingForMembers,
            Modify,
            AssignRank,
            RemoveMember,
            Disband,
        }

        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly ICharacterProvider _characterProvider;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IGuildSessionProvider _guildSessionProvider;
        private readonly IGuildActions _guildActions;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ITextMultiInputDialogFactory _textMultiInputDialogFactory;
        private readonly IItemTransferDialogFactory _itemTransferDialogFactory;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ISfxPlayer _sfxPlayer;

        private readonly Dictionary<GuildDialogState, Action> _stateTransitions;
        private readonly Stack<GuildDialogState> _stateStack;

        private GuildDialogState _state;
        private HashSet<GuildMember> _cachedMembers;
        private HashSet<string> _cachedCreationMembers;
        private Option<GuildInfo> _cachedGuildInfo;
        private MyraListItem _modifyGuildDescriptionListItem;
        private MyraListItem _guildBalanceListItem;
        private int _lastGuildBalance;

        public MyraGuildDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            ICharacterProvider characterProvider,
            IEOMessageBoxFactory messageBoxFactory,
            IGuildSessionProvider guildSessionProvider,
            IGuildActions guildActions,
            ITextInputDialogFactory textInputDialogFactory,
            ITextMultiInputDialogFactory textMultiInputDialogFactory,
            IItemTransferDialogFactory itemTransferDialogFactory,
            ICharacterInventoryProvider characterInventoryProvider,
            IEIFFileProvider eifFileProvider,
            ISfxPlayer sfxPlayer)
            : base(uiManager, fontProvider, localizedStringFinder.GetString(EOResourceID.GUILD_GUILD_MASTER), width: 320, height: 280)
        {
            _localizedStringFinder = localizedStringFinder;
            _characterProvider = characterProvider;
            _messageBoxFactory = messageBoxFactory;
            _guildSessionProvider = guildSessionProvider;
            _guildActions = guildActions;
            _textInputDialogFactory = textInputDialogFactory;
            _textMultiInputDialogFactory = textMultiInputDialogFactory;
            _itemTransferDialogFactory = itemTransferDialogFactory;
            _characterInventoryProvider = characterInventoryProvider;
            _eifFileProvider = eifFileProvider;
            _sfxPlayer = sfxPlayer;

            _stateStack = new Stack<GuildDialogState>();
            _cachedMembers = new HashSet<GuildMember>();
            _cachedCreationMembers = new HashSet<string>();
            _cachedGuildInfo = Option.None<GuildInfo>();

            _stateTransitions = new Dictionary<GuildDialogState, Action>
            {
                { GuildDialogState.Initial, SetupInitialState },
                { GuildDialogState.Information, SetupInformationState },
                { GuildDialogState.Administration, SetupAdministrationState },
                { GuildDialogState.Management, SetupManagementState },
                { GuildDialogState.BankAccount, SetupBankAccountState },
                { GuildDialogState.JoinGuild, SetupJoinGuildState },
                { GuildDialogState.LeaveGuild, SetupLeaveGuildState },
                { GuildDialogState.RegisterGuild, SetupRegisterGuildState },
                { GuildDialogState.WaitingForMembers, SetupWaitingForMembersState },
                { GuildDialogState.Modify, SetupModifyState },
                { GuildDialogState.AssignRank, SetupAssignRankState },
                { GuildDialogState.RemoveMember, SetupRemoveMemberState },
                { GuildDialogState.Disband, SetupDisbandState },
            };

            BackAction += (_, _) => GoBack();

            SetState(GuildDialogState.Initial);
        }

        public override void Update(GameTime gameTime)
        {
            switch (_state)
            {
                case GuildDialogState.Modify:
                    if (_modifyGuildDescriptionListItem != null &&
                        _modifyGuildDescriptionListItem.PrimaryText != _guildSessionProvider.GuildDescription)
                    {
                        _modifyGuildDescriptionListItem.PrimaryText = _guildSessionProvider.GuildDescription;
                    }
                    break;

                case GuildDialogState.Information:
                    _cachedGuildInfo.Match(
                        some: cachedGuildInfo =>
                        {
                            _guildSessionProvider.GuildInfo.MatchSome(
                                some: repoGuildInfo =>
                                {
                                    if (cachedGuildInfo.Equals(repoGuildInfo))
                                        return;
                                    CacheAndSetGuildInfo(repoGuildInfo);
                                }
                            );
                        },
                        none: () => _guildSessionProvider.GuildInfo.MatchSome(CacheAndSetGuildInfo)
                    );

                    if (!_cachedMembers.SetEquals(_guildSessionProvider.GuildMembers))
                    {
                        SetState(GuildDialogState.ViewMembers, pushState: false);
                        ClearItems();

                        _cachedMembers = _guildSessionProvider.GuildMembers.ToHashSet();
                        foreach (var member in _cachedMembers)
                        {
                            AddItem($"{member.Rank}  {member.Name.Capitalize()}", subText: member.RankName.Capitalize());
                        }
                    }
                    break;

                case GuildDialogState.RegisterGuild:
                    _guildSessionProvider.CreationSession.MatchSome(creationSession =>
                    {
                        if (creationSession.Approved)
                        {
                            SetState(GuildDialogState.WaitingForMembers);
                        }
                    });
                    break;

                case GuildDialogState.WaitingForMembers:
                    _guildSessionProvider.CreationSession.MatchSome(creationSession =>
                    {
                        if (!_cachedCreationMembers.SetEquals(creationSession.Members))
                        {
                            foreach (var member in creationSession.Members.Where(c => !_cachedCreationMembers.Contains(c)))
                            {
                                AddItem(member);
                            }
                            _cachedCreationMembers = creationSession.Members.ToHashSet();
                        }
                    });
                    break;

                case GuildDialogState.BankAccount:
                    if (_lastGuildBalance != _guildSessionProvider.GuildBalance)
                    {
                        if (_guildBalanceListItem != null)
                        {
                            _guildBalanceListItem.PrimaryText = $"{_localizedStringFinder.GetString(EOResourceID.GUILD_BANK_STATUS)} {_guildSessionProvider.GuildBalance}";
                            _lastGuildBalance = _guildSessionProvider.GuildBalance;
                        }
                    }
                    break;
            }

            base.Update(gameTime);

            void CacheAndSetGuildInfo(GuildInfo guildInfo)
            {
                SetState(GuildDialogState.Information, pushState: false);

                _cachedGuildInfo = Option.Some(guildInfo);
                ClearItems();

                AddItem($"{guildInfo.Name} [{guildInfo.Tag}]");
                AddItem(" ");
                AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_SIGNUP_DATE));
                AddItem(guildInfo.CreateDate.ToString("yyyy/MM/dd"));
                AddItem(" ");
                AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_GUILD_DESCRIPTION));
                AddItem(guildInfo.Description);
                AddItem(" ");
                AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_BANK_STATUS));
                AddItem(guildInfo.Wealth);
                AddItem(" ");
                AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_RANKING_SYSTEM));

                foreach (var (rank, n) in guildInfo.Ranks.Select((x, n) => (x, n)))
                    AddItem($"{n + 1}  {rank.Capitalize()}");

                AddItem(" ");
                AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_LEADERS));

                foreach (var staff in guildInfo.Staff)
                    AddItem($"{staff.Name.Capitalize()}{(staff.Rank == 0 ? " (founder)" : string.Empty)}");

                AddItem(" ");
            }
        }

        private void GoBack()
        {
            _guildActions.ClearLocalState();

            _cachedMembers.Clear();
            _cachedGuildInfo = Option.None<GuildInfo>();
            _modifyGuildDescriptionListItem = null;
            _guildBalanceListItem = null;
            _lastGuildBalance = 0;

            SetState(_stateStack.Count > 0 ? _stateStack.Pop() : GuildDialogState.Initial, pushState: false);
        }

        private void SetState(GuildDialogState newState, bool pushState = true)
        {
            ClearItems();
            if (pushState && _state != newState)
            {
                _stateStack.Push(_state);
            }

            _state = newState;

            var showBack = _state != GuildDialogState.Initial;
            SetupButtons(showOk: false, showCancel: true, showBack: showBack);

            if (_stateTransitions.ContainsKey(_state))
                _stateTransitions[_state].Invoke();
        }

        // ──── Initial ────
        private void SetupInitialState()
        {
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_INFORMATION),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_LEARN_MORE),
                onClick: _ => SetState(GuildDialogState.Information),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_ADMINISTRATION),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_JOIN_LEAVE_REGISTER),
                onClick: _ => SetState(GuildDialogState.Administration),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_MANAGEMENT),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_MODIFY_RANKING_DISBAND),
                onClick: _ => SetState(GuildDialogState.Management),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_BANK_ACCOUNT),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_DEPOSIT_TO_GUILD_ACCOUNT),
                onClick: _ => SetStateIfInGuild(GuildDialogState.BankAccount),
                isLink: true);
        }

        // ──── Information ────
        private void SetupInformationState()
        {
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_LOOK_UP),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_VIEW_DETAILS),
                onClick: _ => GuildLookup_Click(),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_MEMBERLIST),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_VIEW_MEMBERS),
                onClick: _ => ViewMembers_Click(),
                isLink: true);

            void GuildLookup_Click()
            {
                var showOnce = false;
                var dlg = _textInputDialogFactory.Create(_localizedStringFinder.GetString(EOResourceID.GUILD_TO_VIEW_INFORMATION_ABOUT_A_GUILD_ENTER_ITS_TAG), maxInputChars: 3, upperCase: true);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result != XNADialogResult.OK)
                        return;

                    if (dlg.ResponseText.Length < 2 && !showOnce)
                    {
                        var invalidGuildTag = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_TAG_TOO_SHORT);
                        invalidGuildTag.ShowDialog();
                        showOnce = true;
                    }
                    else
                    {
                        _guildActions.Lookup(dlg.ResponseText);
                    }
                };
                dlg.ShowDialog();
            }

            void ViewMembers_Click()
            {
                var showOnce = false;
                var dlg = _textInputDialogFactory.Create(_localizedStringFinder.GetString(EOResourceID.GUILD_TO_VIEW_INFORMATION_ABOUT_A_GUILD_ENTER_ITS_TAG), maxInputChars: 3, upperCase: true);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result != XNADialogResult.OK)
                        return;

                    if (dlg.ResponseText.Length < 2 && !showOnce)
                    {
                        var invalidGuildTag = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_TAG_TOO_SHORT);
                        invalidGuildTag.ShowDialog();
                        showOnce = true;
                    }
                    else
                    {
                        _guildActions.ViewMembers(dlg.ResponseText);
                    }
                };
                dlg.ShowDialog();
            }
        }

        // ──── Administration ────
        private void SetupAdministrationState()
        {
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_JOIN_GUILD),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_JOIN_AN_EXISTING_GUILD),
                onClick: _ => SetStateIfNotInGuild(GuildDialogState.JoinGuild),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_LEAVE_GUILD),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_LEAVE_YOUR_CURRENT_GUILD),
                onClick: _ => SetStateIfInGuild(GuildDialogState.LeaveGuild),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_REGISTER_GUILD),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_CREATE_YOUR_OWN_GUILD),
                onClick: _ => SetStateIfNotInGuild(GuildDialogState.RegisterGuild),
                isLink: true);
        }

        // ──── Management ────
        private void SetupManagementState()
        {
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_MODIFY_GUILD),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_CHANGE_YOUR_GUILD_DETAILS),
                onClick: _ => SetStateIfLeaderRank(GuildDialogState.Modify),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_RANKING),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_MANAGE_MEMBER_RANKINGS),
                onClick: _ => ShowManageRankingsDialog(),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_RANKING),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_ASSIGN_RANK_TO_MEMBER),
                onClick: _ => SetStateIfLeaderRank(GuildDialogState.AssignRank),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_REMOVE_MEMBER),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_REMOVE_A_MEMBER_FROM_GUILD),
                onClick: _ => SetStateIfLeaderRank(GuildDialogState.RemoveMember),
                isLink: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_DISBAND),
                subText: _localizedStringFinder.GetString(EOResourceID.GUILD_DISBAND_YOUR_GUILD),
                onClick: _ => SetStateIfLeaderRank(GuildDialogState.Disband),
                isLink: true);
        }

        // ──── BankAccount ────
        private void SetupBankAccountState()
        {
            _guildActions.GetGuildBankBalance(_characterProvider.MainCharacter.GuildTag);

            _guildBalanceListItem = AddItem(
                $"{_localizedStringFinder.GetString(EOResourceID.GUILD_BANK_STATUS)} {_guildSessionProvider.GuildBalance}",
                onClick: _ => ShowBankDepositMessageBox(),
                isLink: true);
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_BANK_DESCRIPTION_1));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_BANK_DESCRIPTION_2));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_BANK_DESCRIPTION_3));

            void ShowBankDepositMessageBox()
            {
                var hasEnoughMinimumGold = _characterInventoryProvider.ItemInventory
                    .SingleOrNone(x => x.ItemID == 1)
                    .Match(some: x => x.Amount >= 1000, none: () => false);
                if (!hasEnoughMinimumGold)
                {
                    var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_MINIMUM_DEPOSIT_IS_1000);
                    dlg.ShowDialog();
                    return;
                }

                var goldName = _eifFileProvider.EIFFile[1].Name;
                var goldInventoryItem = _characterInventoryProvider.ItemInventory.Single(x => x.ItemID == 1);
                var transferDialog = _itemTransferDialogFactory.CreateItemTransferDialog(goldName, ItemTransferType.DropItems, goldInventoryItem.Amount, EOResourceID.DIALOG_TRANSFER_DROP);
                transferDialog.DialogClosing += (_, e) =>
                {
                    if (e.Result != XNADialogResult.OK)
                        return;

                    if (transferDialog.SelectedAmount < 1000)
                    {
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_MINIMUM_DEPOSIT_IS_1000);
                        dlg.ShowDialog();
                        return;
                    }

                    _guildActions.BankDeposit(transferDialog.SelectedAmount);

                    GoBack();
                };
                transferDialog.ShowDialog();
            }
        }

        // ──── JoinGuild ────
        private void SetupJoinGuildState()
        {
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_JOIN_GUILD));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_YOU_ARE_ABOUT_TO_JOIN_A_GUILD));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_JOINING_A_GUILD_IS_FREE));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_PLEASE_CONSIDER_CAREFULLY));
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_CLICK_HERE_TO_JOIN_A_GUILD),
                onClick: _ => ShowJoinGuildMessageBox(),
                isLink: true);
        }

        // ──── LeaveGuild ────
        private void SetupLeaveGuildState()
        {
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_LEAVE_GUILD));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_YOU_ARE_ABOUT_TO_LEAVE_THE_GUILD));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_REMEMBER_THAT_AFTER_YOU_HAVE_LEFT_THE_GUILD));
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_CLICK_HERE_TO_LEAVE_YOUR_GUILD),
                onClick: _ => ShowLeaveGuildMessageBox(),
                isLink: true);
        }

        // ──── RegisterGuild ────
        private void SetupRegisterGuildState()
        {
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_REGISTER_GUILD));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_YOU_ARE_ABOUT_TO_CREATE_A_GUILD));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_YOU_NEED_TO_HAVE_AT_LEAST_TEN_MEMBERS));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_THE_GUILD_MASTER_WILL_ASK_A_FEE));
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_CLICK_HERE_TO_REGISTER_A_GUILD),
                onClick: _ => ShowRegisterGuildMessageBox(),
                isLink: true);
        }

        // ──── WaitingForMembers ────
        private void SetupWaitingForMembersState()
        {
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_PLEASE_WAIT_FOR_ALL_MEMBERS_TO_JOIN));
            AddItem(" ");
            AddItem(_characterProvider.MainCharacter.Name.Capitalize());
        }

        // ──── Modify ────
        private void SetupModifyState()
        {
            _guildActions.GetGuildDescription(_characterProvider.MainCharacter.GuildTag);

            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_CURRENT_DESCRIPTION));
            _modifyGuildDescriptionListItem = AddItem(
                string.IsNullOrEmpty(_guildSessionProvider.GuildDescription) ? " " : _guildSessionProvider.GuildDescription);
            AddItem(" ");
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_CLICK_HERE_TO_CHANGE_THE_DESCRIPTION),
                onClick: _ => ShowChangeDescriptionMessageBox(),
                isLink: true);

            void ShowChangeDescriptionMessageBox()
            {
                var dlg = _textInputDialogFactory.Create(_localizedStringFinder.GetString(EOResourceID.GUILD_WORD_DESCRIPTION), 240);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                    {
                        _guildActions.SetGuildDescription(dlg.ResponseText);
                        GoBack();
                    }
                };
                dlg.ShowDialog();
            }
        }

        // ──── ManageRankings (popup only) ────
        private void ShowManageRankingsDialog()
        {
            if (!_characterProvider.MainCharacter.InGuild)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_NOT_IN_GUILD);
                dlg.ShowDialog();
                return;
            }

            if (_characterProvider.MainCharacter.GuildRankID >= 2)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_RANK_TOO_LOW);
                dlg.ShowDialog();
                return;
            }

            _guildActions.GetGuildRanks(_characterProvider.MainCharacter.GuildTag);
        }

        // ──── AssignRank ────
        private void SetupAssignRankState()
        {
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_RANKING));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_RANK_DESCRIPTION_1));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_RANK_DESCRIPTION_2));
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_RANK_DESCRIPTION_3),
                onClick: _ => ShowAssignRankInputBox(),
                isLink: true);

            void ShowAssignRankInputBox()
            {
                var dlg = _textMultiInputDialogFactory.Create(
                    _localizedStringFinder.GetString(EOResourceID.GUILD_RANKING),
                    _localizedStringFinder.GetString(EOResourceID.GUILD_ASSIGN_RANK_TO_MEMBER),
                    TextMultiInputDialog.DialogSize.Two,
                    new TextMultiInputDialog.InputInfo(_localizedStringFinder.GetString(EOResourceID.GUILD_RANK_ASSIGN_NAME)),
                    new TextMultiInputDialog.InputInfo(
                        _localizedStringFinder.GetString(EOResourceID.GUILD_RANK_ASSIGN_RANK),
                        MaxChars: 1,
                        InputRestriction: TextMultiInputDialog.InputInfo.InputRestrict.Numeric
                    )
                );

                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                    {
                        if (dlg.Responses.Any(string.IsNullOrWhiteSpace))
                        {
                            var errorDlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.ACCOUNT_CREATE_FIELDS_STILL_EMPTY);
                            errorDlg.ShowDialog();
                            e.Cancel = true;
                            return;
                        }

                        _guildActions.AssignRank(dlg.Responses[0], int.Parse(dlg.Responses[1]));
                    }
                };
                dlg.ShowDialog();
            }
        }

        // ──── RemoveMember ────
        private void SetupRemoveMemberState()
        {
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_REMOVE_MEMBER));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_REMOVE_MEMBER_DESCRIPTION_1));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_REMOVE_MEMBER_DESCRIPTION_2));
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_REMOVE_MEMBER_DESCRIPTION_3),
                onClick: _ => ShowRemoveMemberInputBox(),
                isLink: true);

            void ShowRemoveMemberInputBox()
            {
                var removeMemberInput = _textInputDialogFactory.Create(_localizedStringFinder.GetString(EOResourceID.GUILD_WHO_DO_YOU_WANT_TO_REMOVE));
                removeMemberInput.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                    {
                        if (removeMemberInput.ResponseText.Length < 4)
                        {
                            e.Cancel = true;
                            var tooShortDlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.CHARACTER_CREATE_NAME_TOO_SHORT);
                            tooShortDlg.ShowDialog();
                            return;
                        }

                        _guildActions.KickMember(removeMemberInput.ResponseText);
                    }
                };
                removeMemberInput.ShowDialog();
            }
        }

        // ──── Disband ────
        private void SetupDisbandState()
        {
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_DISBAND));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_DISBAND_DESCRIPTION_1));
            AddItem(_localizedStringFinder.GetString(EOResourceID.GUILD_DISBAND_DESCRIPTION_2));
            AddItem(
                _localizedStringFinder.GetString(EOResourceID.GUILD_DISBAND_DESCRIPTION_3),
                onClick: _ => ShowDisbandGuildConfirmation(),
                isLink: true);

            void ShowDisbandGuildConfirmation()
            {
                var confirmDlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_PROMPT_DISBAND_GUILD, EODialogButtons.OkCancel);
                confirmDlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                    {
                        _guildActions.DisbandGuild();
                        _sfxPlayer.PlaySfx(SoundEffectID.LeaveGuild);
                    }
                };
                confirmDlg.ShowDialog();
            }
        }

        // ──── Message boxes ────
        private void ShowJoinGuildMessageBox()
        {
            if (_characterProvider.MainCharacter.InGuild)
            {
                var dlgAlreadyMember = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_ALREADY_A_MEMBER);
                dlgAlreadyMember.ShowDialog();
                return;
            }

            var dlgJoin = _textMultiInputDialogFactory.Create(
                _localizedStringFinder.GetString(DialogResourceID.GUILD_JOIN_GUILD),
                _localizedStringFinder.GetString(DialogResourceID.GUILD_JOIN_GUILD + 1),
                TextMultiInputDialog.DialogSize.Two,
                new TextMultiInputDialog.InputInfo(_localizedStringFinder.GetString(EOResourceID.GUILD_GUILD_TAG), MaxChars: 3, InputRestriction: TextMultiInputDialog.InputInfo.InputRestrict.Uppercase),
                new TextMultiInputDialog.InputInfo(_localizedStringFinder.GetString(EOResourceID.GUILD_RECRUITER), MaxChars: 12)
            );

            dlgJoin.DialogClosing += (sender, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    var guildTag = dlgJoin.Responses[0];
                    var recruiterName = dlgJoin.Responses[1];

                    if (string.IsNullOrEmpty(guildTag))
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_TAG_FIELD_EMPTY);
                        dlg.ShowDialog();
                        return;
                    }

                    if (string.IsNullOrEmpty(recruiterName))
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_RECRUITER_INPUT_MISSING);
                        dlg.ShowDialog();
                        return;
                    }

                    if (guildTag.Length == 1)
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_TAG_TOO_SHORT);
                        dlg.ShowDialog();
                        return;
                    }

                    _guildActions.RequestToJoinGuild(guildTag, recruiterName);
                }
            };

            dlgJoin.ShowDialog();
        }

        private void ShowLeaveGuildMessageBox()
        {
            if (!_characterProvider.MainCharacter.InGuild)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_NOT_IN_GUILD);
                dlg.ShowDialog();
                return;
            }

            var dlgLeave = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_PROMPT_LEAVE_GUILD, whichButtons: EODialogButtons.OkCancel);
            dlgLeave.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    _sfxPlayer.PlaySfx(SoundEffectID.LeaveGuild);
                    _guildActions.LeaveGuild();
                }
            };
            dlgLeave.ShowDialog();
        }

        private void ShowRegisterGuildMessageBox()
        {
            if (_characterProvider.MainCharacter.InGuild)
            {
                var dlgAlreadyMember = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_ALREADY_A_MEMBER);
                dlgAlreadyMember.ShowDialog();
                return;
            }

            var dlgRegister = _textMultiInputDialogFactory.Create(
                _localizedStringFinder.GetString(EOResourceID.GUILD_REGISTER_GUILD),
                _localizedStringFinder.GetString(EOResourceID.GUILD_ENTER_YOUR_GUILD_DETAILS),
                TextMultiInputDialog.DialogSize.Three,
                new TextMultiInputDialog.InputInfo(_localizedStringFinder.GetString(EOResourceID.GUILD_GUILD_TAG), MaxChars: 3, InputRestriction: TextMultiInputDialog.InputInfo.InputRestrict.Uppercase),
                new TextMultiInputDialog.InputInfo(_localizedStringFinder.GetString(EOResourceID.GUILD_GUILD_NAME), MaxChars: 24),
                new TextMultiInputDialog.InputInfo(_localizedStringFinder.GetString(EOResourceID.GUILD_WORD_DESCRIPTION), MaxChars: 240)
            );

            dlgRegister.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    var guildTag = dlgRegister.Responses[0];
                    var guildName = dlgRegister.Responses[1];
                    var guildDescription = dlgRegister.Responses[2];

                    if (!_characterInventoryProvider.ItemInventory.Any(x => x.ItemID == 1 && x.Amount >= 50_000))
                    {
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH, $" {_eifFileProvider.EIFFile[1].Name}");
                        dlg.ShowDialog();
                        return;
                    }

                    if (string.IsNullOrEmpty(guildTag))
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_TAG_FIELD_EMPTY);
                        dlg.ShowDialog();
                        return;
                    }

                    if (string.IsNullOrEmpty(guildName))
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_NAME_FIELD_EMPTY);
                        dlg.ShowDialog();
                        return;
                    }

                    if (guildTag.Length == 1)
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_TAG_TOO_SHORT);
                        dlg.ShowDialog();
                        return;
                    }

                    if (guildName.Length <= 3)
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_CREATE_NAME_TOO_SHORT);
                        dlg.ShowDialog();
                        return;
                    }

                    if (char.ToLower(guildTag[0]) != char.ToLower(guildName[0]))
                    {
                        e.Cancel = true;
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_TAG_NAME_LETTER_MUST_MATCH);
                        dlg.ShowDialog();
                        return;
                    }

                    _guildActions.RequestToCreateGuild(guildTag, guildName, guildDescription);
                }
            };

            dlgRegister.ShowDialog();
        }

        // ──── Guard helpers ────
        private void SetStateIfInGuild(GuildDialogState state)
        {
            if (!_characterProvider.MainCharacter.InGuild)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_NOT_IN_GUILD);
                dlg.ShowDialog();
                return;
            }

            SetState(state);
        }

        private void SetStateIfLeaderRank(GuildDialogState state)
        {
            if (!_characterProvider.MainCharacter.InGuild)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_NOT_IN_GUILD);
                dlg.ShowDialog();
                return;
            }

            if (_characterProvider.MainCharacter.GuildRankID >= 2)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_RANK_TOO_LOW);
                dlg.ShowDialog();
                return;
            }

            SetState(state);
        }

        private void SetStateIfNotInGuild(GuildDialogState state)
        {
            if (_characterProvider.MainCharacter.InGuild)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.GUILD_ALREADY_A_MEMBER);
                dlg.ShowDialog();
                return;
            }

            SetState(state);
        }
    }
}
