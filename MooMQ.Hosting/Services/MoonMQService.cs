using Grpc.Core;
using MooMQ.Hosting;

namespace MooMQ.Hosting.Services
{
    public class MoonMQService
        : MoonMQ.Core.MoonMQService.MoonMQServiceBase
    {
        private readonly ILogger<MoonMQService> logger;
        public MoonMQService(ILogger<MoonMQService> logger)
        {
            this.logger = logger;
        }

        public override Task<MoonMQ.Core.MoonResult> RequestVote(MoonMQ.Core.RequestVoteMessage request,
                                                                 ServerCallContext context)
        {
            return Task.FromResult(new MoonMQ.Core.MoonResult
            {
                Success = true,
                Term = request.Term,
            });
        }

        public override Task<MoonMQ.Core.MoonResult> AppendEntries(MoonMQ.Core.AppendEntriesMessage request,
                                                                 ServerCallContext context)
        {
            return Task.FromResult(new MoonMQ.Core.MoonResult
            {
                Success = true,
                Term = request.Term,
            });
        }
    }
}