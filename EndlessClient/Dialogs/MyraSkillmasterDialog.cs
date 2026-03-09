using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EndlessClient.Dialogs.Factories;
using EndlessClient.HUD;
using EndlessClient.UI.Myra;
using EOLib.Domain.Character;
using EOLib.Domain.Interact.Skill;
using EOLib.IO.Repositories;
using EOLib.Localization;
using EOLib.Shared;
using Microsoft.Xna.Framework;
using Optional.Collections;
using XNAControls;

namespace EndlessClient.Dialogs
{
    /// <summary>
    /// Native Myra replacement for the XNA SkillmasterDialog.
    /// Stateful dialog with Initial/Learn/Forget/ForgetAll states.
    /// </summary>
    public class MyraSkillmasterDialog : MyraScrollingListDialog
    {
        private enum SkillState
        {
            Initial,
            Learn,
            Forget,
            ForgetAll
        }

        private SkillState _state;
        private bool _showingRequirements;
        private EventHandler _backHandler;

        private HashSet<Skill> _cachedSkills;
        private HashSet<InventorySpell> _cachedSpells;
        private string _cachedTitle;

        private readonly ISkillmasterActions _skillmasterActions;
        private readonly ILocalizedStringFinder _localizedStringFinder;
        private readonly IStatusLabelSetter _statusLabelSetter;
        private readonly IEOMessageBoxFactory _messageBoxFactory;
        private readonly ITextInputDialogFactory _textInputDialogFactory;
        private readonly ISkillDataProvider _skillDataProvider;
        private readonly ICharacterProvider _characterProvider;
        private readonly ICharacterInventoryProvider _characterInventoryProvider;
        private readonly IPubFileProvider _pubFileProvider;

        public MyraSkillmasterDialog(
            IMyraUIManager uiManager,
            IMyraFontProvider fontProvider,
            ISkillmasterActions skillmasterActions,
            ILocalizedStringFinder localizedStringFinder,
            IStatusLabelSetter statusLabelSetter,
            IEOMessageBoxFactory messageBoxFactory,
            ITextInputDialogFactory textInputDialogFactory,
            ISkillDataProvider skillDataProvider,
            ICharacterProvider characterProvider,
            ICharacterInventoryProvider characterInventoryProvider,
            IPubFileProvider pubFileProvider)
            : base(uiManager, fontProvider, skillDataProvider.Title, width: 320, height: 280)
        {
            _skillmasterActions = skillmasterActions;
            _localizedStringFinder = localizedStringFinder;
            _statusLabelSetter = statusLabelSetter;
            _messageBoxFactory = messageBoxFactory;
            _textInputDialogFactory = textInputDialogFactory;
            _skillDataProvider = skillDataProvider;
            _characterProvider = characterProvider;
            _characterInventoryProvider = characterInventoryProvider;
            _pubFileProvider = pubFileProvider;

            _cachedSkills = new HashSet<Skill>();
            _cachedSpells = new HashSet<InventorySpell>();
            _cachedTitle = string.Empty;

            SetState(SkillState.Initial, regen: true);
        }

        public override void Update(GameTime gameTime)
        {
            if (_cachedTitle != _skillDataProvider.Title)
            {
                Window.Title = _cachedTitle = _skillDataProvider.Title;
            }

            if (!_cachedSkills.SetEquals(_skillDataProvider.Skills))
            {
                _cachedSkills = _skillDataProvider.Skills.ToHashSet();
                SetState(_state, regen: true);
            }

            if (!_cachedSpells.SetEquals(_characterInventoryProvider.SpellInventory))
            {
                _cachedSpells = _characterInventoryProvider.SpellInventory.ToHashSet();
                SetState(_state, regen: true);
            }

            base.Update(gameTime);
        }

        private void BackClicked()
        {
            if (_state == SkillState.Learn && _showingRequirements)
            {
                SetState(SkillState.Learn, regen: true);
                _showingRequirements = false;
            }
            else
            {
                SetState(SkillState.Initial);
            }
        }

        private void SetState(SkillState newState, bool regen = false)
        {
            SkillState old = _state;

            if (old == newState && !regen)
                return;

            int numToLearn = _cachedSkills.Count(x => !_cachedSpells.Any(si => si.ID == x.Id));
            int numToForget = _cachedSpells.Count;

            ClearItems();

            if (newState == SkillState.Learn && numToLearn == 0)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SKILL_NOTHING_MORE_TO_LEARN);
                dlg.ShowDialog();
                return;
            }

            switch (newState)
            {
                case SkillState.Initial:
                    {
                        string learnNum = $"{numToLearn}{_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_ITEMS_TO_LEARN)}";
                        string forgetNum = $"{numToForget}{_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_ITEMS_LEARNED)}";

                        AddItem(
                            _localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_LEARN),
                            learnNum,
                            onClick: _ => SetState(SkillState.Learn),
                            isLink: true);

                        AddItem(
                            _localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_FORGET),
                            forgetNum,
                            onClick: _ => SetState(SkillState.Forget),
                            isLink: true);

                        AddItem(
                            _localizedStringFinder.GetString(EOResourceID.SKILLMASTER_FORGET_ALL),
                            _localizedStringFinder.GetString(EOResourceID.SKILLMASTER_RESET_YOUR_CHARACTER),
                            onClick: _ => SetState(SkillState.ForgetAll),
                            isLink: true);

                        SetupButtons(showOk: false, showCancel: true);
                        UnsubscribeBack();
                    }
                    break;

                case SkillState.Learn:
                    {
                        foreach (var skill in _cachedSkills.Where(x => !_cachedSpells.Any(y => y.ID == x.Id)))
                        {
                            var skillRef = skill;
                            var spellData = _pubFileProvider.ESFFile[skill.Id];

                            var item = AddItem(
                                spellData.Name,
                                _localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_REQUIREMENTS),
                                onClick: _ => ShowRequirements(skillRef),
                                isLink: true);

                            // Show requirements summary in the status bar on hover
                            item.Widget.MouseEntered += (_, _) => ShowRequirementsLabel(skillRef);
                        }

                        SetupButtons(showOk: false, showCancel: true, showBack: true);
                        SubscribeBack();
                    }
                    break;

                case SkillState.Forget:
                    {
                        var input = _textInputDialogFactory.Create(
                            _localizedStringFinder.GetString(DialogResourceID.SKILL_PROMPT_TO_FORGET), 32);

                        input.DialogClosing += (_, args) =>
                        {
                            if (args.Result == XNADialogResult.Cancel)
                                return;

                            _cachedSpells.SingleOrNone(s =>
                                string.Equals(_pubFileProvider.ESFFile[s.ID].Name, input.ResponseText, StringComparison.OrdinalIgnoreCase))
                                .Match(
                                    some: si => _skillmasterActions.ForgetSkill(si.ID),
                                    none: () =>
                                    {
                                        args.Cancel = true;
                                        var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SKILL_FORGET_ERROR_NOT_LEARNED);
                                        dlg.ShowDialog();
                                    });
                        };

                        input.ShowDialog();

                        // Show initial info behind the popup
                        newState = SkillState.Initial;
                        goto case SkillState.Initial;
                    }

                case SkillState.ForgetAll:
                    {
                        // Add multi-line text as individual items
                        AddItem(_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_FORGET_ALL));
                        AddItem(_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_FORGET_ALL_MSG_1));
                        AddItem(_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_FORGET_ALL_MSG_2));
                        AddItem(_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_FORGET_ALL_MSG_3));
                        AddItem(
                            _localizedStringFinder.GetString(EOResourceID.SKILLMASTER_CLICK_HERE_TO_FORGET_ALL),
                            onClick: _ => ConfirmResetCharacter(),
                            isLink: true);

                        SetupButtons(showOk: false, showCancel: true, showBack: true);
                        SubscribeBack();
                    }
                    break;
            }

            _state = newState;
        }

        private void Learn(Skill skill)
        {
            bool skillReqsMet = true;
            foreach (var req in skill.SkillRequirements.Where(x => x > 0))
            {
                if (!_characterInventoryProvider.SpellInventory.Any(s => s.ID == req))
                {
                    skillReqsMet = false;
                    break;
                }
            }

            var stats = _characterProvider.MainCharacter.Stats;

            if (!skillReqsMet ||
                stats[CharacterStat.Strength] < skill.StrRequirement || stats[CharacterStat.Intelligence] < skill.IntRequirement || stats[CharacterStat.Wisdom] < skill.WisRequirement ||
                stats[CharacterStat.Agility] < skill.AgiRequirement || stats[CharacterStat.Constitution] < skill.ConRequirement || stats[CharacterStat.Charisma] < skill.ChaRequirement ||
                stats[CharacterStat.Level] < skill.LevelRequirement || !_characterInventoryProvider.ItemInventory.SingleOrNone(x => x.ItemID == 1 && x.Amount >= skill.GoldRequirement).HasValue)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SKILL_LEARN_REQS_NOT_MET);
                dlg.ShowDialog();
            }
            else if (skill.ClassRequirement > 0 && _characterProvider.MainCharacter.ClassID != skill.ClassRequirement)
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SKILL_LEARN_WRONG_CLASS, $" {_pubFileProvider.ECFFile[skill.ClassRequirement].Name}!");
                dlg.ShowDialog();
            }
            else
            {
                var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SKILL_LEARN_CONFIRMATION, $" {_pubFileProvider.ESFFile[skill.Id].Name}?", EODialogButtons.OkCancel);
                dlg.DialogClosing += (_, e) =>
                {
                    if (e.Result == XNADialogResult.OK)
                        _skillmasterActions.LearnSkill(skill.Id);
                };
                dlg.ShowDialog();
            }
        }

        private void ShowRequirementsLabel(Skill skill)
        {
            var full = new StringBuilder();

            full.Append($"{_pubFileProvider.ESFFile[skill.Id].Name} {skill.LevelRequirement} LVL, ");

            if (skill.StrRequirement > 0)
                full.Append($"{skill.StrRequirement} STR, ");
            if (skill.IntRequirement > 0)
                full.Append($"{skill.IntRequirement} INT, ");
            if (skill.WisRequirement > 0)
                full.Append($"{skill.WisRequirement} WIS, ");
            if (skill.AgiRequirement > 0)
                full.Append($"{skill.AgiRequirement} AGI, ");
            if (skill.ConRequirement > 0)
                full.Append($"{skill.ConRequirement} CON, ");
            if (skill.ChaRequirement > 0)
                full.Append($"{skill.ChaRequirement} CHA, ");
            if (skill.GoldRequirement > 0)
                full.Append($"{skill.GoldRequirement} {_pubFileProvider.EIFFile[1].Name}");
            if (skill.ClassRequirement > 0)
                full.Append($", {_pubFileProvider.ECFFile[skill.ClassRequirement].Name}");

            _statusLabelSetter.SetStatusLabel(EOResourceID.STATUS_LABEL_TYPE_INFORMATION, full.ToString());
        }

        private void ShowRequirements(Skill skill)
        {
            _showingRequirements = true;

            ClearItems();

            var spellName = _pubFileProvider.ESFFile[skill.Id].Name;
            var header = skill.ClassRequirement > 0
                ? $"{spellName} [{_pubFileProvider.ECFFile[skill.ClassRequirement].Name}]"
                : spellName;

            AddItem(header);
            AddItem(" ");

            // Prerequisite skills
            var skillReqs = skill.SkillRequirements.Where(x => x != 0).ToList();
            if (skillReqs.Any())
            {
                foreach (var req in skillReqs)
                {
                    AddItem($"{_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_SKILL)}: {_pubFileProvider.ESFFile[req].Name}");
                }
                AddItem(" ");
            }

            // Stat requirements
            if (skill.StrRequirement > 0)
                AddItem($"{skill.StrRequirement} {_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_STRENGTH)}");
            if (skill.IntRequirement > 0)
                AddItem($"{skill.IntRequirement} {_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_INTELLIGENCE)}");
            if (skill.WisRequirement > 0)
                AddItem($"{skill.WisRequirement} {_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_WISDOM)}");
            if (skill.AgiRequirement > 0)
                AddItem($"{skill.AgiRequirement} {_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_AGILITY)}");
            if (skill.ConRequirement > 0)
                AddItem($"{skill.ConRequirement} {_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_CONSTITUTION)}");
            if (skill.ChaRequirement > 0)
                AddItem($"{skill.ChaRequirement} {_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_CHARISMA)}");

            AddItem(" ");
            AddItem($"{skill.LevelRequirement} {_localizedStringFinder.GetString(EOResourceID.SKILLMASTER_WORD_LEVEL)}");
            AddItem($"{skill.GoldRequirement} {_pubFileProvider.EIFFile[1].Name}");

            AddItem(" ");
            AddItem($"Learn {spellName}", onClick: _ => Learn(skill), isLink: true);

            SetupButtons(showOk: false, showCancel: true, showBack: true);
            SubscribeBack();
        }

        private void ConfirmResetCharacter()
        {
            var dlg = _messageBoxFactory.CreateMessageBox(DialogResourceID.SKILL_RESET_CHARACTER_CONFIRMATION, EODialogButtons.OkCancel);
            dlg.DialogClosing += (_, args) =>
            {
                if (args.Result == XNADialogResult.OK)
                {
                    _skillmasterActions.ResetCharacter();
                }
            };
            dlg.ShowDialog();
        }

        private void SubscribeBack()
        {
            UnsubscribeBack();
            _backHandler = (_, _) => BackClicked();
            BackAction += _backHandler;
        }

        private void UnsubscribeBack()
        {
            if (_backHandler != null)
            {
                BackAction -= _backHandler;
                _backHandler = null;
            }
        }
    }
}
