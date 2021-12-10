using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    public static class MoonMQBuilder
    {
        public static void StartMoonMQ(this IHost app)
        {
            using var scope = app.Services.CreateScope();
            var moonmq = scope.ServiceProvider.GetService<MoonMQ.Core.MoonMQ>();
            moonmq.Start();
        }
    }
}
