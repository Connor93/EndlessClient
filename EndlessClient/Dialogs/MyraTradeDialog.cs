using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering.Map;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Trade;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Myra-based trade dialog — dual-panel layout for player-to-player trading.
    /// Left panel = Player 1 offer, Right panel = Player 2 offer.
    /// </summary>
    public class MyraTradeDialog : MyraDialogAdapter
    {
        private readonly ITradeActions _tradeActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly ITradeProvider _tradeProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IMapItemGraphicProvider _mapItemGraphicProvider;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IMyraFontProvider _fontProvider;

        private readonly Label _leftPlayerName, _rightPlayerName;
        private readonly Label _leftPlayerStatus, _rightPlayerStatus;
        private readonly VerticalStackPanel _leftItemList, _rightItemList;

        private TradeOffer _leftOffer, _rightOffer;
        private int _recentPartnerItemChanges;
        private Stopwatch _partnerItemChangeTick;

        public MyraTradeDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ITradeActions tradeActions,
            ILocalizedStringFinder localizedStringFinder,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            IInventorySpaceValidator inventorySpaceValidator,
            ITradeProvider tradeProvider,
            ICharacterProvider characterProvider,
            IEIFFileProvider eifFileProvider,
            IMapItemGraphicProvider mapItemGraphicProvider,
            ISfxPlayer sfxPlayer)
            : base(uiManager, "Trade")
        {
            _fontProvider = fontProvider;
            _tradeActions = tradeActions;
            _localizedStringFinder = localizedStringFinder;
            _messageBoxFactory = messageBoxFactory;
            _statusLabelSetter = statusLabelSetter;
            _inventorySpaceValidator = inventorySpaceValidator;
            _tradeProvider = tradeProvider;
            _characterProvider = characterProvider;
            _eifFileProvider = eifFileProvider;
            _mapItemGraphicProvider = mapItemGraphicProvider;
            _sfxPlayer = sfxPlayer;

            _leftOffer = new TradeOffer.Builder().ToImmutable();
            _rightOffer = new TradeOffer.Builder().ToImmutable();

            Window.Width = 500;
            Window.Height = 340;
            Window.TitleFont = fontProvider.Header;

            var tradingText = _localizedStringFinder.GetString(EOResourceID.DIALOG_TRADE_WORD_TRADING);

            // --- Left panel header ---
            _leftPlayerName = new Label
            {
                Text = "",
                Font = fontProvider.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            _leftPlayerStatus = new Label
            {
                Text = tradingText,
                Font = fontProvider.Normal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            var leftHeader = new HorizontalStackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 24
            };
            leftHeader.Widgets.Add(_leftPlayerName);
            leftHeader.Widgets.Add(_leftPlayerStatus);

            // --- Right panel header ---
            _rightPlayerName = new Label
            {
                Text = "",
                Font = fontProvider.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            _rightPlayerStatus = new Label
            {
                Text = tradingText,
                Font = fontProvider.Normal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            var rightHeader = new HorizontalStackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 24
            };
            rightHeader.Widgets.Add(_rightPlayerName);
            rightHeader.Widgets.Add(_rightPlayerStatus);

            // --- Left item list ---
            _leftItemList = new VerticalStackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Top
            };
            var leftScroll = new ScrollViewer
            {
                Content = _leftItemList,
                ShowHorizontalScrollBar = false,
                ShowVerticalScrollBar = true
            };

            // --- Right item list ---
            _rightItemList = new VerticalStackPanel
            {
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Top
            };
            var rightScroll = new ScrollViewer
            {
                Content = _rightItemList,
                ShowHorizontalScrollBar = false,
                ShowVerticalScrollBar = true
            };

            // --- Left panel ---
            var leftPanel = new VerticalStackPanel
            {
                Spacing = 4,
                Padding = new Thickness(4)
            };
            leftPanel.Widgets.Add(leftHeader);
            leftPanel.Widgets.Add(leftScroll);
            leftPanel.Proportions.Add(new Proportion(ProportionType.Auto));
            leftPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            // --- Right panel ---
            var rightPanel = new VerticalStackPanel
            {
                Spacing = 4,
                Padding = new Thickness(4)
            };
            rightPanel.Widgets.Add(rightHeader);
            rightPanel.Widgets.Add(rightScroll);
            rightPanel.Proportions.Add(new Proportion(ProportionType.Auto));
            rightPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            // --- Two-column layout ---
            var columnsPanel = new HorizontalStackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            columnsPanel.Widgets.Add(leftPanel);
            columnsPanel.Widgets.Add(rightPanel);
            columnsPanel.Proportions.Add(new Proportion(ProportionType.Fill));
            columnsPanel.Proportions.Add(new Proportion(ProportionType.Fill));

            // --- Button bar ---
            var okButton = new Button
            {
                Content = new Label { Text = "OK", Font = fontProvider.Normal },
                Width = 72,
                Height = 28
            };
            okButton.Click += (_, _) => OkButtonClicked();

            var cancelButton = new Button
            {
                Content = new Label { Text = "Cancel", Font = fontProvider.Normal },
                Width = 72,
                Height = 28
            };
            cancelButton.Click += (_, _) => CancelButtonClicked();

            var buttonBar = new HorizontalStackPanel
            {
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            buttonBar.Widgets.Add(okButton);
            buttonBar.Widgets.Add(cancelButton);

            // --- Main layout ---
            var mainPanel = new VerticalStackPanel
            {
                Spacing = 4
            };
            mainPanel.Widgets.Add(columnsPanel);
            mainPanel.Widgets.Add(buttonBar);
            mainPanel.Proportions.Add(new Proportion(ProportionType.Fill));
            mainPanel.Proportions.Add(new Proportion(ProportionType.Auto));

            Window.Content = mainPanel;

            // Poll trade data each frame
            Window.BeforeRender += _ => PollTradeData();
        }

        private void PollTradeData()
        {
            if (_tradeProvider.PlayerOneOffer != null && !_tradeProvider.PlayerOneOffer.Equals(_leftOffer))
            {
                UpdateOffer(_tradeProvider.PlayerOneOffer, _leftOffer, _leftPlayerName, _leftPlayerStatus, _leftItemList, isLeft: true);
                _leftOffer = _tradeProvider.PlayerOneOffer;
            }

            if (_tradeProvider.PlayerTwoOffer != null && !_tradeProvider.PlayerTwoOffer.Equals(_rightOffer))
            {
                UpdateOffer(_tradeProvider.PlayerTwoOffer, _rightOffer, _rightPlayerName, _rightPlayerStatus, _rightItemList, isLeft: false);
                _rightOffer = _tradeProvider.PlayerTwoOffer;
            }

            // Decay partner item change counter
            if (_partnerItemChangeTick?.ElapsedMilliseconds > 1000 && _recentPartnerItemChanges > 0)
            {
                _recentPartnerItemChanges--;
                _partnerItemChangeTick = Stopwatch.StartNew();
            }
        }

        private void UpdateOffer(TradeOffer actualOffer, TradeOffer cachedOffer,
            Label playerNameLabel, Label playerStatusLabel,
            VerticalStackPanel itemList, bool isLeft)
        {
            // Update player name + item count
            if (actualOffer.PlayerName != cachedOffer.PlayerName || actualOffer.Items.Count != cachedOffer.Items.Count)
            {
                if (!string.IsNullOrEmpty(actualOffer.PlayerName))
                {
                    var name = char.ToUpper(actualOffer.PlayerName[0]) + actualOffer.PlayerName[1..];
                    playerNameLabel.Text = actualOffer.Items.Any()
                        ? $"{name} [{actualOffer.Items.Count}]"
                        : name;
                }
            }

            // Update agree status
            if (actualOffer.Agrees != cachedOffer.Agrees)
            {
                playerStatusLabel.Text = actualOffer.Agrees
                    ? _localizedStringFinder.GetString(EOResourceID.DIALOG_TRADE_WORD_AGREE)
                    : _localizedStringFinder.GetString(EOResourceID.DIALOG_TRADE_WORD_TRADING);
                playerStatusLabel.TextColor = actualOffer.Agrees
                    ? new Microsoft.Xna.Framework.Color(0, 200, 0)
                    : Microsoft.Xna.Framework.Color.White;

                if (actualOffer.Agrees)
                {
                    _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION,
                        actualOffer.PlayerID == _characterProvider.MainCharacter.ID
                            ? EOResourceID.STATUS_LABEL_TRADE_YOU_ACCEPT
                            : EOResourceID.STATUS_LABEL_TRADE_OTHER_ACCEPT);
                }
                else
                {
                    _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ACTION,
                        actualOffer.PlayerID == _characterProvider.MainCharacter.ID
                            ? EOResourceID.STATUS_LABEL_TRADE_YOU_CANCEL
                            : EOResourceID.STATUS_LABEL_TRADE_OTHER_CANCEL);
                }
            }

            // Update item list
            if (cachedOffer.Items == null || !actualOffer.Items.ToHashSet().SetEquals(cachedOffer.Items))
            {
                // Rebuild the item list entirely for simplicity
                itemList.Widgets.Clear();

                foreach (var item in actualOffer.Items)
                {
                    var itemRec = _eifFileProvider.EIFFile[item.ItemID];
                    var genderSuffix = itemRec.Type == ItemType.Armor
                        ? $" ({_localizedStringFinder.GetString(itemRec.Gender == 0 ? EOResourceID.FEMALE : EOResourceID.MALE)})"
                        : string.Empty;
                    var itemText = $"{itemRec.Name}  x{item.Amount}{genderSuffix}";

                    var row = new HorizontalStackPanel
                    {
                        Spacing = 6,
                        Padding = new Thickness(4, 2),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };

                    // Try to add item icon
                    try
                    {
                        var itemIcon = _mapItemGraphicProvider.GetItemGraphic(item.ItemID, item.Amount);
                        if (itemIcon != null)
                        {
                            var image = new Image
                            {
                                Renderable = new Myra.Graphics2D.TextureAtlases.TextureRegion(itemIcon),
                                Width = 24,
                                Height = 24,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            row.Widgets.Add(image);
                        }
                    }
                    catch { /* gracefully handle missing graphics */ }

                    var label = new Label
                    {
                        Text = itemText,
                        Font = _fontProvider.Normal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Wrap = true
                    };
                    row.Widgets.Add(label);

                    // Right-click to remove own items
                    if (actualOffer.PlayerID == _characterProvider.MainCharacter.ID)
                    {
                        var itemId = itemRec.ID;
                        row.TouchDown += (_, _) =>
                        {
                            _tradeActions.RemoveItemFromOffer(itemId);
                        };

                        // Hover effect for removable items
                        row.MouseEntered += (_, _) =>
                        {
                            row.Background = new SolidBrush(new Microsoft.Xna.Framework.Color(80, 80, 100, 100));
                        };
                        row.MouseLeft += (_, _) =>
                        {
                            row.Background = null;
                        };
                    }

                    itemList.Widgets.Add(row);
                }

                _sfxPlayer.PlaySfx(SoundEffectID.TradeItemOfferChanged);

                // Anti-cheat: detect partner rapidly changing items
                if (cachedOffer.Items != null && actualOffer.PlayerID != 0 && actualOffer.PlayerID != _characterProvider.MainCharacter.ID)
                {
                    _partnerItemChangeTick = Stopwatch.StartNew();
                    _recentPartnerItemChanges++;

                    if (_recentPartnerItemChanges == 2)
                    {
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.TRADE_OTHER_PLAYER_TRICK_YOU);
                        dlg.ShowDialog();
                        _recentPartnerItemChanges = -1000;
                    }
                    else if ((_leftOffer == cachedOffer ? _rightOffer : _leftOffer).Agrees)
                    {
                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.TRADE_ABORTED_OFFER_CHANGED);
                        dlg.ShowDialog();
                        _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, EOResourceID.STATUS_LABEL_TRADE_OTHER_PLAYER_CHANGED_OFFER);
                    }
                }
            }
        }

        private void OkButtonClicked()
        {
            var (offer, partnerOffer) = _leftOffer.PlayerID == _characterProvider.MainCharacter.ID
                ? (_leftOffer, _rightOffer)
                : (_rightOffer, _leftOffer);

            if (offer.Agrees)
                return;

            if (_leftOffer.Items.Count == 0 || _rightOffer.Items.Count == 0)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRADE_BOTH_PLAYERS_OFFER_ONE_ITEM, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
                return;
            }

            if (!_inventorySpaceValidator.ItemsFit(offer.Items, partnerOffer.Items))
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_SPACE, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
                return;
            }

            var partnerItemWeight = partnerOffer.Items
                .Select(x => _eifFileProvider.EIFFile[x.ItemID].Weight * x.Amount)
                .Aggregate((a, b) => a + b);
            var offerItemWeight = offer.Items
                .Select(x => _eifFileProvider.EIFFile[x.ItemID].Weight * x.Amount)
                .Aggregate((a, b) => a + b);

            var stats = _characterProvider.MainCharacter.Stats;
            if (stats[CharacterStat.Weight] - offerItemWeight + partnerItemWeight > stats[CharacterStat.MaxWeight])
            {
                var dlg = _messageBoxFactory.CreateMessageBox(EOResourceID.DIALOG_TRANSFER_NOT_ENOUGH_WEIGHT, EOResourceID.STATUS_LABEL_TYPE_WARNING);
                dlg.ShowDialog();
                return;
            }

            var finalCheckDlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.TRADE_DO_YOU_AGREE, EODialogButtons.OkCancel);
            finalCheckDlg.DialogClosing += (_, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    _tradeActions.AgreeToTrade(true);
                }
            };
            finalCheckDlg.ShowDialog();
        }

        private void CancelButtonClicked()
        {
            var offer = _leftOffer.PlayerID == _characterProvider.MainCharacter.ID
                ? _leftOffer
                : _rightOffer;

            if (!offer.Agrees)
            {
                _tradeActions.CancelTrade();
                Close(XNADialogResult.Cancel);
            }
            else
            {
                _tradeActions.AgreeToTrade(false);
            }
        }
    }
}
