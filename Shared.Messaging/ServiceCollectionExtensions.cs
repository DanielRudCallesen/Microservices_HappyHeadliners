using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Messaging.ArticleQueue.Interface;

namespace Shared.Messaging
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddArticleQueue(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IArticleQueue, Shared.Messaging.ArticleQueue.RabbitMqArticleQueue>();
            return services;
        }
    }
}
