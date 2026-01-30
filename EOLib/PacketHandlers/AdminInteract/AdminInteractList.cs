using System.Collections.Generic;
using System.Linq;
using AutomaticTypeMapper;
using EOLib.Domain.Character;
using EOLib.Domain.IniEditor;
using EOLib.Domain.Login;
using EOLib.Domain.Notifiers;
using EOLib.Net.Handlers;
using EOLib.PacketHandlers.IniEditor;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace EOLib.PacketHandlers.AdminInteract
{
    /// <summary>
    /// Response to $inventory &lt;character&gt; command OR INI editor file list response.
    /// Differentiates based on packet type at runtime since both use AdminInteract+List.
    /// Implements IPacketHandler directly to handle polymorphic packet types.
    /// </summary>
    [AutoMappedType]
    public class AdminInteractList : IPacketHandler
    {
        private readonly IPlayerInfoProvider _playerInfoProvider;
        private readonly IEnumerable<IUserInterfaceNotifier> _userInterfaceNotifiers;
        private readonly IIniEditorRepository _iniEditorRepository;
        private readonly IEnumerable<IIniEditorNotifier> _iniEditorNotifiers;

        public PacketFamily Family => PacketFamily.AdminInteract;

        public PacketAction Action => PacketAction.List;

        public bool CanHandle => _playerInfoProvider.PlayerIsInGame;

        public AdminInteractList(IPlayerInfoProvider playerInfoProvider,
                                 IEnumerable<IUserInterfaceNotifier> userInterfaceNotifiers,
                                 IIniEditorRepository iniEditorRepository,
                                 IEnumerable<IIniEditorNotifier> iniEditorNotifiers)
        {
            _playerInfoProvider = playerInfoProvider;
            _userInterfaceNotifiers = userInterfaceNotifiers;
            _iniEditorRepository = iniEditorRepository;
            _iniEditorNotifiers = iniEditorNotifiers;
        }

        public bool IsHandlerFor(IPacket packet)
        {
            // This handler can process either packet type
            return packet is IniEditorListResponsePacket || packet is AdminInteractListServerPacket;
        }

        public bool HandlePacket(IPacket packet)
        {
            // Check if this is an INI editor response
            if (packet is IniEditorListResponsePacket iniPacket)
            {
                _iniEditorRepository.ConfigFiles = new List<string>(iniPacket.ConfigFiles);
                _iniEditorRepository.DataFiles = new List<string>(iniPacket.DataFiles);

                foreach (var notifier in _iniEditorNotifiers)
                {
                    notifier.NotifyIniFileListReceived(iniPacket.ConfigFiles, iniPacket.DataFiles);
                }

                return true;
            }

            // Otherwise, handle as inventory lookup response
            if (packet is AdminInteractListServerPacket invPacket)
            {
                var inventory = invPacket.Inventory.Select(x => new InventoryItem(x.Id, x.Amount)).ToList();
                var bank = invPacket.Bank.Select(x => new InventoryItem(x.Id, x.Amount)).ToList();
                foreach (var notifier in _userInterfaceNotifiers)
                {
                    notifier.NotifyCharacterInventory(invPacket.Name, invPacket.Usage, invPacket.GoldBank, inventory, bank);
                }

                return true;
            }

            return false;
        }
    }
}

