using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    public class Highlow : SpamStrategy
    {
        private TaskCompletionSource<object> _tcs;
        private ChannelControl _control;
        private Task _workTask;

        public Highlow()
        {
            Command = "pls highlow";
            Interval = 30000;
        }

        public override void OnMessageReceive(object sender, MessageReceiveEventArgs args)
        {
            var message = args.Message;
            var embeds = message["embeds"];
            foreach (var embed in embeds)
            {
                var title = embed["author"]?.Value<string>("name");
                // Freaking double messages fucks this up!
                if (title != null)
                {
                    Console.Error.WriteLine($"[Debug]: {title}");
                    if (title.Contains("high-low game"))
                    {
                        if (title.Contains("winning"))
                        {
                            Console.Error.WriteLine("[Debug]: Won high-low game.");
                            _tcs.TrySetResult(null);
                        }
                        else if (title.Contains("losing"))
                        {
                            Console.Error.WriteLine("[Debug]: Losing high-low game.");
                            _tcs.TrySetResult(null);
                        }
                        else
                        {
                            Console.Error.WriteLine("[Debug]: Starting high-low game");
                            _workTask = _control.SendMessageAsync("high");
                        }
                    }
                }
            }
        }

        protected override async Task RunInternal(ChannelControl control)
        {
            _control = control;
            Console.Error.WriteLine("[Debug]: Highlow running...");
            _tcs = new TaskCompletionSource<object>();
            _control.MessageReceived += OnMessageReceive;
            await _control.SendMessageAsync(Command);
            Console.Error.WriteLine("[Debug]: Sent highlow command");
            await _tcs.Task;
            await _workTask;
        }
    }
}
