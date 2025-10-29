using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using Shared.Messaging.SubscriberQueue.Model;

namespace Shared.Messaging.SubscriberQueue.Interface;

public interface ISubscriberQueue
{
    Task Publish(NewSubscriber message, CancellationToken ct);

    Task Subscribe(string subscriberName, Func<NewSubscriber, PropagationContext, CancellationToken, Task> handler,
        CancellationToken ct);
}

