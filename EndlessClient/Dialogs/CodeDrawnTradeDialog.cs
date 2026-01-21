using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Inventory;
using EndlessClient.Rendering.Map;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Domain.Trade;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using MonoGame.Extended.BitmapFonts;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A code-drawn version of TradeDialog with procedural rendering.
    /// Features dual-panel layout for trading between two players.
    /// </summary>
    public class CodeDrawnTradeDialog : XNADialog
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly INativeGraphicsManager _graphicsManager;
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
        private readonly IContentProvider _contentProvider;

        private readonly List<TradeListItem> _leftItems, _rightItems;
        private readonly CodeDrawnScrollBar _leftScroll, _rightScroll;
        private IXNALabel _leftPlayerName, _rightPlayerName;
        private IXNALabel _leftPlayerStatus, _rightPlayerStatus;
        private CodeDrawnButton _okButton, _cancelButton;

        private TradeOffer _leftOffer, _rightOffer;
        private int _leftScrollOffset, _rightScrollOffset;

        private int _recentPartnerItemChanges;
        private Stopwatch _partnerItemChangeTick;

        private const int DialogWidth = 550;
        private const int DialogHeight = 290;
        private const int PanelWidth = 270;
        private const int ListAreaTop = 45;
        private const int ListAreaHeight = 195;
        private const int ItemHeight = 36;
        private const int ItemsToShow = 5;

        public CodeDrawnTradeDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            INativeGraphicsManager graphicsManager,
            ITradeActions tradeActions,
            ILocalizedStringFinder localizedStringFinder,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            IInventorySpaceValidator inventorySpaceValidator,
            ITradeProvider tradeProvider,
            ICharacterProvider characterProvider,
            IEIFFileProvider eifFileProvider,
            IMapItemGraphicProvider mapItemGraphicProvider,
            ISfxPlayer sfxPlayer,
            IContentProvider contentProvider)
        {
            _styleProvider = styleProvider;
            _graphicsManager = graphicsManager;
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
            _contentProvider = contentProvider;

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            _leftItems = new List<TradeListItem>();
            _rightItems = new List<TradeListItem>();
            _leftOffer = new TradeOffer.Builder().ToImmutable();
            _rightOffer = new TradeOffer.Builder().ToImmutable();

            // Create scrollbars
            _leftScroll = new CodeDrawnScrollBar(_styleProvider, new Vector2(PanelWidth - 24, ListAreaTop), new Vector2(16, ListAreaHeight))
            {
                LinesToRender = ItemsToShow
            };
            _leftScroll.SetParentControl(this);

            _rightScroll = new CodeDrawnScrollBar(_styleProvider, new Vector2(DialogWidth - 24, ListAreaTop), new Vector2(16, ListAreaHeight))
            {
                LinesToRender = ItemsToShow
            };
            _rightScroll.SetParentControl(this);

            CreateLabels();
            CreateButtons();
            CenterInGameView();
        }

        private void CreateLabels()
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];

            _leftPlayerName = new XNALabel(Constants.FontSize08pt5)
            {
                DrawArea = new Rectangle(16, 12, 180, 20),
                AutoSize = false,
                TextAlign = LabelAlignment.MiddleLeft,
                ForeColor = _styleProvider.TextPrimary,
                Text = ""
            };
            _leftPlayerName.SetParentControl(this);

            _rightPlayerName = new XNALabel(Constants.FontSize08pt5)
            {
                DrawArea = new Rectangle(PanelWidth + 16, 12, 180, 20),
                AutoSize = false,
                TextAlign = LabelAlignment.MiddleLeft,
                ForeColor = _styleProvider.TextPrimary,
                Text = ""
            };
            _rightPlayerName.SetParentControl(this);

            _leftPlayerStatus = new XNALabel(Constants.FontSize08pt5)
            {
                DrawArea = new Rectangle(200, 12, 60, 20),
                AutoSize = false,
                TextAlign = LabelAlignment.MiddleRight,
                ForeColor = _styleProvider.TextHighlight,
                Text = _localizedStringFinder.GetString(EOResourceID.DIALOG_TRADE_WORD_TRADING)
            };
            _leftPlayerStatus.SetParentControl(this);

            _rightPlayerStatus = new XNALabel(Constants.FontSize08pt5)
            {
                DrawArea = new Rectangle(PanelWidth + 200, 12, 60, 20),
                AutoSize = false,
                TextAlign = LabelAlignment.MiddleRight,
                ForeColor = _styleProvider.TextHighlight,
                Text = _localizedStringFinder.GetString(EOResourceID.DIALOG_TRADE_WORD_TRADING)
            };
            _rightPlayerStatus.SetParentControl(this);
        }

        private void CreateButtons()
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];
            var buttonWidth = 72;
            var buttonHeight = 26;
            var buttonY = DialogHeight - 36;

            _okButton = new CodeDrawnButton(_styleProvider, font)
            {
                Text = "OK",
                DrawArea = new Rectangle(DialogWidth / 2 - buttonWidth - 16, buttonY, buttonWidth, buttonHeight)
            };
            _okButton.OnClick += OkButtonClicked;
            _okButton.SetParentControl(this);

            _cancelButton = new CodeDrawnButton(_styleProvider, font)
            {
                Text = "Cancel",
                DrawArea = new Rectangle(DialogWidth / 2 + 16, buttonY, buttonWidth, buttonHeight)
            };
            _cancelButton.OnClick += CancelButtonClicked;
            _cancelButton.SetParentControl(this);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(Game.GraphicsDevice);

            _leftPlayerName?.Initialize();
            _rightPlayerName?.Initialize();
            _leftPlayerStatus?.Initialize();
            _rightPlayerStatus?.Initialize();
            _leftScroll?.Initialize();
            _rightScroll?.Initialize();
            _okButton?.Initialize();
            _cancelButton?.Initialize();

            foreach (var item in _leftItems)
                item.Initialize();
            foreach (var item in _rightItems)
                item.Initialize();

            base.Initialize();
        }

        public override void CenterInGameView()
        {
            base.CenterInGameView();

            var centerX = (Game.GraphicsDevice.Viewport.Width - DialogWidth) / 2;
            var centerY = (Game.GraphicsDevice.Viewport.Height - DialogHeight) / 2;
            DrawPosition = new Vector2(centerX, centerY);
        }

        /// <summary>
        /// Closes the dialog with Cancel result.
        /// </summary>
        public void Close()
        {
            Close(XNADialogResult.Cancel);
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            var updateItemVisibility = false;

            if (_tradeProvider.PlayerOneOffer != null && !_tradeProvider.PlayerOneOffer.Equals(_leftOffer))
            {
                UpdateOffer(_tradeProvider.PlayerOneOffer, _leftOffer, _leftPlayerName, _leftPlayerStatus, _leftItems, isLeft: true);
                _leftOffer = _tradeProvider.PlayerOneOffer;
                _leftScroll.UpdateDimensions(_leftOffer.Items.Count);
                updateItemVisibility = true;
            }

            if (_tradeProvider.PlayerTwoOffer != null && !_tradeProvider.PlayerTwoOffer.Equals(_rightOffer))
            {
                UpdateOffer(_tradeProvider.PlayerTwoOffer, _rightOffer, _rightPlayerName, _rightPlayerStatus, _rightItems, isLeft: false);
                _rightOffer = _tradeProvider.PlayerTwoOffer;
                _rightScroll.UpdateDimensions(_rightOffer.Items.Count);
                updateItemVisibility = true;
            }

            if (updateItemVisibility || _leftScrollOffset != _leftScroll.ScrollOffset)
            {
                UpdateItemScrollIndexes(_leftScroll, _leftItems);
                _leftScrollOffset = _leftScroll.ScrollOffset;
            }

            if (updateItemVisibility || _rightScrollOffset != _rightScroll.ScrollOffset)
            {
                UpdateItemScrollIndexes(_rightScroll, _rightItems);
                _rightScrollOffset = _rightScroll.ScrollOffset;
            }

            if (_partnerItemChangeTick?.ElapsedMilliseconds > 1000 && _recentPartnerItemChanges > 0)
            {
                _recentPartnerItemChanges--;
                _partnerItemChangeTick = Stopwatch.StartNew();
            }

            base.OnUpdateControl(gameTime);
        }

        private void UpdateOffer(TradeOffer actualOffer, TradeOffer cachedOffer,
            IXNALabel playerNameLabel, IXNALabel playerStatusLabel,
            List<TradeListItem> listItems,
            bool isLeft)
        {
            if (actualOffer.PlayerName != cachedOffer.PlayerName || actualOffer.Items.Count != cachedOffer.Items.Count)
            {
                if (!string.IsNullOrEmpty(actualOffer.PlayerName))
                {
                    playerNameLabel.Text = $"{char.ToUpper(actualOffer.PlayerName[0]) + actualOffer.PlayerName[1..]}{(actualOffer.Items.Any() ? $" [{actualOffer.Items.Count}]" : "")}";
                }
            }

            if (actualOffer.Agrees != cachedOffer.Agrees)
            {
                playerStatusLabel.Text = actualOffer.Agrees
                    ? _localizedStringFinder.GetString(EOResourceID.DIALOG_TRADE_WORD_AGREE)
                    : _localizedStringFinder.GetString(EOResourceID.DIALOG_TRADE_WORD_TRADING);
                playerStatusLabel.ForeColor = actualOffer.Agrees ? new Color(100, 255, 100) : _styleProvider.TextHighlight;

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

            if (cachedOffer.Items == null || !actualOffer.Items.ToHashSet().SetEquals(cachedOffer.Items))
            {
                var added = actualOffer.Items.Except(cachedOffer.Items ?? Enumerable.Empty<InventoryItem>());
                var removed = (cachedOffer.Items ?? Enumerable.Empty<InventoryItem>()).Where(i => !actualOffer.Items.Contains(i));

                foreach (var addedItem in added)
                {
                    var itemRec = _eifFileProvider.EIFFile[addedItem.ItemID];
                    var subText = $"x {addedItem.Amount}  {(itemRec.Type == ItemType.Armor ? $"({_localizedStringFinder.GetString(itemRec.Gender == 0 ? EOResourceID.FEMALE : EOResourceID.MALE)})" : string.Empty)}";
                    var itemIcon = _mapItemGraphicProvider.GetItemGraphic(addedItem.ItemID, addedItem.Amount);

                    var newListItem = new TradeListItem(_styleProvider, _contentProvider.Fonts[Constants.FontSize08pt5], this, isLeft)
                    {
                        Data = addedItem,
                        PrimaryText = itemRec.Name,
                        SubText = subText,
                        IconGraphic = itemIcon
                    };

                    if (actualOffer.PlayerID == _characterProvider.MainCharacter.ID)
                        newListItem.RightClick += (_, _) => _tradeActions.RemoveItemFromOffer(itemRec.ID);

                    newListItem.SetParentControl(this);
                    listItems.Add(newListItem);

                    _sfxPlayer.PlaySfx(SoundEffectID.TradeItemOfferChanged);
                }

                foreach (var removedItem in removed)
                {
                    listItems.SingleOrNone(y => ((InventoryItem)y.Data).Equals(removedItem))
                        .MatchSome(listItem =>
                        {
                            listItems.Remove(listItem);
                            listItem.Dispose();
                        });

                    _sfxPlayer.PlaySfx(SoundEffectID.TradeItemOfferChanged);
                }

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

        private void UpdateItemScrollIndexes(CodeDrawnScrollBar scrollBar, List<TradeListItem> items)
        {
            var scrollOffset = items.Count > ItemsToShow ? scrollBar.ScrollOffset : 0;

            for (int i = 0; i < items.Count; i++)
            {
                items[i].Visible = i >= scrollOffset && i < ItemsToShow + scrollOffset;
                items[i].VisualIndex = i - scrollOffset;
            }
        }

        private void OkButtonClicked(object sender, EventArgs e)
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
            finalCheckDlg.DialogClosing += (o, e) =>
            {
                if (e.Result == XNADialogResult.OK)
                {
                    _tradeActions.AgreeToTrade(true);
                }
            };
            finalCheckDlg.ShowDialog();
        }

        private void CancelButtonClicked(object sender, EventArgs e)
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

        protected override bool HandleMouseWheelMoved(IXNAControl control, MonoGame.Extended.Input.InputListeners.MouseEventArgs eventArgs)
        {
            // Determine which panel the mouse is over
            var mouseX = eventArgs.Position.X - (int)DrawPosition.X;
            if (mouseX < PanelWidth)
            {
                if (eventArgs.ScrollWheelDelta > 0)
                    _leftScroll.ScrollUp(2);
                else if (eventArgs.ScrollWheelDelta < 0)
                    _leftScroll.ScrollDown(2);
            }
            else
            {
                if (eventArgs.ScrollWheelDelta > 0)
                    _rightScroll.ScrollUp(2);
                else if (eventArgs.ScrollWheelDelta < 0)
                    _rightScroll.ScrollDown(2);
            }

            return true;
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            _spriteBatch.Begin(transformMatrix: transform);

            // Main dialog background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(0, 0, DialogWidth, DialogHeight), _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, new Rectangle(0, 0, DialogWidth, DialogHeight), _styleProvider.PanelBorder, 2);

            // Left panel background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(4, ListAreaTop, PanelWidth - 30, ListAreaHeight), new Color(0, 0, 0, 60));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, new Rectangle(4, ListAreaTop, PanelWidth - 30, ListAreaHeight), _styleProvider.PanelBorder, 1);

            // Right panel background
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(PanelWidth + 4, ListAreaTop, PanelWidth - 30, ListAreaHeight), new Color(0, 0, 0, 60));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, new Rectangle(PanelWidth + 4, ListAreaTop, PanelWidth - 30, ListAreaHeight), _styleProvider.PanelBorder, 1);

            // Title bar
            var titleBarHeight = 32;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(2, 2, DialogWidth - 4, titleBarHeight - 2), _styleProvider.TitleBarBackground);

            // Center divider line
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(PanelWidth - 2, 2, 4, DialogHeight - 50), _styleProvider.PanelBorder);

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }
    }

    /// <summary>
    /// A single item in a trade dialog list.
    /// </summary>
    internal class TradeListItem : XNAControl
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly MonoGame.Extended.BitmapFonts.BitmapFont _font;
        private readonly CodeDrawnTradeDialog _parentDialog;
        private readonly bool _isLeftPanel;

        private const int PanelWidth = 270;
        private const int ListAreaTop = 45;
        private const int ItemHeight = 36;

        public int VisualIndex { get; set; }
        public string PrimaryText { get; set; } = string.Empty;
        public string SubText { get; set; } = string.Empty;
        public object Data { get; set; }
        public Microsoft.Xna.Framework.Graphics.Texture2D IconGraphic { get; set; }

        public event EventHandler<MonoGame.Extended.Input.InputListeners.MouseEventArgs> RightClick;

        public TradeListItem(IUIStyleProvider styleProvider, MonoGame.Extended.BitmapFonts.BitmapFont font, CodeDrawnTradeDialog parent, bool isLeftPanel)
        {
            _styleProvider = styleProvider;
            _font = font;
            _parentDialog = parent;
            _isLeftPanel = isLeftPanel;
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            var baseX = _isLeftPanel ? 8 : (PanelWidth + 8);
            var yPos = ListAreaTop + (VisualIndex * ItemHeight);
            DrawArea = new Rectangle(baseX, yPos, PanelWidth - 40, ItemHeight);

            base.OnUpdateControl(gameTime);
        }

        protected override bool HandleMouseUp(IXNAControl control, MonoGame.Extended.Input.InputListeners.MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MonoGame.Extended.Input.MouseButton.Right)
            {
                RightClick?.Invoke(this, eventArgs);
                return true;
            }
            return base.HandleMouseUp(control, eventArgs);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            if (!Visible) return;

            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            _spriteBatch.Begin(transformMatrix: transform);

            var textOffsetX = IconGraphic != null ? 40 : 4;

            // Icon
            if (IconGraphic != null)
            {
                var iconSize = Math.Min(32, DrawArea.Height - 2);
                var iconY = (DrawArea.Height - iconSize) / 2;
                _spriteBatch.Draw(IconGraphic, new Rectangle(4, iconY, iconSize, iconSize), Color.White);
            }

            // Primary text
            if (!string.IsNullOrEmpty(PrimaryText))
            {
                _spriteBatch.DrawString(_font, PrimaryText, new Vector2(textOffsetX, 2), _styleProvider.TextPrimary);
            }

            // Sub text
            if (!string.IsNullOrEmpty(SubText))
            {
                var subSize = _font.MeasureString(SubText);
                var subPos = new Vector2(DrawArea.Width - subSize.Width - 4, 2);
                _spriteBatch.DrawString(_font, SubText, subPos, _styleProvider.TextSecondary);
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }
    }
}
