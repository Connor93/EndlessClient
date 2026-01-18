using AutomaticTypeMapper;
using EOLib.Net.Communication;
using EOLib.Shared;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.Net.Handlers
{
    [AutoMappedType(IsSingleton = true)]
    public class OutOfBandPacketHandler : IOutOfBandPacketHandler
    {
        private readonly IPacketQueueProvider _packetQueueProvider;
        private readonly IPacketHandlerFinder _packetHandlerFinder;

        public OutOfBandPacketHandler(IPacketQueueProvider packetQueueProvider,
                                      IPacketHandlerFinder packetHandlerFinder)
        {
            _packetQueueProvider = packetQueueProvider;
            _packetHandlerFinder = packetHandlerFinder;
        }

        public void PollForPacketsAndHandle()
        {
            if (OutOfBandPacketQueue.QueuedPacketCount == 0)
                return;

            if (Constants.OutOfBand_Packets_Handled_Per_Update >= OutOfBandPacketQueue.QueuedPacketCount)
            {
                var packets = OutOfBandPacketQueue.DequeueAllPackets();
                foreach (var nextPacket in packets)
                    FindAndHandlePacket(nextPacket);
            }
            else
            {
                for (int i = 0; i < Constants.OutOfBand_Packets_Handled_Per_Update && OutOfBandPacketQueue.QueuedPacketCount > 0; ++i)
                {
                    var nextPacket = OutOfBandPacketQueue.DequeueFirstPacket();
                    if (!FindAndHandlePacket(nextPacket))
                        i -= 1;
                }
            }
        }

        private bool FindAndHandlePacket(IPacket packet)
        {
            if (!_packetHandlerFinder.HandlerExists(packet.Family, packet.Action))
            {
                if (packet.Family == PacketFamily.Item && packet.Action == PacketAction.Spec)
                    System.Console.WriteLine($"[DEBUG] No handler exists for ITEM_SPEC");
                return false;
            }

            var handler = _packetHandlerFinder.FindHandler(packet.Family, packet.Action);
            if (!handler.CanHandle || !handler.IsHandlerFor(packet))
            {
                if (packet.Family == PacketFamily.Item && packet.Action == PacketAction.Spec)
                {
                    System.Console.WriteLine($"[DEBUG] Handler found but CanHandle={handler.CanHandle}, IsHandlerFor={handler.IsHandlerFor(packet)}");
                    System.Console.WriteLine($"[DEBUG] PacketType: {packet.GetType().FullName}, Assembly: {packet.GetType().Assembly.GetName().Name}");
                    System.Console.WriteLine($"[DEBUG] HandlerType: {handler.GetType().FullName}");
                }
                return false;
            }

            //todo: catch exceptions and log error details
            //      should also exit out of game gracefully as state may be corrupted by not handling packet (i.e. for warp packets)
            return handler.HandlePacket(packet);
        }

        private IPacketQueue OutOfBandPacketQueue => _packetQueueProvider.HandleOutOfBandPacketQueue;
    }

    public interface IOutOfBandPacketHandler
    {
        void PollForPacketsAndHandle();
    }
}
