using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    public class Search : SpamStrategy
    {
        private static readonly List<string> _safeSearchLocations = new List<string>{ "bus", "coat", "dresser", "fridge", "grass", "laundromat", "mailbox", "pantry", "pocket", "shoe", "sink", "vacuum", "washer" };

        public Search()
        {
            Command = "pls search";
            Interval = 30000;
        }

        TaskCompletionSource<object> _tcs;
        ChannelControl _control;

        public override async void OnMessageReceive(object sender, MessageReceiveEventArgs args)
        {
            //no-op
            var content = args.Message.Value<string>("content");
            if (content.Contains("Pick from the list below and type the name in chat."))
            {
                var matches = Regex.Matches(content, "`[^`]*`");
                if (matches.Count != 0)
                {
                    foreach (Match match in matches)
                    {
                        var location = match.Value;
                        location = location.Substring(1, location.Length - 2);
                        if (_safeSearchLocations.Contains(location))
                        {
                            await _control.SendMessageAsync(location);
                            _tcs.SetResult(null);
                            return;
                        }
                    }

                    await _control.SendMessageAsync("Dude it ain't safe here.");
                    _tcs.SetResult(null);
                }
            }
        }

        protected override async Task RunInternal(ChannelControl control)
        {
            _control = control;
            _control.MessageReceived += OnMessageReceive;
            _tcs = new TaskCompletionSource<object>();
            Console.Error.WriteLine("[Debug]: Searching...");
            await _control.SendMessageAsync(Command);
            await Task.WhenAny(Task.Delay(60000), _tcs.Task);
        }
    }
}
