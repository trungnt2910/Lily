using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lily
{
    public abstract class SpamStrategy
    {
        public const int MinDelayTime = 16000;
        public const int MaxDelayTime = 64000;

        public int Interval { get; protected set; }
        public string Command { get; protected set; }
        public bool IsLongRunningStrategy { get; protected set; } = false;
        
        public abstract void OnMessageReceive(object sender, MessageReceiveEventArgs e);
        protected abstract Task RunInternal(Channel channel);

        private static Random _rand = new Random();

        public async Task Run(Channel channel, CancellationToken ct)
        {
            while (true)
            {
                if (ct.IsCancellationRequested) return;
                await RunInternal(channel);
                await Task.Delay(Interval + _rand.Next(MinDelayTime, MaxDelayTime));
            }
        }
    }
}
