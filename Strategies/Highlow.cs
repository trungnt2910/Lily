using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    public class Highlow : SpamStrategy
    {
        public Highlow()
        {
            Command = "pls highlow";
            Interval = 30000;
        }

        public override void OnMessageReceive(object sender, MessageReceiveEventArgs message)
        {
            //no-op
        }

        protected override async Task RunInternal(Channel channel)
        {
            var control = await channel.RequestControlAsync();
            Console.Error.WriteLine("[Debug]: Highlow running...");
            await control.SendMessageAsync(Command);
            var task = await Task.WhenAny(Task.Delay(60000), control.WaitForResponseAsync("Dank Memer"));
            if (task is Task<JToken> realTask)
            {
                await control.SendMessageAsync("high");
            }
            control.Release();
        }
    }
}
