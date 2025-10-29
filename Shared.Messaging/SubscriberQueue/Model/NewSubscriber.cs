using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Messaging.SubscriberQueue.Model;

public sealed record NewSubscriber(string Email, string? Name, DateTimeOffset SubscribedAt);


