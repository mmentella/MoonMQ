using Grpc.Net.Client;

namespace MoonMQ.Core
{
    public record class Cluster
        : IDisposable
    {
        private readonly IDictionary<string, GrpcChannel> peerChannels;
        private bool disposed = false;

        public Cluster(string serverId, int minTimerMillis, int maxTimerMillis, string[] peers)
        {
            ArgumentNullException.ThrowIfNull(peers, nameof(peers));

            ServerId = serverId;
            MinTimerMillis = minTimerMillis;
            MaxTimerMillis = maxTimerMillis;
            Peers = peers;

            peerChannels = new Dictionary<string, GrpcChannel>();
        }

        public string ServerId { get; }
        public int MinTimerMillis { get; }
        public int MaxTimerMillis { get; }
        public string[] Peers { get; }

        public int MajorityThreshold => (int)(0.5 * Peers.Length);

        public void Dispose()
        {
            if (disposed) { return; }
            foreach (var pc in peerChannels)
            {
                pc.Value.Dispose();
            }

            disposed = true;
        }

        internal Task<MoonResult> RequestVoteAsync(int currentTerm,
                                                   string serverId,
                                                   int index,
                                                   int term,
                                                   string peer,
                                                   CancellationToken token)
        {
            if (disposed) { throw new ObjectDisposedException(nameof(Cluster)); }

            if (!peerChannels.TryGetValue(peer, out var channel))
            {
                channel = GrpcChannel.ForAddress(peer);
                peerChannels.Add(peer, channel);
            }
            var client = new MoonMQService.MoonMQServiceClient(channel);

            var message = new RequestVoteMessage
            {
                Term = currentTerm,
                CandidateId = serverId,
                LastLogIndex = index,
                LastLogTerm = term
            };

            var call = client.RequestVoteAsync(message, cancellationToken: token);

            return call.ResponseAsync;
        }

        internal Task<MoonResult> AppendEntriesAsync(int currentTerm,
                                                     string serverId,
                                                     int index,
                                                     int term,
                                                     Record[] appendRecord,
                                                     int commitIndex,
                                                     string peer,
                                                     CancellationToken token)
        {
            if (disposed) { throw new ObjectDisposedException(nameof(Cluster)); }

            if (!peerChannels.TryGetValue(peer, out var channel))
            {
                channel = GrpcChannel.ForAddress(peer);
                peerChannels.Add(peer, channel);
            }
            var client = new MoonMQService.MoonMQServiceClient(channel);

            var message = new AppendEntriesMessage
            {
                LeaderCommit = commitIndex,
                LeaderId = serverId,
                PrevLogIndex = index,
                PrevLogTerm = term
            };
            message.Records.AddRange(appendRecord);

            var call = client.AppendEntriesAsync(message, cancellationToken: token);

            return call.ResponseAsync;
        }
    }
}
