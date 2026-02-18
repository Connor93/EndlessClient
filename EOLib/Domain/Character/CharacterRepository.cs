using AutomaticTypeMapper;

namespace EOLib.Domain.Character
{
    public interface ICharacterRepository
    {
        bool HasAvatar { get; set; }

        bool HasActivePet { get; set; }

        Character MainCharacter { get; set; }
    }

    public interface ICharacterProvider
    {
        bool HasAvatar { get; }

        bool HasActivePet { get; }

        Character MainCharacter { get; }
    }

    [AutoMappedType(IsSingleton = true)]
    public class CharacterRepository : ICharacterRepository, ICharacterProvider
    {
        public bool HasAvatar { get; set; } = true;

        public bool HasActivePet { get; set; }

        public Character MainCharacter { get; set; }
    }
}
