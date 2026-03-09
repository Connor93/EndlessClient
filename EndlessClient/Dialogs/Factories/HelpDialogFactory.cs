using System;
using System.Collections.Generic;
using AutomaticTypeMapper;
using EndlessClient.Content;
using EndlessClient.Dialogs.Actions;
using EndlessClient.Dialogs.Services;
using EndlessClient.UI.Myra;
using EOLib.Config;
using EOLib.Graphics;
using EOLib.Localization;
using EOLib.Shared;
using XNAControls;

namespace EndlessClient.Dialogs.Factories
{
    [AutoMappedType(IsSingleton = true)]
    public class HelpDialogFactory : IHelpDialogFactory
    {
        private readonly INativeGraphicsManager _nativeGraphicsManager;
        private readonly IEODialogButtonService _dialogButtonService;
        private readonly IContentProvider _contentProvider;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IHelpActions _helpActions;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly IMyraUIManager _myraUIManager;
        private readonly IMyraFontProvider _myraFontProvider;

        public HelpDialogFactory(INativeGraphicsManager nativeGraphicsManager,
                                 IEODialogButtonService dialogButtonService,
                                 IContentProvider contentProvider,
                                 ILocalizedStringFinder localizedStringFinder,
                                 IHelpActions helpActions,
                                 IConfigurationProvider configurationProvider,
                                 IMyraUIManager myraUIManager,
                                 IMyraFontProvider myraFontProvider)
        {
            _nativeGraphicsManager = nativeGraphicsManager;
            _dialogButtonService = dialogButtonService;
            _contentProvider = contentProvider;
            _localizedStringFinder = localizedStringFinder;
            _helpActions = helpActions;
            _configurationProvider = configurationProvider;
            _myraUIManager = myraUIManager;
            _myraFontProvider = myraFontProvider;
        }

        public IXNADialog Create()
        {
            if (_configurationProvider.UIMode != UIMode.Gfx)
            {
                var dlg = new MyraScrollingListDialog(
                    _myraUIManager,
                    _myraFontProvider,
                    _localizedStringFinder.GetString(EOResourceID.ENDLESS_HELP),
                    width: 320,
                    height: 300);

                dlg.CancelClosesDialog = true;
                dlg.SetupButtons(showOk: false, showCancel: true);

                var messages = GetMessages();
                var actions = GetActions();
                int linkIndex = 0;

                foreach (var message in messages)
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        dlg.AddItem(" ");
                        continue;
                    }

                    var isLink = message.Length > 0 && message[0] == '*';
                    var text = isLink ? message[1..] : message;

                    if (isLink && linkIndex < actions.Count)
                    {
                        var action = actions[linkIndex++];
                        dlg.AddItem(text, isLink: true, onClick: _ => action());
                    }
                    else
                    {
                        dlg.AddItem(text);
                    }
                }

                return dlg;
            }

            var xnaDlg = new ScrollingListDialog(_nativeGraphicsManager, _dialogButtonService, DialogType.Help)
            {
                Title = _localizedStringFinder.GetString(EOResourceID.ENDLESS_HELP),
                Buttons = ScrollingListDialogButtons.Cancel,
                ListItemType = ListDialogItem.ListItemStyle.Small,
            };

            xnaDlg.AddTextAsListItems(_contentProvider.Fonts[Constants.FontSize08pt5],
                insertLineBreaks: false,
                linkClickActions: GetActions(),
                messages: GetMessages());

            return xnaDlg;
        }

        private string[] GetMessages()
        {
            return new[]
            {
                _localizedStringFinder.GetString(EOResourceID.ENDLESS_HELP_SUMMARY_1),
                string.Empty,
                _localizedStringFinder.GetString(EOResourceID.ENDLESS_HELP_SUMMARY_2),
                string.Empty,
                _localizedStringFinder.GetString(EOResourceID.ENDLESS_HELP_LINK_RESET_PASSWORD),
                _localizedStringFinder.GetString(EOResourceID.ENDLESS_HELP_LINK_REPORT_SOMEONE),
                _localizedStringFinder.GetString(EOResourceID.ENDLESS_HELP_LINK_SPEAK_TO_ADMIN),
            };
        }

        private List<Action> GetActions()
        {
            return new List<Action>
            {
                _helpActions.ResetPassword,
                _helpActions.ReportSomeone,
                _helpActions.SpeakToAdmin,
            };
        }
    }

    public interface IHelpDialogFactory
    {
        IXNADialog Create();
    }
}
