using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Shared.Messaging.ArticleQueue.Interface;

namespace Shared.Messaging.ArticleQueue
{
    internal sealed class RabbitMqArticleQueue(IConfiguration config, ILogger<RabbitMqArticleQueue> logger) : IArticleQueue
    {
    }
}
