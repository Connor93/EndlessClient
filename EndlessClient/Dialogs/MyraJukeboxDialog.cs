using System;
using EndlessClient.Audio;
using EndlessClient.Dialogs.Factories;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Jukebox;
using EOLib.Localization;
using Microsoft.Xna.Framework;
using Optional;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    public class MyraJukeboxDialog : MyraScrollingListDialog
    {
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IJukeboxActions _jukeboxActions;
        private readonly IJukeboxRepository _jukeboxRepository;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IEDFFile _songNames;

        private DateTime _openedTime;
        private Option<string> _lastRequestedName;
        private int _songIndex;

        public MyraJukeboxDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ILocalizedStringFinder localizedStringFinder,
            IDataFileProvider dataFileProvider,
            IEOMessageBoxFactory messageBoxFactory,
            IJukeboxActions jukeboxActions,
            IJukeboxRepository jukeboxRepository,
            ICharacterInventoryProvider characterInventoryProvider,
            ISfxPlayer sfxPlayer)
            : base(uiManager, fontProvider,
                   localizedStringFinder.GetString(EOResourceID.JUKEBOX_IS_READY),
                   width: 320, height: 200)
        {
            _localizedStringFinder = localizedStringFinder;
            _messageBoxFactory = messageBoxFactory;
            _jukeboxActions = jukeboxActions;
            _jukeboxRepository = jukeboxRepository;
            _characterInventoryProvider = characterInventoryProvider;
            _sfxPlayer = sfxPlayer;
            _songNames = dataFileProvider.DataFiles[DataFiles.JukeBoxSongs];

            _openedTime = DateTime.Now;

            SetupButtons(showOk: false, showCancel: true);

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.JUKEBOX_BROWSE_THROUGH_SONGS),
                subText: FormatSubtitle(_songNames.Data[_songIndex]),
                isLink: true,
                onClick: _ => ChangeSong());

            AddItem(
                _localizedStringFinder.GetString(EOResourceID.JUKEBOX_PLAY_SONG),
                subText: FormatSubtitle("25 gold"),
                isLink: true,
                onClick: _ => PlaySong());
        }

        public override void Update(GameTime gameTime)
        {
            if ((DateTime.Now - _openedTime).TotalSeconds >= 95)
            {
                _jukeboxRepository.PlayingRequestName = Option.None<string>();
                _openedTime = DateTime.Now.AddMinutes(100);
            }

            _jukeboxRepository.PlayingRequestName.Match(
                requestedName =>
                {
                    if (_lastRequestedName.Map(x => !x.Equals(requestedName)).ValueOr(true))
                    {
                        _lastRequestedName = Option.Some(requestedName);

                        var titleString = _localizedStringFinder.GetString(EOResourceID.JUKEBOX_PLAYING_REQUEST);
                        if (!string.IsNullOrWhiteSpace(requestedName))
                            titleString += $" ({requestedName})";

                        SetTitle(titleString);
                    }
                },
                () =>
                {
                    if (_lastRequestedName.HasValue)
                    {
                        _lastRequestedName = Option.None<string>();
                        SetTitle(_localizedStringFinder.GetString(EOResourceID.JUKEBOX_IS_READY));
                    }
                });

            base.Update(gameTime);
        }

        private void ChangeSong()
        {
            _songIndex = (_songIndex + 1) % _songNames.Data.Count;
            UpdateItemSubText(0, FormatSubtitle(_songNames.Data[_songIndex]));
        }

        private void PlaySong()
        {
            if (_jukeboxRepository.PlayingRequestName.HasValue)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.JUKEBOX_REQUESTED_RECENTLY);
                dlg.ShowDialog();
                return;
            }

            if (_characterInventoryProvider.ItemInventory.SingleOrNone(x => x.ItemID == 1).Map(x => x.Amount < 25).ValueOr(true))
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.WARNING_YOU_HAVE_NOT_ENOUGH, " gold");
                dlg.ShowDialog();
                return;
            }

            var confirmDlg = _messageBoxFactory.CreateMessageBox(
                $"{_localizedStringFinder.GetString(EOResourceID.JUKEBOX_REQUEST_SONG_FOR)} 25 gold?",
                _localizedStringFinder.GetString(EOResourceID.JUKEBOX_REQUEST_SONG),
                EODialogButtons.OkCancel);

            confirmDlg.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    _jukeboxActions.RequestSong(_songIndex);
                    _sfxPlayer.PlaySfx(SoundEffectID.BuySell);
                    Close(XNADialogResult.NO_BUTTON_PRESSED);
                }
            };

            confirmDlg.ShowDialog();
        }

        private string FormatSubtitle(string additionalText)
        {
            return _localizedStringFinder.GetString(EOResourceID.DIALOG_WORD_CURRENT) + " : " + additionalText;
        }
    }
}
