using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lily
{
	public class Guess : SpamStrategy
	{
		public int Amount { get; set; }

		private TaskCompletionSource<object> _tcs;
		private ChannelControl _control;
		private TaskCompletionSource<int> _binarySearchTcs;
		private Random _rand = new Random();
		private int _attempts;
		private int _hints;
		private int _low;
		private int _high;
		
		// For Amount = 100, we can simply do this with a Binary Search.
		public Guess(int amount = 100)
		{
			Amount = amount;
			Command = $"pls guess {amount}";
			// We need to validate this.
			Interval = 10000;
		}
		
		public override async void OnMessageReceive(object sender, MessageReceiveEventArgs e)
		{
			e.Deferral.Lock();
			var message = e.Message;
			var content = message.Value<string>("content");

			if (content.Contains("try and guess my random number"))
			{
				var match = Regex.Match(content, "You've got (\\d*) attempts to try and guess my random number between \\*\\*(\\d*) and (\\d*)\\*\\*");
				_attempts = int.Parse(match.Groups[1].Value);
				// Is this correct? We need verification. For Attempts = 100 this is right.
				_hints = _attempts / 2;
				_low = int.Parse(match.Groups[2].Value);
				_high = int.Parse(match.Groups[3].Value);
				_ = BinarySearchAsync();
			}

			if (content.Contains("not this time,"))
			{
				if (_hints > 0)
				{
					--_hints;
					await _control.SendMessageAsync("hint");
				}
				else
				{
					_binarySearchTcs?.TrySetResult(1);
				}
			}

			if (content.Contains("too low"))
			{
				_binarySearchTcs?.TrySetResult(-1);
			}
			else if (content.Contains("too high"))
			{
				_binarySearchTcs?.TrySetResult(1);
			}

			if (content.Contains("you got the number right"))
			{
				_binarySearchTcs?.TrySetResult(0);
				Debug.WriteLine("Successful Guess the Number game");
				_tcs.SetResult(null);
			}

			if (content.Contains("you ran out of attempts"))
			{
				_binarySearchTcs?.TrySetResult(1);
				Debug.WriteLine("Unsuccessful Guess the Number game");
				_tcs.SetResult(null);
			}
			e.Deferral.Unlock();
		}

		private async Task BinarySearchAsync()
		{
			while (_hints > 0)
			{
				_binarySearchTcs = new TaskCompletionSource<int>();
				// This delay is to simulate a player's human calculations.
				// We cannot calculate one addition and one division instantly,
				// even with a calculator on our hands.
				await Task.Delay(_rand.Next(500, 2000));

				int mid = (_low + _high) / 2;
				--_attempts;
				await _control.SendMessageAsync(mid.ToString());

				var result = await _binarySearchTcs.Task;
				if (result == 0)
				{
					return;
				}
				// mid is too high
				else if (result > 0)
				{
					_high = mid - 1;
				}
				else //if (result < 0)
				{
					_low = mid + 1;
				}
			}
			List<int> l = Enumerable.Range(_low, _high - _low + 1).ToList();
			// Dirty way to shuffle.
			l.Sort((x, y) => _rand.Next(-1, 1));
			while (_attempts > 0 && l.Count > 0)
			{
				_binarySearchTcs = new TaskCompletionSource<int>();
				--_attempts;
				await _control.SendMessageAsync(l.Last().ToString());

				var result = await _binarySearchTcs.Task;
				if (result == 0)
				{
					return;
				}
			}
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