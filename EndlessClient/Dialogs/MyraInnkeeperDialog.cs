using System;
using System.Collections.Generic;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Interact.Citizen;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraInnkeeperDialog : MyraScrollingListDialog
    {
        private enum InnkeeperDialogState
        {
            Initial,
            Registration,
            SignUp,
            Unsubscribe
        }

        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ICitizenActions _citizenActions;
        private readonly ICitizenDataProvider _citizenDataProvider;
        private readonly IENFFileProvider _enfFileProvider;

        private InnkeeperDialogState _state;
        private int _lastVendorId;

        public MyraInnkeeperDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            IEOMessageBoxFactory messageBoxFactory,
            ITextInputDialogFactory textInputDialogFactory,
            ICitizenActions citizenActions,
            ICitizenDataProvider citizenDataProvider,
            IENFFileProvider enfFileProvider)
            : base(uiManager, fontProvider, string.Empty, width: 320, height: 280)
        {
            _localizedStringFinder = localizedStringFinder;
            _messageBoxFactory = messageBoxFactory;
            _textInputDialogFactory = textInputDialogFactory;
            _citizenActions = citizenActions;
            _citizenDataProvider = citizenDataProvider;
            _enfFileProvider = enfFileProvider;

            CancelClosesDialog = false;
            BackAction += (_, _) =>
            {
                if (_state == InnkeeperDialogState.SignUp || _state == InnkeeperDialogState.Unsubscribe)
                    SetState(InnkeeperDialogState.Registration);
                else if (_state == InnkeeperDialogState.Registration)
                    SetState(InnkeeperDialogState.Initial);
            };

            CancelAction += (_, _) =>
            {
                Close(XNADialogResult.Cancel);
            };

            SetState(InnkeeperDialogState.Initial);
        }

        public override void Update(GameTime gameTime)
        {
            if (_citizenDataProvider.BehaviorID.Map(x => x != _lastVendorId).ValueOr(false))
            {
                _lastVendorId = _citizenDataProvider.BehaviorID.ValueOr(0);
                _enfFileProvider.ENFFile.SingleOrNone(x => x.Type == NPCType.Inn && x.VendorID == _lastVendorId)
                    .MatchSome(innkeeperData => SetTitle(innkeeperData.Name));
            }

            base.Update(gameTime);
        }

        private void SetState(InnkeeperDialogState state)
        {
            if (state != InnkeeperDialogState.Initial && _state == state)
                return;

            _state = state;
            ClearItems();

            switch (_state)
            {
                case InnkeeperDialogState.Initial:
                    SetupButtons(showOk: false, showCancel: true, showBack: false);

                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.INN_REGISTRATION_SERVICE),
                        subText: _localizedStringFinder.GetString(EOResourceID.INN_CITIZEN_REGISTRATION_SERVICE),
                        isLink: true,
                        onClick: _ => SetState(InnkeeperDialogState.Registration));

                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.INN_SLEEP),
                        subText: _localizedStringFinder.GetString(EOResourceID.INN_FULL_HP_RECOVERY),
                        isLink: true,
                        onClick: _ => _citizenActions.RequestSleep());
                    break;

                case InnkeeperDialogState.Registration:
                    SetupButtons(showOk: false, showCancel: true, showBack: true);

                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.INN_SIGN_UP),
                        subText: _localizedStringFinder.GetString(EOResourceID.INN_BECOME_A_CITIZEN),
                        isLink: true,
                        onClick: _ => SetState(InnkeeperDialogState.SignUp));

                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.INN_UNSUBSCRIBE),
                        subText: _localizedStringFinder.GetString(EOResourceID.INN_GIVE_UP_CITIZENSHIP),
                        isLink: true,
                        onClick: _ => SetState(InnkeeperDialogState.Unsubscribe));
                    break;

                case InnkeeperDialogState.SignUp:
                    SetupButtons(showOk: false, showCancel: true, showBack: true);

                    AddItem(_localizedStringFinder.GetString(EOResourceID.INN_SIGN_UP));
                    AddItem(_localizedStringFinder.GetString(EOResourceID.INN_BECOME_CITIZEN_TEXT_1));
                    AddItem(_localizedStringFinder.GetString(EOResourceID.INN_BECOME_CITIZEN_TEXT_2));
                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.INN_BECOME_CITIZEN_TEXT_LINK),
                        isLink: true,
                        onClick: _ => HandleSignUp());
                    break;

                case InnkeeperDialogState.Unsubscribe:
                    SetupButtons(showOk: false, showCancel: true, showBack: true);

                    AddItem(_localizedStringFinder.GetString(EOResourceID.INN_UNSUBSCRIBE));
                    AddItem(_localizedStringFinder.GetString(EOResourceID.INN_GIVE_UP_TEXT_1));
                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.INN_GIVE_UP_TEXT_LINK),
                        isLink: true,
                        onClick: _ =>
                        {
                            _citizenActions.Unsubscribe();
                            SetState(InnkeeperDialogState.Registration);
                        });
                    break;
            }
        }

        private void HandleSignUp()
        {
            if (_citizenDataProvider.CurrentHomeID.HasValue)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.INN_YOU_ARE_ALREADY_A_CITIZEN_OF_A_TOWN, EOResourceID.INN_REGISTRATION_SERVICE);
                dlg.ShowDialog();
            }
            else
            {
                Func<int, ITextInputDialog> createDlg = i => _textInputDialogFactory.Create($"{i + 1}. {_citizenDataProvider.Questions[i]}");

                var dlg1 = createDlg(0);
                dlg1.DialogClosing += (_, e1) =>
                {
                    if (e1.Result != XNADialogResult.OK)
                        return;

                    var dlg2 = createDlg(1);
                    dlg2.DialogClosing += (_, e2) =>
                    {
                        if (e2.Result != XNADialogResult.OK)
                            return;

                        var dlg3 = createDlg(2);
                        dlg3.DialogClosing += (_, e3) =>
                        {
                            if (e3.Result != XNADialogResult.OK)
                                return;

                            var answers = new List<string>
                            {
                                dlg1.ResponseText,
                                dlg2.ResponseText,
                                dlg3.ResponseText
                            };

                            _citizenActions.SignUp(answers);
                        };

                        dlg3.ShowDialog();
                    };

                    dlg2.ShowDialog();
                };

                dlg1.ShowDialog();
            }
        }
    }
}
