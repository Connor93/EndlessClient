using System;
using System.Collections.Generic;
using System.Linq;
using EndlessClient.Audio;
using EndlessClient.Content;
using EndlessClient.Dialogs;
using EndlessClient.Dialogs.Factories;
using EndlessClient.Rendering;
using EndlessClient.UI.Controls;
using EndlessClient.UI.Styles;
using EOLib.Config;
using EOLib.Domain.Chat;
using EOLib.Graphics;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended.BitmapFonts;
using XNAControls;

namespace EndlessClient.HUD.Panels
{
    public class CodeDrawnSettingsPanel : DraggableHudPanel
    {
        private enum KeyboardLayout
        {
            English,
            Dutch,
            Swedish,
            Azerty
        }

        private enum WhichSetting
        {
            Sfx,
            Mfx,
            Keyboard,
            Language,
            HearWhispers,
            ShowBalloons,
            ShowShadows,
            CurseFilter,
            LogChat,
            Interaction,
        }

        private readonly IChatActions _chatActions;
        private readonly IAudioActions _audioActions;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly IConfigurationRepository _configurationRepository;
        private readonly ISfxPlayer _sfxPlayer;
        private readonly IUIStyleProvider _styleProvider;
        private readonly IGraphicsDeviceProvider _graphicsDeviceProvider;
        private readonly BitmapFont _font;
        private readonly BitmapFont _labelFont;

        private readonly Dictionary<WhichSetting, string> _settingLabels;
        private readonly Dictionary<WhichSetting, string> _settingValues;
        private readonly Dictionary<WhichSetting, Rectangle> _settingHitAreas;

        private bool _soundChanged, _musicChanged;
        private KeyboardLayout _keyboardLayout;
        private WhichSetting? _hoveredSetting;

        private const int PanelWidth = 476;
        private const int PanelHeight = 120;
        private const int RowHeight = 20;
        private const int ColWidth = 238;

        public CodeDrawnSettingsPanel(IChatActions chatActions,
                                      IAudioActions audioActions,
                                      IStatusLabelSetter statusLabelSetter,
                                      ILocalizedStringFinder localizedStringFinder,
                                      IEOMessageBoxFactory messageBoxFactory,
                                      IConfigurationRepository configurationRepository,
                                      ISfxPlayer sfxPlayer,
                                      IUIStyleProvider styleProvider,
                                      IGraphicsDeviceProvider graphicsDeviceProvider,
                                      IContentProvider contentProvider,
                                      IClientWindowSizeProvider clientWindowSizeProvider)
            : base(clientWindowSizeProvider.Resizable)
        {
            _chatActions = chatActions;
            _audioActions = audioActions;
            _statusLabelSetter = statusLabelSetter;
            _localizedStringFinder = localizedStringFinder;
            _messageBoxFactory = messageBoxFactory;
            _configurationRepository = configurationRepository;
            _sfxPlayer = sfxPlayer;
            _styleProvider = styleProvider;
            _graphicsDeviceProvider = graphicsDeviceProvider;
            _font = contentProvider.Fonts[Constants.FontSize08];
            _labelFont = contentProvider.Fonts[Constants.FontSize08pt5];

            DrawArea = new Rectangle(102, 330, PanelWidth, PanelHeight);

            _settingLabels = new Dictionary<WhichSetting, string>
            {
                { WhichSetting.Sfx, "Sound" },
                { WhichSetting.Mfx, "Music" },
                { WhichSetting.Keyboard, "Keyboard" },
                { WhichSetting.Language, "Language" },
                { WhichSetting.HearWhispers, "Whispers" },
                { WhichSetting.ShowBalloons, "Balloons" },
                { WhichSetting.ShowShadows, "Shadows" },
                { WhichSetting.CurseFilter, "Filter" },
                { WhichSetting.LogChat, "Log Chat" },
                { WhichSetting.Interaction, "Interaction" },
            };

            _settingValues = new Dictionary<WhichSetting, string>();
            _settingHitAreas = new Dictionary<WhichSetting, Rectangle>();

            var values = Enum.GetValues<WhichSetting>();
            foreach (var setting in values)
            {
                var ndx = (int)setting;
                var col = ndx / 5;
                var row = ndx % 5;

                _settingValues[setting] = "";
                _settingHitAreas[setting] = new Rectangle(4 + col * ColWidth, 6 + row * RowHeight, ColWidth - 8, RowHeight - 2);
            }

            UpdateDisplayText();
        }

        public override void Initialize()
        {
            DrawingPrimitives.Initialize(_graphicsDeviceProvider.GraphicsDevice);

            if (!_configurationRepository.HearWhispers)
                _chatActions.SetHearWhispers(_configurationRepository.HearWhispers);

            base.Initialize();
        }

        protected override void OnUpdateControl(GameTime gameTime)
        {
            // Detect hover
            var mouseState = Mouse.GetState();
            var mousePos = new Point(mouseState.X, mouseState.Y);
            var panelPos = DrawAreaWithParentOffset;

            _hoveredSetting = null;
            foreach (var pair in _settingHitAreas)
            {
                var hitArea = new Rectangle(panelPos.X + pair.Value.X, panelPos.Y + pair.Value.Y, pair.Value.Width, pair.Value.Height);
                if (hitArea.Contains(mousePos))
                {
                    _hoveredSetting = pair.Key;
                    break;
                }
            }

            base.OnUpdateControl(gameTime);
        }

        protected override bool HandleClick(IXNAControl control, MonoGame.Extended.Input.InputListeners.MouseEventArgs eventArgs)
        {
            if (_hoveredSetting.HasValue)
            {
                SettingChange(_hoveredSetting.Value);
                return true;
            }
            return base.HandleClick(control, eventArgs);
        }

        protected override void OnDrawControl(GameTime gameTime)
        {
            _spriteBatch.Begin();

            var pos = DrawPositionWithParentOffset;

            // Draw panel background
            var bgRect = new Rectangle((int)pos.X, (int)pos.Y, PanelWidth, PanelHeight);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, bgRect, _styleProvider.PanelBackground);
            DrawingPrimitives.DrawRectBorder(_spriteBatch, bgRect, _styleProvider.PanelBorder, 2);

            // Draw column divider
            var lineColor = new Color((byte)_styleProvider.PanelBorder.R, (byte)_styleProvider.PanelBorder.G, (byte)_styleProvider.PanelBorder.B, (byte)100);
            DrawingPrimitives.DrawFilledRect(_spriteBatch, new Rectangle((int)pos.X + ColWidth, (int)pos.Y + 4, 1, PanelHeight - 8), lineColor);

            // Draw settings
            var labelColor = _styleProvider.TextSecondary;

            foreach (var pair in _settingLabels)
            {
                var setting = pair.Key;
                var label = pair.Value;
                var ndx = (int)setting;
                var col = ndx / 5;
                var row = ndx % 5;

                var hitArea = _settingHitAreas[setting];
                var rowRect = new Rectangle((int)pos.X + hitArea.X, (int)pos.Y + hitArea.Y, hitArea.Width, hitArea.Height);

                // Draw hover highlight
                if (_hoveredSetting == setting)
                {
                    DrawingPrimitives.DrawFilledRect(_spriteBatch, rowRect, new Color(255, 255, 255, 30));
                    DrawingPrimitives.DrawRectBorder(_spriteBatch, rowRect, new Color(255, 255, 255, 60), 1);
                }

                var labelX = pos.X + 10 + col * ColWidth;
                var valueX = pos.X + 90 + col * ColWidth;
                var y = pos.Y + 9 + row * RowHeight;

                _spriteBatch.DrawString(_labelFont, label, new Vector2(labelX, y), labelColor);

                // Draw value with color based on state
                var value = _settingValues[setting];
                var valueColor = GetValueColor(setting, value);
                _spriteBatch.DrawString(_font, value, new Vector2(valueX, y), valueColor);

                // Draw toggle indicator arrow
                var arrowX = pos.X + 205 + col * ColWidth;
                _spriteBatch.DrawString(_font, "◄►", new Vector2(arrowX, y), new Color(120, 120, 120, 200));
            }

            _spriteBatch.End();

            base.OnDrawControl(gameTime);
        }

        private Color GetValueColor(WhichSetting setting, string value)
        {
            // Make enabled values green, disabled red, others white
            var enabledStr = _localizedStringFinder.GetString(EOResourceID.SETTING_ENABLED);
            var disabledStr = _localizedStringFinder.GetString(EOResourceID.SETTING_DISABLED);

            if (value == enabledStr)
                return new Color(100, 200, 100);
            else if (value == disabledStr)
                return new Color(200, 100, 100);
            else
                return _styleProvider.TextPrimary;
        }

        private void SettingChange(WhichSetting setting)
        {
            _sfxPlayer.PlaySfx(SoundEffectID.DialogButtonClick);

            switch (setting)
            {
                case WhichSetting.Sfx:
                    {
                        if (!_soundChanged && !_configurationRepository.SoundEnabled)
                        {
                            var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SETTINGS_SOUND_DISABLED, EODialogButtons.OkCancel);
                            dlg.DialogClosing += (_, e) =>
                            {
                                if (e.Result != XNADialogResult.OK)
                                    return;

                                _soundChanged = true;
                                _configurationRepository.SoundEnabled = !_configurationRepository.SoundEnabled;
                                _audioActions.ToggleSound();

                                UpdateDisplayText();
                            };
                            dlg.ShowDialog();
                        }
                        else
                        {
                            _soundChanged = true;
                            _configurationRepository.SoundEnabled = !_configurationRepository.SoundEnabled;
                            _audioActions.ToggleSound();
                        }
                    }
                    break;
                case WhichSetting.Mfx:
                    {
                        if (!_musicChanged && !_configurationRepository.MusicEnabled)
                        {
                            var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SETTINGS_MUSIC_DISABLED, EODialogButtons.OkCancel);
                            dlg.DialogClosing += (_, e) =>
                            {
                                if (e.Result != XNADialogResult.OK)
                                    return;

                                _musicChanged = true;
                                _configurationRepository.MusicEnabled = !_configurationRepository.MusicEnabled;
                                _audioActions.ToggleBackgroundMusic();

                                UpdateDisplayText();
                            };
                            dlg.ShowDialog();
                        }
                        else
                        {
                            _musicChanged = true;
                            _configurationRepository.MusicEnabled = !_configurationRepository.MusicEnabled;
                            _audioActions.ToggleBackgroundMusic();
                        }
                    }
                    break;
                case WhichSetting.Keyboard:
                    {
                        _keyboardLayout++;
                        if (_keyboardLayout > KeyboardLayout.Azerty)
                            _keyboardLayout = 0;
                    }
                    break;
                case WhichSetting.Language:
                    {
                        _configurationRepository.Language++;
                        if (_configurationRepository.Language > EOLanguage.Portuguese)
                            _configurationRepository.Language = 0;
                    }
                    break;
                case WhichSetting.HearWhispers:
                    {
                        _configurationRepository.HearWhispers = !_configurationRepository.HearWhispers;
                        _chatActions.SetHearWhispers(_configurationRepository.HearWhispers);
                    }
                    break;
                case WhichSetting.ShowBalloons:
                    _configurationRepository.ShowChatBubbles = !_configurationRepository.ShowChatBubbles;
                    break;
                case WhichSetting.ShowShadows:
                    _configurationRepository.ShowShadows = !_configurationRepository.ShowShadows;
                    break;
                case WhichSetting.CurseFilter:
                    {
                        if (_configurationRepository.StrictFilterEnabled)
                        {
                            _configurationRepository.StrictFilterEnabled = false;
                        }
                        else if (_configurationRepository.CurseFilterEnabled)
                        {
                            _configurationRepository.CurseFilterEnabled = false;
                            _configurationRepository.StrictFilterEnabled = true;
                        }
                        else
                        {
                            _configurationRepository.CurseFilterEnabled = true;
                        }
                    }
                    break;
                case WhichSetting.LogChat:
                    _configurationRepository.LogChatToFile = !_configurationRepository.LogChatToFile;
                    break;
                case WhichSetting.Interaction:
                    _configurationRepository.Interaction = !_configurationRepository.Interaction;
                    break;
            }

            UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            _settingValues[WhichSetting.Sfx] = _localizedStringFinder.GetString(_configurationRepository.SoundEnabled ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED);
            _settingValues[WhichSetting.Mfx] = _localizedStringFinder.GetString(_configurationRepository.MusicEnabled ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED);
            _settingValues[WhichSetting.Keyboard] = _localizedStringFinder.GetString(EOResourceID.SETTING_KEYBOARD_ENGLISH);
            _settingValues[WhichSetting.Language] = _localizedStringFinder.GetString(EOResourceID.SETTING_LANG_CURRENT);
            _settingValues[WhichSetting.HearWhispers] = _localizedStringFinder.GetString(_configurationRepository.HearWhispers ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED);

            _settingValues[WhichSetting.ShowBalloons] = _localizedStringFinder.GetString(_configurationRepository.ShowChatBubbles ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED);
            _settingValues[WhichSetting.ShowShadows] = _localizedStringFinder.GetString(_configurationRepository.ShowShadows ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED);
            if (_configurationRepository.StrictFilterEnabled)
                _settingValues[WhichSetting.CurseFilter] = _localizedStringFinder.GetString(EOResourceID.SETTING_EXCLUSIVE);
            else if (_configurationRepository.CurseFilterEnabled)
                _settingValues[WhichSetting.CurseFilter] = _localizedStringFinder.GetString(EOResourceID.SETTING_NORMAL);
            else
                _settingValues[WhichSetting.CurseFilter] = _localizedStringFinder.GetString(EOResourceID.SETTING_DISABLED);

            _settingValues[WhichSetting.LogChat] = _localizedStringFinder.GetString(_configurationRepository.LogChatToFile ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED);
            _settingValues[WhichSetting.Interaction] = _localizedStringFinder.GetString(_configurationRepository.Interaction ? EOResourceID.SETTING_ENABLED : EOResourceID.SETTING_DISABLED);
        }
    }
}
