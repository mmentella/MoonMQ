using Grpc.Core;

namespace MoonMQ.Hosting.Services
{
    public class MoonMQService
        : Core.MoonMQService.MoonMQServiceBase
    {
        private readonly Core.MoonMQ moonmq;
        private readonly ILogger<MoonMQService> logger;

        public MoonMQService(Core.MoonMQ moonmq, ILogger<MoonMQService> logger)
        {
            this.moonmq = moonmq;
            this.logger = logger;
        }

        public override Task<Core.MoonResult> RequestVote(Core.RequestVoteMessage request,
                                                          ServerCallContext context)
        {
            Core.MoonResult? result = moonmq.RequestVote(request.Term, request.CandidateId, request.LastLogIndex, request.LastLogTerm);

            return Task.FromResult(result);
        }

        public override Task<Core.MoonResult> AppendEntries(Core.AppendEntriesMessage request,
                                                            ServerCallContext context)
        {
            Core.Record[]? records = request.Records.ToArray();
            Core.MoonResult? result = moonmq.AppendEntries(request.Term,
                                                           request.LeaderId,
                                                           request.PrevLogIndex,
                                                           request.PrevLogTerm,
                                                           records,
                                                           request.LeaderCommit);
            return Task.FromResult(result);
        }
    }
}