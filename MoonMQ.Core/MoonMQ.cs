using Microsoft.Extensions.Logging;

namespace MoonMQ.Core
{
    public class MoonMQ
    {
        private readonly string serverId;

        private readonly ILogger<MoonMQ> logger;

        private readonly Cluster cluster;

        private readonly TimeSpan electionTimeSpan;
        private readonly TimeSpan heartbeatTimeSpan;

        private PeriodicTimer electionTimer;
        private PeriodicTimer heartbeatTimer;
        private CancellationTokenSource electionTokenSource;
        private CancellationTokenSource heartbeatTokenSource;

        private int currentTerm;
        private string? votedFor;
        private string? leaderId;

        private int lastApplied;
        private int commitIndex;
        private readonly IDictionary<string, int> nextIndex;
        private readonly IDictionary<string, int> matchIndex;

        private ServerState state;

        private IList<Record> records;

        public MoonMQ(Cluster cluster, ILogger<MoonMQ> logger)
        {
            ArgumentNullException.ThrowIfNull(serverId, nameof(serverId));
            ArgumentNullException.ThrowIfNull(cluster, nameof(cluster));
            ArgumentNullException.ThrowIfNull(logger, nameof(logger));

            this.cluster = cluster;
            this.logger = logger;

            state = ServerState.Stopped;
            this.serverId = cluster.ServerId;

            electionTimeSpan = GetRandomTimespan();
            heartbeatTimeSpan = TimeSpan.FromMilliseconds(cluster.MinTimerMillis).Divide(2);

            electionTokenSource = new();
            heartbeatTokenSource = new();

            electionTimer = new(electionTimeSpan);
            heartbeatTimer = new(heartbeatTimeSpan);

            records = Enumerable.Empty<Record>().ToList();
            nextIndex = cluster.Peers.ToDictionary(p => p, p => records.LastOrDefault()?.Term ?? 0);
            matchIndex = cluster.Peers.ToDictionary(p => p, p => 0);

            this.logger.LogInformation("{serverId} - New Raft Created", serverId);
        }

        public string Id => serverId;

        public MoonResult RequestVote(int term,
                                      string candidateId,
                                      int lastLogIndex,
                                      int lastLogTerm)
        {
            logger.LogInformation("{serverId} - Vote Requested by {candidate}", serverId, candidateId);

            if (state == ServerState.Stopped) { return new(currentTerm, false); }
            if (term < currentTerm) { return new(currentTerm, false); }

            logger.LogInformation("{serverId} - Accept term {term} from {candidate}", serverId, term, candidateId);

            ResetElectionTimer();
            StopHeartbeatTimer();

            state = ServerState.Follower;
            currentTerm = term;
            votedFor = null;
            leaderId = null;
            bool voteGranted = (votedFor is null || votedFor == candidateId) &&
                    lastLogIndex >= commitIndex &&
                    lastLogTerm >= records.LastOrDefault()?.Term;
            logger.LogInformation("{serverId} - {votedfor}, {candidate}, " +
                                               "{index}, {commit}, " +
                                               "{term}, {current}",
                                  serverId,
                                  votedFor,
                                  candidateId,
                                  lastLogIndex,
                                  commitIndex,
                                  lastLogTerm,
                                  records.LastOrDefault()?.Term);
            if (voteGranted)
            {
                votedFor = candidateId;
                logger.LogInformation("{serverId} - Voted For {candidate}", serverId, candidateId);
            }
            else
            {
                logger.LogInformation("{serverId} - Reequest Vote Rejected {candidate}", serverId, candidateId);
            }

            return new(currentTerm, voteGranted);
        }

        public MoonResult AppendEntries(int term,
                                        string leaderId,
                                        int prevLogIndex,
                                        int prevLogTerm,
                                        Record[] records,
                                        int leaderCommit)
        {
            logger.LogInformation("{serverId} - AppendEntries from {leaderid}", serverId, leaderId);

            if (state == ServerState.Stopped)
            {
                logger.LogInformation("{serverId} - AppendEntries from {leaderid} Rejected. Server is Stopped", serverId, leaderId);
                return new(currentTerm, false);
            }
            if (term < currentTerm)
            {
                logger.LogInformation("{serverId} - AppendEntries from {leaderid} Rejected. Term minor of CurrentTerm", serverId, leaderId);
                return new(currentTerm, false);
            }

            if (records != null &&
                this.records.Count > 0 &&
                this.records[prevLogIndex].Term != prevLogTerm)
            {
                logger.LogInformation("{serverId} - AppendEntries from {leaderid} Rejected. PrevLogTerm doesn't match Term of PrevLogIndex", serverId, leaderId);
                return new(currentTerm, false);
            }

            StopHeartbeatTimer();
            ResetElectionTimer();

            state = ServerState.Follower;
            currentTerm = term;
            votedFor = null;
            this.leaderId = leaderId;

            if (records != null && records.Any())
            {
                this.records = this.records
                                   .Take(records.First().Index)
                                   .Concat(records)
                                   .ToList();
            }
            else
            {
                logger.LogInformation("{serverId} - New Heartbeat Received from {leaderid}", serverId, leaderId);
            }

            if (leaderCommit > commitIndex)
            {
                Record[] toApply = this.records.Skip(commitIndex + 1)
                                               .Take(leaderCommit - commitIndex)
                                               .ToArray();
                if (toApply.Length == 0)
                {
                    logger.LogInformation("{serverId} - AppendEntries from {leaderid} Rejected. No Record to apply", serverId, leaderId);
                    return new(currentTerm, false);
                }

                lastApplied =
                    commitIndex =
                    Math.Min(leaderCommit, this.records.Last().Index);
            }

            logger.LogInformation("{serverId} - AppendEntries from {leaderid} Accepted", serverId, leaderId);
            return new(currentTerm, true);
        }

        public void Start()
        {
            state = ServerState.Follower;
            StartElectionTimer();
            logger.LogInformation("{serverId} - Server Started", serverId);
        }

        public void Stop()
        {
            state = ServerState.Stopped;
            StopElectionTimer();
            StopHeartbeatTimer();
            logger.LogInformation("{serverId} - Server Stopped", serverId);
        }

        private async Task WaitElectionTickAsync()
        {
            while (await electionTimer.WaitForNextTickAsync(electionTokenSource.Token))
            {
                state = ServerState.Candidate;

                logger.LogInformation("{serverId} - Election Timer Timeout", serverId);
                logger.LogInformation("{serverId} - New State {state}", serverId, state);

                logger.LogInformation("{serverId} - Start RequestVoteAsync", serverId);
                await RequestVoteAsync();
                logger.LogInformation("{serverId} - End RequestVoteAsync", serverId);
            }
        }

        private async Task WaitHeartbeatTickAsync()
        {
            while (await heartbeatTimer.WaitForNextTickAsync(heartbeatTokenSource.Token))
            {
                logger.LogInformation("{serverId} - Waiting SendHeartbeatAsync", serverId);
                await SendHeartbeatAsync();
                logger.LogInformation("{serverId} - SendHeartbeatAsync Completed", serverId);
            }
        }

        private async Task RequestVoteAsync()
        {
            currentTerm += 1;
            votedFor = serverId;
            int votes = 1;

            CancellationTokenSource tokenSource = new();
            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = cluster.Peers.Length - 1,
                CancellationToken = tokenSource.Token
            };

            Record last = records.LastOrDefault() ?? Record.Default;
            Task voteTask = Parallel.ForEachAsync(cluster.Peers.Except(new string[] { serverId }), options, async (peer, token) =>
             {
                 MoonResult result = await cluster.RequestVoteAsync(currentTerm,
                                                                    serverId,
                                                                    last.Index,
                                                                    last.Term,
                                                                    peer,
                                                                    token);
                 logger.LogInformation("{serverId} - Received result {result} from {peer}", serverId, result, peer);
                 currentTerm = result.Term;
                 if (result.Success) { Interlocked.Increment(ref votes); }
             });
            logger.LogInformation("{serverId} - Parallel Running", serverId);
            await voteTask.WaitAsync(electionTimeSpan);
            logger.LogInformation("{serverId} - Parallel Completed", serverId);

            if (votes > cluster.MajorityThreshold)
            {
                state = ServerState.Leader;
                leaderId = serverId;
                logger.LogInformation("{serverId} - I'm the New Leader!!", serverId);

                StopElectionTimer();
                ResetHeartbeatTimer();
                _ = SendHeartbeatAsync();
            }
        }

        private async Task SendHeartbeatAsync()
        {
            CancellationTokenSource tokenSource = new();
            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = cluster.Peers.Length,
                CancellationToken = tokenSource.Token
            };

            Record last = records.LastOrDefault() ?? Record.Default;
            Task appendTask = Parallel.ForEachAsync(cluster.Peers.Except(new string[] { serverId }), options, async (peer, token) =>
             {
                 Record[] appendRecord = records.Skip(nextIndex[peer] - 1)
                                                .ToArray();
                 MoonResult result = await cluster.AppendEntriesAsync(currentTerm,
                                                                      serverId,
                                                                      last.Index,
                                                                      last.Term,
                                                                      appendRecord,
                                                                      commitIndex,
                                                                      peer,
                                                                      token);
                 logger.LogInformation("{serverId} - SendHeartbeatAsync Received {result} From {peer}", serverId, result, peer);
                 if (result.Term > currentTerm)
                 {
                     logger.LogInformation("{serverId} - SendHeartbeatAsync Received Term {} greather than CurrentTerm {currentTerm}", serverId, result.Term, currentTerm);
                     tokenSource.Cancel();

                     state = ServerState.Follower;
                     StopHeartbeatTimer();
                     ResetElectionTimer();

                     return;
                 }

                 if (result.Success)
                 {
                     if (appendRecord.Any())
                     {
                         nextIndex[peer] = appendRecord.Last().Index + 1;
                         matchIndex[peer] = appendRecord.Last().Index;
                     }
                 }
                 else
                 {
                     nextIndex[peer] = Math.Max(0, nextIndex[peer] - 1);
                 }
             });
            logger.LogInformation("{serverId} - Waitinng SendHeartbeatAsync to Peers", serverId);
            await appendTask.WaitAsync(tokenSource.Token);
            logger.LogInformation("{serverId} - SendHeartbeatAsync to Peers Completed", serverId);

            for (var c = commitIndex + 1; c < records.Count; c++)
            {
                var replicatedIn = matchIndex.Count(kv => kv.Value >= c);
                if (records[c].Term == currentTerm &&
                    replicatedIn > cluster.MajorityThreshold)
                {
                    commitIndex = c;
                    lastApplied = commitIndex;
                }
            }
        }

        private void StartElectionTimer()
        {
            electionTimer = new(electionTimeSpan);
            electionTokenSource = new();

            _ = WaitElectionTickAsync();
            logger.LogInformation("{serverId} - Election Timer Started", serverId);
        }

        private void StopElectionTimer()
        {
            electionTimer?.Dispose();

            logger.LogInformation("{serverId} - Election Timer Stopped", serverId);
        }

        private void ResetElectionTimer()
        {
            StopElectionTimer();
            StartElectionTimer();
        }

        private void StartHeartbeatTimer()
        {
            heartbeatTimer = new(heartbeatTimeSpan);
            heartbeatTokenSource = new();

            _ = WaitHeartbeatTickAsync();
            logger.LogInformation("{serverId} - Heartbeat Timer Started", serverId);
        }

        private void StopHeartbeatTimer()
        {
            heartbeatTimer?.Dispose();

            logger.LogInformation("{serverId} - Heartbeat Timer Stopped", serverId);
        }

        private void ResetHeartbeatTimer()
        {
            StopHeartbeatTimer();
            StartHeartbeatTimer();
        }

        private TimeSpan GetRandomTimespan()
        {
            Random random = new(DateTime.Now.Millisecond);
            return TimeSpan.FromMilliseconds(random.Next(cluster.MinTimerMillis, cluster.MaxTimerMillis));
        }
    }
}
