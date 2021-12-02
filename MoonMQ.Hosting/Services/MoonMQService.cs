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
            return Task.FromResult(new Core.MoonResult
            {
                Success = true,
                Term = request.Term,
            });
        }

        public override Task<Core.MoonResult> AppendEntries(Core.AppendEntriesMessage request,
                                                            ServerCallContext context)
        {
            return Task.FromResult(new Core.MoonResult
            {
                Success = true,
                Term = request.Term,
            });
        }
    }
}