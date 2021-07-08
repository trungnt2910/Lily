using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lily
{
    public class SpamEngine
    {
        private Channel _channel;
        private List<SpamStrategy> _strategies = new List<SpamStrategy>();
        private List<Task> _promises = new List<Task>();
        private CancellationTokenSource _cts;
        private bool _idle;
        private Random _rand = new Random();
        private HttpClient _client = new HttpClient();

        public SpamEngine(Channel channel, int runSpan, int pauseSpan)
        {
            _channel = channel;
            _channel.MessageReceive += OnMessageReceive;
            _ = StartTimer(runSpan, pauseSpan);
        }

        private async void OnMessageReceive(object sender, MessageReceiveEventArgs args)
        {
            args.Deferral.Lock();
            var content = args.Message.Value<string>("content");
            var matches = Regex.Matches(content, "Type `[^`]*`");
            foreach (Match match in matches)
            {
                var toType = match.Value;
                toType = toType.Substring(6, toType.Length - 7);
                toType = Regex.Replace(toType, @"[^\u0000-\u007F]+", string.Empty);

                Console.Error.WriteLine($"[Debug]: Typing {toType}");
                // Must be RAW.
                await _channel.SendMessageRawAsync(toType);
            }
            if (content.Contains("Attack the boss by typing"))
            {
                matches = Regex.Matches(content, "`[^`]*`");
                foreach (Match match in matches)
                {
                    var toType = match.Value;
                    for (int i = 0; i < 10; ++i) await _channel.SendMessageRawAsync(toType.Substring(1, toType.Length - 2), true);
                }
            }
            // if (content.Contains("unscramble", StringComparison.InvariantCultureIgnoreCase))
            // {
            //     matches = Regex.Matches(content, "`[^`]*`");
            //     foreach (Match match in matches)
            //     {
            //         var toType = match.Value;
            //         toType = toType.Substring(1, toType.Length - 2);
            //         Console.Error.WriteLine($"[Debug]: attempting to unscramble: {toType}");
            //         var httpRequest = new HttpRequestMessage();
            //         httpRequest.RequestUri = new Uri($"https://wordunscrambler.me/unscramble/{toType}");
            //         var response = await _client.SendAsync(httpRequest);
            //         var text = await response.Content.ReadAsStringAsync();
            //         var words = Regex.Matches(text, "data-word=\"[^\"]*\"");
            //         Console.Error.WriteLine($"[Debug]: {words.Count} words found.");
            //         foreach (Match wordMatch in words)
            //         {
            //             var word = wordMatch.Value;
            //             word = word.Substring("data-word=\"".Length, word.Length - 1 - "data-word=\"".Length);
            //             if (word.Length != toType.Length)
            //             {
            //                 continue;
            //             }
            //             Console.Error.WriteLine($"[Debug]: trying {word}");
            //             await _channel.SendMessageRawAsync(word);
            //         }
            //     }
            // }
            if (content.Contains("reverse", StringComparison.InvariantCultureIgnoreCase))
            {
                matches = Regex.Matches(content, "`[^`]*`");
                foreach (Match match in matches)
                {
                    var toType = match.Value;
                    toType = toType.Substring(1, toType.Length - 2);
                    toType = new string(toType.Reverse().ToArray());
                    await _channel.SendMessageRawAsync(toType);
                }
            }
            // We won't have pls search here, it lives in its own strategy.
            // foreach (var strategy in _strategies)
            // {
            //    strategy.OnMessageReceive(sender, args);
            // }
            args.Deferral.Unlock();
        }

        private async Task StartTimer(int run, int pause)
        {
            while (true)
            {
                _cts = new CancellationTokenSource();
                _idle = false;
                lock (_strategies)
                {
                    _promises.Clear();
                    foreach (var strategy in _strategies)
                    {
                        _promises.Add(strategy.Run(_channel, _cts.Token));
                        Console.Error.WriteLine($"[Debug]: Added {strategy.Command}");
                    }
                }
                var runTime = _rand.Next(0, run);
                Console.WriteLine("I'm working now, sit back and relax!");
                Console.Error.WriteLine($"[Debug]: Running for {runTime / 1000.0} seconds...");
                await Task.Delay(runTime);
                _idle = true;
                _cts.Cancel();
                foreach (var promise in _promises) await promise;
                Console.Error.WriteLine("[Debug]: Killed all strategies.");
                var breakTime = _rand.Next(1000000, pause);
                Console.WriteLine($"Dank Memer seems to hate me, I'm hiding for {breakTime / 1000.0} seconds so that he could cool down.");
                Console.Error.WriteLine($"[Debug]: Taking a break for {breakTime / 1000.0} seconds...");
                await Task.Delay(breakTime);
            }
        }

        public void AddStrategy(SpamStrategy strategy)
        {
            if (!strategy.IsLongRunningStrategy)
            {
                lock (_strategies)
                {
                    _strategies.Add(strategy);
                    if (!_idle)
                    {
                        _promises.Add(strategy.Run(_channel, _cts.Token));
                    }
                    Console.Error.WriteLine($"[Debug]: Added {strategy.Command}");
                }
            }
            else
            {
                _ = strategy.Run(_channel, CancellationToken.None);
            }
        }
    }
}
