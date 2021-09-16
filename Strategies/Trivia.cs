using System;
using System.Threading.Tasks;

namespace Lily
{
	public class Trivia : SpamStrategy
	{
		private static Random _rand = new Random();
		private static char LettorOfTheDay = (char)_rand.Next('A', 'D');

		private ChannelControl _control;
		private TaskCompletionSource<object> _tcs;
		public Trivia()
		{
			Command = "pls trivia";
			Interval = 15000;
		}

		// We may want to implement this using the MachineLearning class, however,
		// this proves to be more complicated, so we'll just spam a Letter of the Day.
		// This approach is similar to the way we implement highlow.
		public override async void OnMessageReceive(object sender, MessageReceiveEventArgs args)
		{
			args.Deferral.Lock();
            var message = args.Message;
            var embeds = message["embeds"];
            foreach (var embed in embeds)
            {
                var title = embed["author"]?.Value<string>("name");
                if (title != null)
                {
                    Console.Error.WriteLine($"[Debug]: {title}");
                    if (title.Contains("trivia question"))
                    {
						await _control.SendMessageAsync(LettorOfTheDay.ToString());
                    }
                }
            }

			var content = message.Value<string>("content");
			if (content.Contains("You got that answer correct"))
			{
				Debug.WriteLine("Successful trivia attempt.");
				_tcs.SetResult(null);
			}
			if (content.Contains("the correct answer was"))
			{
				Debug.WriteLine("Unsuccessful trivia attempt.");
				_tcs.SetResult(null);
			}

			args.Deferral.Unlock();
		}

		protected override async Task RunInternal(ChannelControl control)
		{
			_tcs = new TaskCompletionSource<object>();
			_control = control;
			_control.MessageReceived += OnMessageReceive;
			await _control.SendMessageAsync(Command);
			await _tcs.Task;
		}
	}
}