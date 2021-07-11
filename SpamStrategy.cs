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
        public const int WorkTimeout = 60000;
        public const int MaxFailStreak = 10;

        public int Interval { get; protected set; }
        public string Command { get; protected set; }
        public bool IsLongRunningStrategy { get; protected set; } = false;
        
        /// <summary>
        /// The fail streak of the tasks.
        /// If this number exceeds MaxFailStreak, the app should
        /// terminate in emergency mode, as this probably 
        /// indicates that Dank Memer is about to blacklist the 
        /// current account.
        /// </summary>
        public static int FailStreak { get; set; }

        public abstract void OnMessageReceive(object sender, MessageReceiveEventArgs e);
        protected abstract Task RunInternal(ChannelControl control);

        private static Random _rand = new Random();

        public async Task Run(Channel channel, CancellationToken ct)
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    Console.Error.WriteLine($"[Debug]: Strategy {Command} killed.");
                    return;
                }

                Console.Error.WriteLine($"[Debug]: {Command} waiting for control...");
                var control = await channel.RequestControlAsync();

                var delayTask = Task.Delay(WorkTimeout);
                var task = await Task.WhenAny(RunInternal(control), delayTask);

                if (task == delayTask)
                {
                    Console.Error.WriteLine($"[Debug]: {Command} failed to run in time.");
                    
                    await channel.PingAsync();

                    ++FailStreak;
                    if (FailStreak > MaxFailStreak)
                    {
                        throw new ApplicationException("Too many timeouts. This may indicate that Dank Memer has blacklisted the current user.");
                    }
                }
                else
                {
                    FailStreak = 0;
                }

                Console.Error.WriteLine($"[Debug]: {Command} Trying to release control...");
                control.Release();
                Console.Error.WriteLine($"[Debug]: {Command} released control.");

                await Task.Delay(Interval + _rand.Next(MinDelayTime, MaxDelayTime));
            }
        }
    }
}
