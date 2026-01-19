using AutomaticTypeMapper;
using EndlessClient.Audio;
using EndlessClient.ControlSets;
using EndlessClient.Dialogs.Actions;
using EndlessClient.HUD;
using EndlessClient.HUD.Controls;
using EndlessClient.HUD.Macros;
using EndlessClient.HUD.Panels;
using EndlessClient.HUD.Spells;
using EndlessClient.Rendering;
using EndlessClient.Rendering.Character;
using EndlessClient.Rendering.Map;
using EOLib.Domain.Character;
using EOLib.Domain.Extensions;
using EOLib.Domain.Map;
using EOLib.Domain.Spells;
using EOLib.IO;
using EOLib.IO.Repositories;
using EOLib.Localization;

namespace EndlessClient.Controllers
{
    [AutoMappedType]
    public class FunctionKeyController : IFunctionKeyController
    {
        private readonly IMapActions _mapActions;
        private readonly ICharacterActions _characterActions;
        private readonly ISpellSelectActions _spellSelectActions;
        private readonly ICharacterAnimationActions _characterAnimationActions;
        private readonly ISpellCastValidationActions _spellCastValidationActions;
        private readonly IInGameDialogActions _inGameDialogActions;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly ICharacterProvider _characterProvider;
        private readonly IESFFileProvider _esfFileProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly ISpellSlotDataRepository _spellSlotDataRepository;
        private readonly IMacroSlotDataProvider _macroSlotDataProvider;
        private readonly IHudControlProvider _hudControlProvider;
        private readonly IInventoryController _inventoryController;
        private readonly ISfxPlayer _sfxPlayer;

        public FunctionKeyController(IMapActions mapActions,
                                     ICharacterActions characterActions,
                                     ISpellSelectActions spellSelectActions,
                                     ICharacterAnimationActions characterAnimationActions,
                                     ISpellCastValidationActions spellCastValidationActions,
                                     IInGameDialogActions inGameDialogActions,
                                     IStatusLabelSetter statusLabelSetter,
                                     ICharacterProvider characterProvider,
                                     IESFFileProvider esfFileProvider,
                                     IEIFFileProvider eifFileProvider,
                                     ISpellSlotDataRepository spellSlotDataRepository,
                                     IMacroSlotDataProvider macroSlotDataProvider,
                                     IHudControlProvider hudControlProvider,
                                     IInventoryController inventoryController,
                                     ISfxPlayer sfxPlayer)
        {
            _mapActions = mapActions;
            _characterActions = characterActions;
            _spellSelectActions = spellSelectActions;
            _characterAnimationActions = characterAnimationActions;
            _spellCastValidationActions = spellCastValidationActions;
            _inGameDialogActions = inGameDialogActions;
            _statusLabelSetter = statusLabelSetter;
            _characterProvider = characterProvider;
            _esfFileProvider = esfFileProvider;
            _eifFileProvider = eifFileProvider;
            _spellSlotDataRepository = spellSlotDataRepository;
            _macroSlotDataProvider = macroSlotDataProvider;
            _hudControlProvider = hudControlProvider;
            _inventoryController = inventoryController;
            _sfxPlayer = sfxPlayer;
        }

        public bool SelectSpell(int index, bool isAlternate)
        {
            if (!_characterProvider.MainCharacter.RenderProperties.IsActing(CharacterActionState.Standing))
                return false;

            var macroSlotIndex = index + (isAlternate ? MacroSlotDataRepository.MacroRowLength : 0);

            // Check if there's a macro slot assigned for this F-key
            if (macroSlotIndex < _macroSlotDataProvider.MacroSlots.Count)
            {
                var macroSlotOption = _macroSlotDataProvider.MacroSlots[macroSlotIndex];
                var handled = false;

                macroSlotOption.MatchSome(macroSlot =>
                {
                    if (macroSlot.Type == MacroSlotType.Item)
                    {
                        // Use item from macro slot
                        var itemData = _eifFileProvider.EIFFile[macroSlot.Id];
                        if (itemData != null)
                        {
                            _inventoryController.UseItemById(macroSlot.Id);
                            _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_ITEM, itemData.Name);
                            handled = true;
                        }
                    }
                    else if (macroSlot.Type == MacroSlotType.Spell)
                    {
                        // Use spell from macro slot
                        HandleSpellCast(macroSlot.Id);
                        handled = true;
                    }
                });

                if (handled)
                    return true;
            }

            // Fall back to the spell slot system if no macro is assigned
            _spellSelectActions.SelectSpellBySlot(index + (isAlternate ? ActiveSpellsPanel.SpellRowLength : 0));

            _spellSlotDataRepository.SelectedSpellInfo.Match(
                some: x =>
                {
                    HandleSpellCast(x.ID);
                },
                none: () => _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, EOResourceID.SPELL_NOTHING_WAS_SELECTED)
            );

            return true;
        }

        private void HandleSpellCast(int spellId)
        {
            var spellData = _esfFileProvider.ESFFile[spellId];
            if (spellData.Type == SpellType.Bard && _spellCastValidationActions.ValidateBard())
            {
                _inGameDialogActions.ShowBardDialog();
            }
            else
            {
                var castResult = _spellCastValidationActions.ValidateSpellCast(spellId);

                switch (castResult)
                {
                    case SpellCastValidationResult.ExhaustedNoTp:
                        _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, EOResourceID.ATTACK_YOU_ARE_EXHAUSTED_TP);
                        break;
                    case SpellCastValidationResult.ExhaustedNoSp:
                        _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, EOResourceID.ATTACK_YOU_ARE_EXHAUSTED_SP);
                        break;
                    case SpellCastValidationResult.NotMemberOfGroup:
                        _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_WARNING, EOResourceID.SPELL_ONLY_WORKS_ON_GROUP);
                        break;
                    case SpellCastValidationResult.Frozen:
                        // no-op
                        break;
                    default:
                        _statusLabelSetter.SetStatusLabel(EOResourceID.SKILLMASTER_WORD_SPELL, $"{spellData.Name} ", EOResourceID.SPELL_WAS_SELECTED);
                        if (spellData.Target == SpellTarget.Normal)
                        {
                            // Set up spell slot repository for target selection
                            _spellSlotDataRepository.PreparedMacroSpellId = Optional.Option.Some(spellId);
                            _spellSlotDataRepository.SelectedSpellSlot = Optional.Option.None<int>();
                            _spellSlotDataRepository.SpellIsPrepared = true;
                            _sfxPlayer.PlaySfx(SoundEffectID.SpellActivate);
                        }
                        else if (_characterAnimationActions.PrepareMainCharacterSpell(spellId, _characterProvider.MainCharacter))
                        {
                            _characterActions.PrepareCastSpell(spellId);
                        }
                        break;
                }
            }
        }

        public bool Sit()
        {
            if (_characterProvider.MainCharacter.RenderProperties.IsActing(CharacterActionState.Walking, CharacterActionState.Attacking, CharacterActionState.SpellCast))
                return false;

            var cursorRenderer = _hudControlProvider.GetComponent<IMapRenderer>(HudControlIdentifier.MapRenderer);
            _characterActions.Sit(cursorRenderer.GridCoordinates);

            return true;
        }

        public bool RefreshMapState()
        {
            _mapActions.RequestRefresh();
            return true;
        }
    }

    public interface IFunctionKeyController
    {
        bool SelectSpell(int index, bool isAlternate);

        bool Sit();

        bool RefreshMapState();
    }
}

