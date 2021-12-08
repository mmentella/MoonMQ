using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoonMQ.Core;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MoonMQHosting
    {
        public static IServiceCollection AddMoonMQ(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<Cluster>(configuration.GetSection(nameof(Cluster)));
            services.AddSingleton(sp =>
            {
                IOptions<Cluster>? options = sp.GetService<IOptions<Cluster>>();
                ILogger<MoonMQ.Core.MoonMQ>? logger = sp.GetService<ILogger<MoonMQ.Core.MoonMQ>>();

                MoonMQ.Core.MoonMQ moonmq = new(options.Value, logger);

                return moonmq;
            });
            return services;
        }
    }
}
