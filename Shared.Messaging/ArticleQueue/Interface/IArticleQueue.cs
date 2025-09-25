using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using Shared.Messaging.ArticleQueue.Model;

namespace Shared.Messaging.ArticleQueue.Interface
{
    public interface IArticleQueue
    {
        Task PublishAsync(PublishedArticle message, CancellationToken ct);

        Task SubscribeAsync(string subscriberName,
            Func<PublishedArticle, PropagationContext, CancellationToken, Task> handler, CancellationToken ct);
    }
}
