using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    public class Deposit : SpamStrategy
    {
        public Deposit()
        {
            Command = "pls deposit max";
            Interval = 60000;
        }

        public override void OnMessageReceive(object sender, MessageReceiveEventArgs message)
        {
            //no-op
        }

        protected override async Task RunInternal(Channel channel)
        {
            var control = await channel.RequestControlAsync();
            Console.Error.WriteLine("[Debug]: Depositing all...");
            // By default, we will all use "SendAndWaitForReplyAsync".
            await control.SendMessageAsync(Command);
            await Task.WhenAny(Task.Delay(60000), control.WaitForResponseAsync("Dank Memer"));
            control.Release();
        }
    }
}
