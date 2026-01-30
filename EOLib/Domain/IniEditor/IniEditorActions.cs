using AutomaticTypeMapper;
using EOLib.Net.Communication;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.Domain.IniEditor
{
    [AutoMappedType]
    public class IniEditorActions : IIniEditorActions
    {
        private readonly IPacketSendService _packetSendService;

        public IniEditorActions(IPacketSendService packetSendService)
        {
            _packetSendService = packetSendService;
        }

        public void RequestFileList()
        {
            var packet = new IniEditorListRequestPacket();
            _packetSendService.SendPacket(packet);
        }

        public void RequestFileContent(int dirType, string filename)
        {
            var packet = new IniEditorOpenRequestPacket
            {
                DirType = dirType,
                Filename = filename
            };
            _packetSendService.SendPacket(packet);
        }

        public void SaveFileContent(int dirType, string filename, string content)
        {
            var packet = new IniEditorSaveRequestPacket
            {
                DirType = dirType,
                Filename = filename,
                Content = content
            };
            _packetSendService.SendPacket(packet);
        }
    }

    public interface IIniEditorActions
    {
        void RequestFileList();
        void RequestFileContent(int dirType, string filename);
        void SaveFileContent(int dirType, string filename, string content);
    }

    /// <summary>
    /// Request list of INI files from server
    /// AdminInteract + List
    /// </summary>
    public class IniEditorListRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.AdminInteract;
        public PacketAction Action => PacketAction.List;

        public void Serialize(EoWriter writer)
        {
            // No additional data needed - server just returns file lists
        }

        public void Deserialize(EoReader reader)
        {
            // Client packet - not received by client
        }
    }

    /// <summary>
    /// Request INI file content from server
    /// AdminInteract + Spec
    /// </summary>
    public class IniEditorOpenRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.AdminInteract;
        public PacketAction Action => PacketAction.Spec;

        public int DirType { get; set; }  // 0 = config, 1 = data
        public string Filename { get; set; }

        public void Serialize(EoWriter writer)
        {
            writer.AddChar(DirType);
            writer.AddString(Filename);
        }

        public void Deserialize(EoReader reader)
        {
            // Client packet - not received by client
        }
    }

    /// <summary>
    /// Save INI file content to server
    /// AdminInteract + Create
    /// </summary>
    public class IniEditorSaveRequestPacket : IPacket
    {
        public PacketFamily Family => PacketFamily.AdminInteract;
        public PacketAction Action => PacketAction.Create;

        public int DirType { get; set; }  // 0 = config, 1 = data
        public string Filename { get; set; }
        public string Content { get; set; }

        public void Serialize(EoWriter writer)
        {
            writer.AddChar(DirType);
            writer.AddString(Filename);
            writer.AddByte(0xFF);  // Break string delimiter
            writer.AddString(Content);
        }

        public void Deserialize(EoReader reader)
        {
            // Client packet - not received by client
        }
    }
}
