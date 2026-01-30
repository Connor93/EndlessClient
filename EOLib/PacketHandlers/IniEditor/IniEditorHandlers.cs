using System.Collections.Generic;
using AutomaticTypeMapper;
using EOLib.Domain.IniEditor;
using EOLib.Domain.Login;
using EOLib.Domain.Notifiers;
using EOLib.Net.Handlers;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.PacketHandlers.IniEditor
{
    /// <summary>
    /// Handle AdminInteract+List response containing INI file lists.
    /// Note: This handler is NOT auto-registered because it uses the same
    /// Family/Action as AdminInteractList. The INI editor responses are
    /// handled via raw packet processing in PacketEncoderService.
    /// </summary>
    public class IniEditorListHandler : InGameOnlyPacketHandler<IniEditorListResponsePacket>
    {
        private readonly IIniEditorRepository _iniEditorRepository;
        private readonly IEnumerable<IIniEditorNotifier> _iniEditorNotifiers;

        public override PacketFamily Family => PacketFamily.AdminInteract;
        public override PacketAction Action => PacketAction.List;

        public IniEditorListHandler(IPlayerInfoProvider playerInfoProvider,
                                   IIniEditorRepository iniEditorRepository,
                                   IEnumerable<IIniEditorNotifier> iniEditorNotifiers)
            : base(playerInfoProvider)
        {
            _iniEditorRepository = iniEditorRepository;
            _iniEditorNotifiers = iniEditorNotifiers;
        }

        public override bool HandlePacket(IniEditorListResponsePacket packet)
        {
            _iniEditorRepository.ConfigFiles = new List<string>(packet.ConfigFiles);
            _iniEditorRepository.DataFiles = new List<string>(packet.DataFiles);

            foreach (var notifier in _iniEditorNotifiers)
            {
                notifier.NotifyIniFileListReceived(packet.ConfigFiles, packet.DataFiles);
            }

            return true;
        }
    }

    /// <summary>
    /// Handle AdminInteract+Spec response containing INI file content.
    /// </summary>
    [AutoMappedType]
    public class IniEditorOpenHandler : InGameOnlyPacketHandler<IniEditorOpenResponsePacket>
    {
        private readonly IIniEditorRepository _iniEditorRepository;
        private readonly IEnumerable<IIniEditorNotifier> _iniEditorNotifiers;

        public override PacketFamily Family => PacketFamily.AdminInteract;
        public override PacketAction Action => PacketAction.Spec;

        public IniEditorOpenHandler(IPlayerInfoProvider playerInfoProvider,
                                    IIniEditorRepository iniEditorRepository,
                                    IEnumerable<IIniEditorNotifier> iniEditorNotifiers)
            : base(playerInfoProvider)
        {
            _iniEditorRepository = iniEditorRepository;
            _iniEditorNotifiers = iniEditorNotifiers;
        }

        public override bool HandlePacket(IniEditorOpenResponsePacket packet)
        {
            if (!packet.Success)
                return true;

            _iniEditorRepository.CurrentDirType = packet.DirType;
            _iniEditorRepository.CurrentFilename = packet.Filename;
            _iniEditorRepository.CurrentContent = packet.Content;

            foreach (var notifier in _iniEditorNotifiers)
            {
                notifier.NotifyIniFileContentReceived(packet.DirType, packet.Filename, packet.Content);
            }

            return true;
        }
    }

    /// <summary>
    /// Handle AdminInteract+Create (MSG on server) response for save result.
    /// </summary>
    [AutoMappedType]
    public class IniEditorSaveHandler : InGameOnlyPacketHandler<IniEditorSaveResponsePacket>
    {
        private readonly IEnumerable<IIniEditorNotifier> _iniEditorNotifiers;

        public override PacketFamily Family => PacketFamily.AdminInteract;
        public override PacketAction Action => PacketAction.Msg;

        public IniEditorSaveHandler(IPlayerInfoProvider playerInfoProvider,
                                    IEnumerable<IIniEditorNotifier> iniEditorNotifiers)
            : base(playerInfoProvider)
        {
            _iniEditorNotifiers = iniEditorNotifiers;
        }

        public override bool HandlePacket(IniEditorSaveResponsePacket packet)
        {
            foreach (var notifier in _iniEditorNotifiers)
            {
                notifier.NotifyIniFileSaveResult(packet.Success, packet.Message);
            }

            return true;
        }
    }

    // Response packet classes for deserialization

    public class IniEditorListResponsePacket : IPacket
    {
        public PacketFamily Family => PacketFamily.AdminInteract;
        public PacketAction Action => PacketAction.List;

        public List<string> ConfigFiles { get; } = new List<string>();
        public List<string> DataFiles { get; } = new List<string>();

        public void Serialize(EoWriter writer) { }

        public void Deserialize(EoReader reader)
        {
            reader.ChunkedReadingMode = true;

            var configCount = reader.GetChar();
            for (int i = 0; i < configCount; i++)
            {
                ConfigFiles.Add(reader.GetString());
                reader.NextChunk();
            }

            var dataCount = reader.GetChar();
            for (int i = 0; i < dataCount; i++)
            {
                DataFiles.Add(reader.GetString());
                reader.NextChunk();
            }

            reader.ChunkedReadingMode = false;
        }
    }

    public class IniEditorOpenResponsePacket : IPacket
    {
        public PacketFamily Family => PacketFamily.AdminInteract;
        public PacketAction Action => PacketAction.Spec;

        public bool Success { get; set; }
        public int DirType { get; set; }
        public string Filename { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        public void Serialize(EoWriter writer) { }

        public void Deserialize(EoReader reader)
        {
            Success = reader.GetChar() == 1;
            if (Success)
            {
                reader.ChunkedReadingMode = true;
                DirType = reader.GetChar();
                Filename = reader.GetString();
                reader.NextChunk();
                // Rest of the data is the file content (no 0xFF delimiter at end)
                reader.ChunkedReadingMode = false;
                Content = reader.GetString();
            }
        }
    }

    public class IniEditorSaveResponsePacket : IPacket
    {
        public PacketFamily Family => PacketFamily.AdminInteract;
        public PacketAction Action => PacketAction.Msg;

        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public void Serialize(EoWriter writer) { }

        public void Deserialize(EoReader reader)
        {
            Success = reader.GetChar() == 1;
            reader.ChunkedReadingMode = true;
            Message = reader.GetString();
            reader.ChunkedReadingMode = false;
        }
    }
}
