//using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using BusFire;

namespace BusFire;
public record EventHandlerExecutor(object HandlerInstance, Func<IEvent, CancellationToken, Task> HandlerCallback);
