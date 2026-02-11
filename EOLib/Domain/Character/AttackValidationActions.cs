using System.Linq;
using AutomaticTypeMapper;
using EOLib.Domain.Extensions;
using EOLib.Domain.Map;
using EOLib.IO;
using EOLib.IO.Repositories;
using Optional;
using Optional.Collections;

namespace EOLib.Domain.Character
{
    [AutoMappedType]
    public class AttackValidationActions : IAttackValidationActions
    {
        private readonly ICharacterProvider _characterProvider;
        private readonly IMapCellStateProvider _mapCellStateProvider;
        private readonly IEIFFileProvider _eifFileProvider;
        private readonly IENFFileProvider _enfFileProvider;
        private readonly IPaperdollProvider _paperdollProvider;

        public AttackValidationActions(ICharacterProvider characterProvider,
                                       IMapCellStateProvider mapCellStateProvider,
                                       IEIFFileProvider eifFileProvider,
                                       IENFFileProvider enfFileProvider,
                                       IPaperdollProvider paperdollProvider)
        {
            _characterProvider = characterProvider;
            _mapCellStateProvider = mapCellStateProvider;
            _eifFileProvider = eifFileProvider;
            _enfFileProvider = enfFileProvider;
            _paperdollProvider = paperdollProvider;
        }

        public AttackValidationError ValidateCharacterStateBeforeAttacking()
        {
            if (_characterProvider.MainCharacter.Frozen)
                return AttackValidationError.Frozen;

            if (_characterProvider.MainCharacter.Stats[CharacterStat.Weight] >
                _characterProvider.MainCharacter.Stats[CharacterStat.MaxWeight])
                return AttackValidationError.Overweight;

            if (_characterProvider.MainCharacter.Stats[CharacterStat.SP] <= 0)
                return AttackValidationError.Exhausted;

            var rp = _characterProvider.MainCharacter.RenderProperties;
            var mainCharId = _characterProvider.MainCharacter.ID;

            // Use paperdoll data (actual equipped item IDs) when available.
            // This correctly handles glamor gems where render properties show cosmetic overlays
            // instead of the actual equipment.
            var isRangedWeapon = false;
            var hasArrows = false;
            var hasShield = false;

            if (_paperdollProvider.VisibleCharacterPaperdolls.TryGetValue(mainCharId, out var paperdoll))
            {
                if (paperdoll.Paperdoll.TryGetValue(EquipLocation.Weapon, out var weaponId) && weaponId > 0)
                {
                    var weaponRecord = _eifFileProvider.EIFFile[weaponId];
                    isRangedWeapon = weaponRecord.Type == IO.ItemType.Weapon && weaponRecord.SubType == IO.ItemSubType.Ranged;
                }

                if (paperdoll.Paperdoll.TryGetValue(EquipLocation.Shield, out var shieldId) && shieldId > 0)
                {
                    hasShield = true;
                    var shieldRecord = _eifFileProvider.EIFFile[shieldId];
                    hasArrows = shieldRecord.Type == IO.ItemType.Shield && shieldRecord.SubType == IO.ItemSubType.Arrows;
                }
            }
            else
            {
                // Fallback: use render properties when paperdoll data isn't available
                var matchingWeapon = _eifFileProvider.EIFFile
                    .FirstOrNone(x => x.DollGraphic == rp.WeaponGraphic && x.Type == IO.ItemType.Weapon);
                isRangedWeapon = matchingWeapon.Map(x => x.SubType == IO.ItemSubType.Ranged).ValueOr(false);

                hasShield = rp.ShieldGraphic != 0;
                hasArrows = _eifFileProvider.EIFFile
                    .Any(x => x.DollGraphic == rp.ShieldGraphic && x.Type == IO.ItemType.Shield && x.SubType == IO.ItemSubType.Arrows);
            }

            if (isRangedWeapon && (!hasShield || !hasArrows))
                return AttackValidationError.MissingArrows;

            return _mapCellStateProvider
                .GetCellStateAt(rp.GetDestinationX(), rp.GetDestinationY())
                .NPC.Match(
                    some: npc => npc.OpponentID.Match(
                        some: id =>
                        {
                            var notYourBattle = id != _characterProvider.MainCharacter.ID;
                            var isBossNpc = _enfFileProvider.ENFFile[npc.ID].Boss > 0;
                            return notYourBattle && !isBossNpc
                                ? AttackValidationError.NotYourBattle
                                : AttackValidationError.OK;
                        },
                        none: () => AttackValidationError.OK),
                    none: () => AttackValidationError.OK);
        }
    }

    public interface IAttackValidationActions
    {
        AttackValidationError ValidateCharacterStateBeforeAttacking();
    }

    public enum AttackValidationError
    {
        OK,
        Overweight,
        Exhausted,
        NotYourBattle,
        MissingArrows,
        Frozen,
    }
}
