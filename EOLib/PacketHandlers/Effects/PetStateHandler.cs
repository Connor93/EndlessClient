using AutomaticTypeMapper;
using EOLib.Domain.Character;
using EOLib.Domain.Login;
using EOLib.Net.Handlers;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.PacketHandlers.Effects
{
    /// <summary>
    /// Handles pet state notifications from the server.
    /// Custom packet: EFFECT family with action 20 (not in SDK).
    /// Data: 1 char - 1 if pet active, 0 if dismissed.
    /// </summary>
    [AutoMappedType]
    public class PetStateHandler : InGameOnlyPacketHandler<PetStatePacket>
    {
        private readonly ICharacterRepository _characterRepository;

        public override PacketFamily Family => PacketFamily.Effect;
        public override PacketAction Action => (PacketAction)20;

        public PetStateHandler(IPlayerInfoProvider playerInfoProvider,
                               ICharacterRepository characterRepository)
            : base(playerInfoProvider)
        {
            _characterRepository = characterRepository;
        }

        public override bool HandlePacket(PetStatePacket packet)
        {
            _characterRepository.HasActivePet = packet.PetActive;
            return true;
        }
    }

    /// <summary>
    /// Custom packet for pet state data.
    /// Server sends EFFECT family, action 20 with 1 char (1=active, 0=inactive).
    /// </summary>
    public class PetStatePacket : IPacket
    {
        public PacketFamily Family => PacketFamily.Effect;
        public PacketAction Action => (PacketAction)20;

        public bool PetActive { get; private set; }

        public void Serialize(EoWriter writer) { }

        public void Deserialize(EoReader reader)
        {
            PetActive = reader.GetChar() == 1;
        }
    }
}
