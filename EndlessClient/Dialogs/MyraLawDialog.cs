using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Interact.Law;
using EOLib.Domain.Map;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraLawDialog : MyraScrollingListDialog
    {
        private enum LawDialogState
        {
            Initial,
            Registration,
            Marriage,
            Divorce,
        }

        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ILawActions _lawActions;

        private LawDialogState _state;

        public MyraLawDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            ITextInputDialogFactory textInputDialogFactory,
            ILawActions lawActions,
            ICurrentMapStateProvider currentMapStateProvider,
            IENFFileProvider enfFileProvider)
            : base(uiManager, fontProvider, string.Empty, width: 320, height: 280)
        {
            _localizedStringFinder = localizedStringFinder;
            _textInputDialogFactory = textInputDialogFactory;
            _lawActions = lawActions;

            CancelClosesDialog = false;
            BackAction += (_, _) =>
            {
                if (_state == LawDialogState.Marriage || _state == LawDialogState.Divorce)
                    SetState(LawDialogState.Registration);
                else if (_state == LawDialogState.Registration)
                    SetState(LawDialogState.Initial);
            };

            CancelAction += (_, _) =>
            {
                Close(XNADialogResult.Cancel);
            };

            currentMapStateProvider.NPCs
                .Select(x => enfFileProvider.ENFFile[x.ID])
                .SingleOrNone(x => x.Type == NPCType.Law)
                .MatchSome(x => SetTitle(x.Name));

            SetState(LawDialogState.Initial);
        }

        private void SetState(LawDialogState state)
        {
            if (state != LawDialogState.Initial && _state == state)
                return;

            _state = state;
            ClearItems();

            switch (_state)
            {
                case LawDialogState.Initial:
                    SetupButtons(showOk: false, showCancel: true, showBack: false);

                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.WEDDING_REGISTRATION_SERVICE),
                        subText: _localizedStringFinder.GetString(EOResourceID.WEDDING_REQUEST_MARRIAGE_OR_DIVORCE),
                        isLink: true,
                        onClick: _ => SetState(LawDialogState.Registration));
                    break;

                case LawDialogState.Registration:
                    SetupButtons(showOk: false, showCancel: true, showBack: true);

                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.WEDDING_MARRIAGE),
                        subText: _localizedStringFinder.GetString(EOResourceID.WEDDING_REQUEST_WEDDING_APPROVAL),
                        isLink: true,
                        onClick: _ => SetState(LawDialogState.Marriage));

                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.WEDDING_DIVORCE),
                        subText: _localizedStringFinder.GetString(EOResourceID.WEDDING_BREAK_UP),
                        isLink: true,
                        onClick: _ => SetState(LawDialogState.Divorce));
                    break;

                case LawDialogState.Marriage:
                    SetupButtons(showOk: false, showCancel: true, showBack: true);

                    AddItem(_localizedStringFinder.GetString(EOResourceID.WEDDING_MARRIAGE));
                    AddItem(_localizedStringFinder.GetString(EOResourceID.WEDDING_REQUEST_TEXT_1));
                    AddItem(_localizedStringFinder.GetString(EOResourceID.WEDDING_REQUEST_TEXT_2));
                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.WEDDING_REQUEST_TEXT_LINK),
                        isLink: true,
                        onClick: _ =>
                        {
                            var dlg = _textInputDialogFactory.Create(_localizedStringFinder.GetString(EOResourceID.WEDDING_PROMPT_ENTER_NAME_MARRY));
                            dlg.DialogClosing += (_, e) =>
                            {
                                if (e.Result != XNADialogResult.OK) return;
                                _lawActions.RequestMarriage(dlg.ResponseText);
                            };
                            dlg.ShowDialog();
                        });
                    break;

                case LawDialogState.Divorce:
                    SetupButtons(showOk: false, showCancel: true, showBack: true);

                    AddItem(_localizedStringFinder.GetString(EOResourceID.WEDDING_DIVORCE));
                    AddItem(_localizedStringFinder.GetString(EOResourceID.WEDDING_DIVORCE_TEXT_1));
                    AddItem(_localizedStringFinder.GetString(EOResourceID.WEDDING_DIVORCE_TEXT_2));
                    AddItem(
                        _localizedStringFinder.GetString(EOResourceID.WEDDING_DIVORCE_TEXT_LINK),
                        isLink: true,
                        onClick: _ =>
                        {
                            var dlg = _textInputDialogFactory.Create(_localizedStringFinder.GetString(EOResourceID.WEDDING_PROMPT_ENTER_NAME_DIVORCE));
                            dlg.DialogClosing += (_, e) =>
                            {
                                if (e.Result != XNADialogResult.OK) return;
                                _lawActions.RequestDivorce(dlg.ResponseText);
                            };
                            dlg.ShowDialog();
                        });
                    break;
            }
        }
    }
}
