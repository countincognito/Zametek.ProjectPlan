using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zametek.Client.ProjectPlan.Wpf
{

    public static class PrismExtensions
    {
        public static void Invoke(this IEventAggregator events, Action action, ThreadOption threadOption = ThreadOption.UIThread)
        {
            if (action == null)
                return;

            var e = events?.GetEvent<InvokeMessage>() ?? throw new ArgumentNullException(nameof(events));
            ExceptionDispatchInfo exception = null;

            using (var waitHandle = new ManualResetEventSlim())
            {
                Action<InvokeMessage> handleMessage = msg =>
                {
                    try
                    {
                        msg.Action();
                    }
                    catch (Exception ex)
                    {
                        exception = ExceptionDispatchInfo.Capture(ex);
                    }
                    finally
                    {
                        waitHandle.Set();
                    }
                };

                using (e.Subscribe(handleMessage, threadOption))
                {
                    e.Publish(new InvokeMessage { Action = action });
                    waitHandle.Wait();
                    exception?.Throw();
                }
            }
        }


        private class InvokeMessage : PubSubEvent<InvokeMessage>
        {
            public Action Action { get; set; }
        }
    }
}
