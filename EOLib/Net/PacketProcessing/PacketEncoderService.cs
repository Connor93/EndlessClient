using System;
using System.Linq;
using AutomaticTypeMapper;
using EOLib.PacketHandlers.Items;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Optional;

namespace EOLib.Net.PacketProcessing
{
    [AutoMappedType]
    public sealed class PacketEncoderService : IPacketEncoderService
    {
        private const string PACKET_NAMESPACE = "Moffat.EndlessOnline.SDK.Protocol.Net.Server";

        private readonly IPacketFactory _packetFactory;

        public PacketEncoderService(IPacketFactoryFactory packetFactoryFactory) => _packetFactory = packetFactoryFactory.Create(PACKET_NAMESPACE);

        public byte[] Encode(IPacket packet, int encodeMultiplier, int sequenceNumber)
        {
            var writer = new EoWriter();
            writer.AddByte((byte)packet.Action);
            writer.AddByte((byte)packet.Family);
            AddSequenceBytes(writer, sequenceNumber);

            packet.Serialize(writer);

            if (encodeMultiplier == 0 || !PacketValidForEncode(packet))
                return writer.ToByteArray();

            var encodedBytes = DataEncrypter.SwapMultiples(writer.ToByteArray(), encodeMultiplier);
            encodedBytes = DataEncrypter.Interleave(encodedBytes);
            encodedBytes = DataEncrypter.FlipMSB(encodedBytes);
            return encodedBytes;
        }

        public Option<IPacket> Decode(byte[] original, int decodeMultiplier)
        {
            var decodedBytes = original;

            if (decodeMultiplier > 0 && PacketValidForDecode(original))
            {
                decodedBytes = DataEncrypter.FlipMSB(decodedBytes);
                decodedBytes = DataEncrypter.Deinterleave(decodedBytes);
                decodedBytes = DataEncrypter.SwapMultiples(decodedBytes, decodeMultiplier);
            }

            // Try SDK factory first
            var result = _packetFactory.Create(decodedBytes);
            if (result.HasValue)
                return result;

            // Fallback for custom packets not in SDK
            return TryCreateCustomPacket(decodedBytes);
        }

        private Option<IPacket> TryCreateCustomPacket(byte[] data)
        {
            if (data.Length < 2)
                return Option.None<IPacket>();

            var action = data[0];
            var family = (PacketFamily)data[1];

            // Handle ITEM family with action 19 (our custom item source packet)
            if (family == PacketFamily.Item && action == 19)
            {
                // Server packet format (after client decodes): action (1), family (1), payload...
                // Server packets don't include sequence bytes - those are only added by client for client→server
                int payloadStart = 2;

                // Create payload slice without header
                var payloadData = new byte[data.Length - payloadStart];
                Array.Copy(data, payloadStart, payloadData, 0, data.Length - payloadStart);

                var reader = new EoReader(payloadData);
                var packet = new ItemSourceResponsePacket();
                packet.Deserialize(reader);
                return Option.Some<IPacket>(packet);
            }

            // Handle NPC family with action 20 (our custom NPC source packet)
            if (family == PacketFamily.Npc && action == 20)
            {
                int payloadStart = 2;

                var payloadData = new byte[data.Length - payloadStart];
                Array.Copy(data, payloadStart, payloadData, 0, data.Length - payloadStart);

                var reader = new EoReader(payloadData);
                var packet = new EOLib.PacketHandlers.Npcs.NpcSourceResponsePacket();
                packet.Deserialize(reader);
                return Option.Some<IPacket>(packet);
            }

            return Option.None<IPacket>();
        }

        private static bool PacketValidForEncode(IPacket pkt)
        {
            return !IsInitPacket((byte)pkt.Family, (byte)pkt.Action);
        }

        private static bool PacketValidForDecode(byte[] data)
        {
            return data.Length >= 2 && !IsInitPacket(data[0], data[1]);
        }

        private static bool IsInitPacket(byte family, byte action)
        {
            return (PacketFamily)family == PacketFamily.Init &&
                   (PacketAction)action == PacketAction.Init;
        }

        private void AddSequenceBytes(EoWriter writer, int seq)
        {
            if (seq >= EoNumericLimits.CHAR_MAX)
                writer.AddShort(seq);
            else
                writer.AddChar(seq);
        }
    }

    public interface IPacketEncoderService
    {
        byte[] Encode(IPacket original, int encodeMultiplier, int sequenceNumber);

        Option<IPacket> Decode(byte[] original, int decodeMultiplier);
    }
}
