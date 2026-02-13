using System.Threading.Tasks;
using AutomaticTypeMapper;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace EOLib.Net.Communication
{
    [AutoMappedType]
    public class PacketSendService : IPacketSendService
    {
        private readonly INetworkClientProvider _networkClientProvider;
        private readonly IPacketQueueProvider _packetQueueProvider;

        public PacketSendService(INetworkClientProvider networkClientProvider,
                                 IPacketQueueProvider packetQueueProvider)
        {
            _networkClientProvider = networkClientProvider;
            _packetQueueProvider = packetQueueProvider;
        }

        public void SendPacket(IPacket packet)
        {
            var client = Client;
            if (client == null)
                return;

            var bytes = client.Send(packet);
            if (bytes == 0)
                throw new NoDataSentException();
        }

        public async Task SendPacketAsync(IPacket packet)
        {
            var client = Client;
            if (client == null)
                return;

            var bytes = await client.SendAsync(packet);
            if (bytes == 0)
                throw new NoDataSentException();
        }

        public async Task<IPacket> SendRawPacketAndWaitAsync(IPacket packet)
        {
            var client = Client;
            if (client == null)
                return null;

            var bytes = await client.SendRawPacketAsync(packet);
            if (bytes == 0)
                throw new NoDataSentException();

            return await InBandQueue.WaitForPacketAndDequeue((int)client.ReceiveTimeout.TotalMilliseconds);
        }

        public async Task<IPacket> SendEncodedPacketAndWaitAsync(IPacket packet)
        {
            var client = Client;
            if (client == null)
                return null;

            var bytes = await client.SendAsync(packet);
            if (bytes == 0)
                throw new NoDataSentException();

            return await InBandQueue.WaitForPacketAndDequeue((int)client.ReceiveTimeout.TotalMilliseconds);
        }

        private INetworkClient Client => _networkClientProvider.NetworkClient;

        private IWaitablePacketQueue InBandQueue => _packetQueueProvider.HandleInBandPacketQueue;
    }
}
