using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Controllers;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Extensions;
using EndlessClient.Dialogs.Factories;
using EndlessClient.GameExecution;
using EndlessClient.HUD;
using EndlessClient.HUD.Controls;
using EndlessClient.HUD.Inventory;
using EndlessClient.HUD.Panels;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Domain.Character;
using EOLib.Extensions;
using EOLib.Graphics;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using MonoGame.Extended.BitmapFonts;
using Optional;
using Optional.Unsafe;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// A code-drawn version of the paperdoll dialog showing character info and equipment.
    /// </summary>
    public class CodeDrawnPaperdollDialog : XNADialog
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly INativeGraphicsManager _graphicsManager;
        private readonly IInventoryController _inventoryController;
        private readonly IPaperdollProvider _paperdollProvider;
        private readonly IPubFileProvider _pubFileProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IContentProvider _contentProvider;

        private readonly bool _isMainCharacter;
        private readonly Character _character;

        private readonly List<CodeDrawnPaperdollItem> _equipmentItems;
        private CodeDrawnButton _okButton;
        private IXNALabel _nameLabel, _homeLabel, _classLabel, _partnerLabel, _titleLabel, _guildLabel, _rankLabel;

        private Option<PaperdollData> _cachedPaperdollData;

        private const int DialogWidth = 385;
        private const int DialogHeight = 320;  // Taller to fit equipment slots (max y=274)

        public CodeDrawnPaperdollDialog(
            IUIStyleProvider styleProvider,
            IGameStateProvider gameStateProvider,
            INativeGraphicsManager graphicsManager,
            IInventoryController inventoryController,
            IPaperdollProvider paperdollProvider,
            IPubFileProvider pubFileProvider,
            IHudControlProvider hudControlProvider,
            IInventorySpaceValidator inventorySpaceValidator,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            ISfxPlayer sfxPlayer,
            IContentProvider contentProvider,
            Character character,
            bool isMainCharacter)
        {
            _styleProvider = styleProvider;
            _graphicsManager = graphicsManager;
            _inventoryController = inventoryController;
            _paperdollProvider = paperdollProvider;
            _pubFileProvider = pubFileProvider;
            _hudControlProvider = hudControlProvider;
            _inventorySpaceValidator = inventorySpaceValidator;
            _messageBoxFactory = messageBoxFactory;
            _statusLabelSetter = statusLabelSetter;
            _sfxPlayer = sfxPlayer;
            _contentProvider = contentProvider;
            _character = character;
            _isMainCharacter = isMainCharacter;

            DrawArea = new Rectangle(0, 0, DialogWidth, DialogHeight);

            _equipmentItems = new List<CodeDrawnPaperdollItem>();
            _cachedPaperdollData = Option.None<PaperdollData>();

            CreateLabels();
            CreateButtons();
            CenterInGameView();
        }

        private void CreateLabels()
        {
            // Value labels positioned after the static "Name:", "Class:" etc. labels
            var valueX = 300;  // After the static labels which are at x=240
            var labelColor = _styleProvider.TextPrimary;

            _nameLabel = new XNALabel(Constants.FontSize08pt5) { DrawArea = new Rectangle(valueX, 38, 80, 20), ForeColor = labelColor };
            _nameLabel.SetParentControl(this);

            _homeLabel = new XNALabel(Constants.FontSize08pt5) { DrawArea = new Rectangle(valueX, 177, 80, 20), ForeColor = labelColor };
            _homeLabel.SetParentControl(this);

            _classLabel = new XNALabel(Constants.FontSize08pt5) { DrawArea = new Rectangle(valueX, 82, 80, 20), ForeColor = labelColor };
            _classLabel.SetParentControl(this);

            _partnerLabel = new XNALabel(Constants.FontSize08pt5) { DrawArea = new Rectangle(valueX, 147, 80, 20), ForeColor = labelColor };
            _partnerLabel.SetParentControl(this);

            _titleLabel = new XNALabel(Constants.FontSize08pt5) { DrawArea = new Rectangle(valueX, 105, 80, 20), ForeColor = labelColor };
            _titleLabel.SetParentControl(this);

            _guildLabel = new XNALabel(Constants.FontSize08pt5) { DrawArea = new Rectangle(valueX, 195, 80, 20), ForeColor = labelColor };
            _guildLabel.SetParentControl(this);

            _rankLabel = new XNALabel(Constants.FontSize08pt5) { DrawArea = new Rectangle(valueX, 216, 80, 20), ForeColor = labelColor };
            _rankLabel.SetParentControl(this);
        }

        private void CreateButtons()
        {
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];

            _okButton = new CodeDrawnButton(_styleProvider, font)
            {
                Text = "OK",
                DrawArea = new Rectangle(DialogWidth / 2 - 36, DialogHeight - 36, 72, 26)
            };
            _okButton.OnClick += (_, _) => Close(XNADialogResult.OK);
            _okButton.SetParentControl(this);
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(Game.GraphicsDevice);

            _nameLabel?.Initialize();
            _homeLabel?.Initialize();
            _classLabel?.Initialize();
            _partnerLabel?.Initialize();
            _titleLabel?.Initialize();
            _guildLabel?.Initialize();
            _rankLabel?.Initialize();
            _okButton?.Initialize();

            foreach (var item in _equipmentItems)
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

        public void Close()
        {
            Close(XNADialogResult.OK);
        }

        public bool NoItemsDragging() => !_equipmentItems.Any(x => x.IsBeingDragged);

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Check for updated paperdoll data
            if (_paperdollProvider.VisibleCharacterPaperdolls.ContainsKey(_character.ID))
            {
                var newData = _paperdollProvider.VisibleCharacterPaperdolls[_character.ID];

                _cachedPaperdollData.Match(
                    some: cached =>
                    {
                        if (!cached.Equals(newData))
                        {
                            UpdateDisplayedData(newData);
                            _cachedPaperdollData = Option.Some(newData);
                        }
                    },
                    none: () =>
                    {
                        UpdateDisplayedData(newData);
                        _cachedPaperdollData = Option.Some(newData);
                    });
            }

            base.OnUpdateControl(gameTime);
        }

        private void UpdateDisplayedData(PaperdollData paperdollData)
        {
            // Update labels
            _nameLabel.Text = Capitalize(paperdollData.Name);
            _homeLabel.Text = Capitalize(paperdollData.Home);
            _partnerLabel.Text = Capitalize(paperdollData.Partner);
            _titleLabel.Text = Capitalize(paperdollData.Title);
            _guildLabel.Text = Capitalize(paperdollData.Guild);
            _rankLabel.Text = Capitalize(paperdollData.Rank);

            paperdollData.Class.SomeWhen(x => x != 0)
                .MatchSome(classId => _classLabel.Text = Capitalize(_pubFileProvider.ECFFile[classId].Name));

            // Clear existing equipment items
            foreach (var item in _equipmentItems)
            {
                item.SetControlUnparented();
                item.Dispose();
            }
            _equipmentItems.Clear();

            // Create new equipment items
            foreach (EquipLocation equipLocation in Enum.GetValues(typeof(EquipLocation)))
            {
                if (equipLocation == EquipLocation.PAPERDOLL_MAX)
                    break;

                if (!paperdollData.Paperdoll.ContainsKey(equipLocation))
                    continue;

                var id = paperdollData.Paperdoll[equipLocation];
                var eifRecord = id.SomeWhen(i => i > 0).Map(i => _pubFileProvider.EIFFile[i]);

                var rect = equipLocation.GetEquipLocationRectangle();
                var equipItem = new CodeDrawnPaperdollItem(
                    _styleProvider,
                    _graphicsManager,
                    _sfxPlayer,
                    _inventoryController,
                    _inventorySpaceValidator,
                    _messageBoxFactory,
                    _statusLabelSetter,
                    this,
                    _isMainCharacter,
                    equipLocation,
                    eifRecord)
                {
                    DrawArea = rect
                };

                equipItem.SetParentControl(this);
                equipItem.Initialize();
                _equipmentItems.Add(equipItem);
            }
        }

        private static string Capitalize(string input) =>
            string.IsNullOrEmpty(input) ? string.Empty : char.ToUpper(input[0]) + input[1..].ToLower();

        protected override void OnDrawControl(GameTime gameTime)
        {
            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            _spriteBatch.Begin(transformMatrix: transform);

            // Main dialog background - more opaque
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(0, 0, DialogWidth, DialogHeight), _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, new Rectangle(0, 0, DialogWidth, DialogHeight), _styleProvider.PanelBorder, 2);

            // Title bar
            var titleBarHeight = 32;
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(2, 2, DialogWidth - 4, titleBarHeight - 2), _styleProvider.TitleBarBackground);

            // Title text
            var font = _contentProvider.Fonts[Constants.FontSize08pt5];
            _spriteBatch.DrawString(font, "Paperdoll", new Vector2(16, 8), _styleProvider.TitleBarText);

            // Equipment area background (left side) - more opaque
            // Equipment slots extend from y=21 to y=274, so area should cover that
            var equipAreaWidth = 220;
            var equipAreaTop = 20;
            var equipAreaHeight = 260;  // y=20 to y=280 covers all equipment slots
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(8, equipAreaTop, equipAreaWidth, equipAreaHeight), new Color(20, 20, 30, 200));
            DrawingPrimitives.DrawRectBorder(_spriteBatch, new Rectangle(8, equipAreaTop, equipAreaWidth, equipAreaHeight), _styleProvider.PanelBorder, 1);

            // Draw static field labels ("Name:", "Class:", etc.)
            DrawFieldLabels(font);

            _spriteBatch.End();
            base.OnDrawControl(gameTime);
        }

        private void DrawFieldLabels(BitmapFont font)
        {
            var labelX = 240;
            var fieldLabels = new[]
            {
                (38, "Name:"),
                (82, "Class:"),
                (105, "Title:"),
                (147, "Partner:"),
                (177, "Home:"),
                (195, "Guild:"),
                (216, "Rank:")
            };

            foreach (var (y, label) in fieldLabels)
            {
                _spriteBatch.DrawString(font, label, new Vector2(labelX, y), _styleProvider.TextSecondary);
            }
        }
    }

    /// <summary>
    /// A single equipment slot item in the paperdoll dialog.
    /// </summary>
    internal class CodeDrawnPaperdollItem : XNAControl
    {
        private readonly IUIStyleProvider _styleProvider;
        private readonly INativeGraphicsManager _graphicsManager;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IInventoryController _inventoryController;
        private readonly IInventorySpaceValidator _inventorySpaceValidator;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly CodeDrawnPaperdollDialog _parentDialog;
        private readonly bool _isMainCharacter;
        private readonly EquipLocation _equipLocation;
        private readonly Option<EOLib.IO.Pub.EIFRecord> _itemRecord;

        private Texture2D _itemTexture;

        public bool IsBeingDragged { get; private set; }

        public CodeDrawnPaperdollItem(
            IUIStyleProvider styleProvider,
            INativeGraphicsManager graphicsManager,
            ISfxPlayer sfxPlayer,
            IInventoryController inventoryController,
            IInventorySpaceValidator inventorySpaceValidator,
            IEOMessageBoxFactory messageBoxFactory,
            IStatusLabelSetter statusLabelSetter,
            CodeDrawnPaperdollDialog parentDialog,
            bool isMainCharacter,
            EquipLocation equipLocation,
            Option<EOLib.IO.Pub.EIFRecord> itemRecord)
        {
            _styleProvider = styleProvider;
            _graphicsManager = graphicsManager;
            _sfxPlayer = sfxPlayer;
            _inventoryController = inventoryController;
            _inventorySpaceValidator = inventorySpaceValidator;
            _messageBoxFactory = messageBoxFactory;
            _statusLabelSetter = statusLabelSetter;
            _parentDialog = parentDialog;
            _isMainCharacter = isMainCharacter;
            _equipLocation = equipLocation;
            _itemRecord = itemRecord;

            // Load item texture if equipped - use Graphic * 2 for inventory icon (not 2*Graphic-1 which is map drop)
            _itemRecord.MatchSome(rec =>
            {
                _itemTexture = _graphicsManager.TextureFromResource(GFXTypes.Items, rec.Graphic * 2, transparent: true);
            });
        }

        protected override bool HandleClick(IXNAControl control, MonoGame.Extended.Input.InputListeners.MouseEventArgs eventArgs)
        {
            if (!_isMainCharacter) return base.HandleClick(control, eventArgs);

            _itemRecord.MatchSome(rec =>
            {
                if (rec.Special == ItemSpecial.Cursed)
                {
                    var msgBox = _messageBoxFactory.CreateMessageBox(DialogResourceID.ITEM_IS_CURSED_ITEM, EODialogButtons.Ok, EOMessageBoxStyle.SmallDialogSmallHeader);
                    msgBox.ShowDialog();
                }
                else
                {
                    if (!_inventorySpaceValidator.ItemFits(rec.ID))
                    {
                        _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, EOResourceID.STATUS_LABEL_ITEM_UNEQUIP_NO_SPACE_LEFT);
                    }
                    else
                    {
                        _inventoryController.UnequipItem(_equipLocation);
                        _sfxPlayer.PlaySfx(SoundEffectID.InventoryPlace);
                    }
                }
            });

            return true;
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            var drawPos = DrawAreaWithParentOffset;
            var transform = Matrix.CreateTranslation(drawPos.X, drawPos.Y, 0);

            _spriteBatch.Begin(transformMatrix: transform);

            // Draw slot background (subtle)
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle(0, 0, DrawArea.Width, DrawArea.Height), new Color(0, 0, 0, 20));

            // Draw item if equipped - at natural size, centered in slot (like original StretchMode.CenterInFrame)
            if (_itemTexture != null)
            {
                var texWidth = _itemTexture.Width;
                var texHeight = _itemTexture.Height;
                var iconX = (DrawArea.Width - texWidth) / 2;
                var iconY = (DrawArea.Height - texHeight) / 2;
                _spriteBatch.Draw(_itemTexture, new Vector2(iconX, iconY), Color.White);
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }
    }
}
