using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Shared.Messaging.SubscriberQueue.Interface;

namespace Shared.Messaging.SubscriberQueue;

internal sealed class SubscriberQueue : ISubscriberQueue, IDisposable
{
    private readonly IConnection _conn;
    private readonly IModel
}
