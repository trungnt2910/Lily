using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    public class Beg : SpamStrategy
    {
        public Beg()
        {
            Command = "pls beg";
            Interval = 45000;
        }

        public override void OnMessageReceive(object sender, MessageReceiveEventArgs message)
        {
            //no-op
        }

        protected override async Task RunInternal(ChannelControl control)
        {
            Console.Error.WriteLine("[Debug]: Beg wating for control...");
            Console.Error.WriteLine("[Debug]: Begging...");
            await control.SendMessageAsync(Command);
            await Task.WhenAny(Task.Delay(60000), control.WaitForResponseAsync("Dank Memer"));
        }
    }
}
