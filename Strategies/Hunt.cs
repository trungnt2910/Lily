using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    public class Hunt : SpamStrategy
    {
        public Hunt()
        {
            Command = "pls hunt";
            Interval = 40000;
        }

        public override void OnMessageReceive(object sender, MessageReceiveEventArgs args)
        {
            //no-op
        }

        protected override async Task RunInternal(Channel channel)
        {
            var control = await channel.RequestControlAsync();
            Console.Error.WriteLine("[Debug]: Hunting...");
            // Hunting quest uses the default "Type" message, so we should be alright.
            await control.SendMessageAsync(Command);
            await Task.WhenAny(Task.Delay(60000), control.WaitForResponseAsync("Dank Memer"));
            control.Release();
        }
    }
}
