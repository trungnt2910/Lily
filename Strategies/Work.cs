
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lily.Strategies
{
	public class Work : SpamStrategy
	{
		private MachineLearning _learning;
		private ChannelControl _control;
		private TaskCompletionSource<object> _tcs;
		private TaskCompletionSource<string> _colorMatchTcs;
		private string _context;
		private string _mode;
		private string _content;
		private string _currentQuestion;
		private string _currentAnswer;
		private CancellationTokenSource _cts;
		private CancellationToken _token;
		private Task _workTask;

		public Work(MachineLearning learning)
		{
			Command = "pls work";
			// Again, debug only.
			Interval = 3600000;
			IsLongRunningStrategy = true;
			_learning = learning;
		}

		public override async void OnMessageReceive(object sender, MessageReceiveEventArgs e)
		{
			e.Deferral.Lock();
			// Work result
			var message = e.Message;
			var embeds = message["embeds"];
			var content = message.Value<string>("content");
			foreach (var embed in embeds)
			{
				var description = embed.Value<string>("description");
				if (description != null)
				{
					if (description.Contains("TERRIBLE work!"))
					{
						Console.Error.WriteLine("[Debug]: Failed working attempt");
						_cts.Cancel();
						_tcs.TrySetResult(null);
						if (_mode == "Hangman")
						{
							var answer = Regex.Match(description, "`([^`]*)`").Groups[1].Value;
							_ =_learning.FeedSentenceAnswer(_context, _currentQuestion, answer);
						}
						if (_mode == "Scramble")
						{
							var answer = Regex.Match(description, "`([^`]*)`").Groups[1].Value;
							_ = _learning.FeedScrambledWordAnswer(_context, _currentQuestion, answer);
						}
					}
					if (description.Contains("Great work!"))
					{
						Console.Error.WriteLine("[Debug]: Successful working attempt");
						if (_mode == "Hangman")
						{
							_ = _learning.FeedSentenceAnswer(_context, _currentQuestion, _currentAnswer);
						}
						if (_mode == "Scramble")
						{
							_ = _learning.FeedScrambledWordAnswer(_context, _currentQuestion, _currentAnswer);
						}
						_cts.Cancel();
						_tcs.TrySetResult(null);
					}
				}
			}

			//Debug Only!
#region Evil
			if (content.Contains("TERRIBLE work!"))
			{
				Console.Error.WriteLine("[Debug]: Failed working attempt");
				_cts.Cancel();
				_tcs.TrySetResult(null);
				if (_mode == "Hangman")
				{
					var answer = Regex.Match(content, "`([^`]*)`").Groups[1].Value;
					_ = _learning.FeedSentenceAnswer(_context, _currentQuestion, answer);
				}
				if (_mode == "Scramble")
				{
					var answer = Regex.Match(content, "`([^`]*)`").Groups[1].Value;
					_ = _learning.FeedScrambledWordAnswer(_context, _currentQuestion, answer);
				}
			}
			if (content.Contains("Great work!"))
			{
				Console.Error.WriteLine("[Debug]: Successful working attempt");
				if (_mode == "Hangman")
				{
					_ = _learning.FeedSentenceAnswer(_context, _currentQuestion, _currentAnswer);
				}
				if (_mode == "Scramble")
				{
					_ = _learning.FeedScrambledWordAnswer(_context, _currentQuestion, _currentAnswer);
				}
				_cts.Cancel();
				_tcs.TrySetResult(null);
			}
#endregion

			// To prevent a deadlock...
			// The job's response, "The word was `bullcrap` ya looser"
			// might be mistaken as a simple response and prompt 
			// the word spammer thread to continue.
			if (content.Contains("The word was"))
            {
				try
                {
					_cts.Cancel();
				}
				catch
                {
					// Nothing to worry, if it wasn't the answer.
                }
			}


			if (content.Contains("You need to wait"))
			{
				Console.Error.WriteLine("[Debug]: Not time to work yet!");
				_workTask = Task.CompletedTask;
				_tcs.TrySetResult(null);
			}

			if (content.Contains("You never fail to amaze me"))
			{
				Debug.WriteLine("Promoted");
				await _control.SendMessageAsync(Command);
			}

			// Work task.
			if (content.Contains("Work for"))
			{
				try
				{
					var contentParts = Regex.Match(content, "\\*\\*Work for (.*)\\*\\* - (.*) - ([\\S\\s]*)").Groups
										.Cast<Group>()
										.Skip(1)
										.Select(g => g.Value.Trim())
										.ToArray();
					//content.Split("-").Select(x => x.Trim()).ToArray();
					// [0]: Context
					// [1]: Mode
					// [2]: Description.

					// const string contextPrefix = "**Work for ";
					// const string contextSuffix = "**";
					
					_context = contentParts[0];
					// _context = _context.Substring(contextPrefix.Length, _context.Length - (contextPrefix.Length + contextSuffix.Length));
					_mode = contentParts[1];
					_content = contentParts[2];

					Console.Error.WriteLine($"[Debug]: Context: {_context}");
					Console.Error.WriteLine($"[Debug]: Mode: {_mode}");

					_workTask = WorkAsync();
				}
				catch
				{
					
				}

			}
			e.Deferral.Unlock();
		}

		public void OnMessageUpdate(object sender, MessageReceiveEventArgs e)
		{
			var content = e.Message.Value<string>("content");
			if (content.Contains("What color was next to the word"))
			{
				var match = Regex.Match(content, "`[^`]*`");
				var word = match.Value;
				word = word.Substring(1, word.Length - 2);
				word = Purify(word);
				Console.Error.WriteLine($"[Debug]: Asked for the word {word}");
				_colorMatchTcs?.TrySetResult(word);
			}
		}

		public async Task WorkAsync()
		{
			switch (_mode)
			{
				// These two primitive tasks have been handled by the main SpamEngine.
				// We might want to feed these sentences to Lily.
				case "Reverse":
					Console.WriteLine($"[Debug]: Doing subtask...");
					await FeedWordsAsync(_content);
					Console.WriteLine($"[Debug]: Done subtask...");
				break;
				case "Retype":
					await FeedSentenceAsync();
				break;
				case "Soccer":
					await PlaySoccerAsync();
				break;
				// In all of these games, we will feed the words to machine learning, for scramble.
				case "Memory":
					await PlayMemoryAsync();
				break;
				case "Color Match":
					await PlayColorMatchAsync();
				break;
				// Machine learning cases:
				case "Hangman":
					await PlayHangmanAsync();
				break;
				case "Scramble":
					await PlayUnscrambleAsync();
				break;
				default:
					await _control.SendMessageAsync("Sorry, I can't do this job");
				break;
			}
			Console.WriteLine($"[Debug]: Done switching...");
		}

		protected override async Task RunInternal(ChannelControl control)
		{
			_control = control;
			_tcs = new TaskCompletionSource<object>();
			_colorMatchTcs = new TaskCompletionSource<string>();
			_cts = new CancellationTokenSource();
			_token = _cts.Token;
			_control.MessageReceived += OnMessageReceive;
			_control.MessageUpdated += OnMessageUpdate;
			await _control.SendMessageAsync(Command);
			await _tcs.Task;
			Console.Error.WriteLine("[Debug]: Work done, releasing control.");
			await _workTask;
			Console.Error.WriteLine("[Debug]: Work task complete.");
		}

		private async Task PlaySoccerAsync()
		{
			// We assume that Lily can send her message fast enough so
			// that the damn goalkeeper cannot move.
			// Left
			// Somehow the user sees differently from the JSON?
			if (_content.Contains("🥅🥅🥅\n🕴️\n\n"))
			{
				await _control.SendMessageAsync("middle");
			}
			else if (_content.Contains(":goal::goal::goal:\n:levitate:\n\n"))
			{
				await _control.SendMessageAsync("middle");
			}
			// else we might bother checking for either
			// :goal::goal::goal:\n       :levitate:\n\n       \u26bd
			// (7 spaces before :levitate:)
			// or 
			// :goal::goal::goal:\n              :levitate:\n\n       \u26bd
			// (14 spaces before :levitate:)
			// but we can just go left, instead of all this fuckery.
			else
			{
				await _control.SendMessageAsync("left");
			}
		}

		private async Task PlayMemoryAsync()
		{
			var matches = Regex.Matches(_content, "`[^`]*`");
			var wordList = new List<string>();
			foreach (Match match in matches)
			{
				var toType = match.Value;
				toType = toType.Substring(1, toType.Length - 2);
				toType = Purify(toType);
				wordList.AddRange(toType.Split("\n"));
			}
			await _control.SendMessageAsync(string.Join("\n", wordList));
			await FeedWordsAsync(_content);
		}

		private async Task PlayColorMatchAsync()
		{
			var wordColorPairs = new Dictionary<string, string>();
			var matches = Regex.Matches(_content, "<:([A-Za-z]*):.*> `([A-Za-z]*)`");
			foreach (Match match in matches)
			{
				wordColorPairs.Add(match.Groups[2].Value, match.Groups[1].Value);
			}
			var requiredWord = await _colorMatchTcs.Task;
			await _control.SendMessageAsync(wordColorPairs[requiredWord]);
			await FeedWordsAsync(_content);
		}

		private async Task PlayHangmanAsync()
		{
			_currentQuestion = Regex.Match(_content, "`([^`]*)`").Groups[1].Value;
			_currentQuestion = _currentQuestion.Replace(" _", "[a-zA-Z*]");
			var results = await _learning.QuerySentenceAsync(_context, _currentQuestion);
			foreach (var result in results)
			{
				if (_token.IsCancellationRequested)
				{
					return;
				}
				_currentAnswer = result;
				Console.Error.WriteLine($"[Debug]: {_currentAnswer}");
				await _control.SendMessageAsync(result);
				await _control.WaitForResponseAsync("Dank Memer");
			}
		}

		private async Task PlayUnscrambleAsync()
		{
			_currentQuestion = Regex.Match(_content, "`([^`]*)`").Groups[1].Value;
			var results = await _learning.QueryUnscrambleAsync(_context, _currentQuestion);
			foreach (var result in results)
			{
				if (_token.IsCancellationRequested)
				{
					return;
				}
				_currentAnswer = result;
				Console.Error.WriteLine($"[Debug]: {_currentAnswer}");
				await _control.SendMessageAsync(result);
				await _control.WaitForResponseAsync("Dank Memer");
			}
		}

		private async Task FeedSentenceAsync()
		{
			var matches = Regex.Matches(_content, "`[^`]*`");
			foreach (Match match in matches)
			{
				var toType = match.Value;
				toType = toType.Substring(1, toType.Length - 2);
				toType = Purify(toType);
				await _learning.FeedSentenceAnswer(_context, toType, "");
			}
		}

		private async Task FeedWordsAsync(string content)
		{
			var matches = Regex.Matches(content, "`[^`]*`");
			var wordList = new List<string>();
			foreach (Match match in matches)
			{
				var toType = match.Value;
				toType = toType.Substring(1, toType.Length - 2);
				toType = Purify(toType);
				wordList.AddRange(toType.Split("\n"));
			}
			foreach (var word in wordList)
			{
				await _learning.FeedScrambledWordAnswer(_context, word, word);
			}
		}

		// Removes trap characters.
		private string Purify(string old)
		{
			return Regex.Replace(old, @"[^\u0000-\u007F]+", string.Empty);
		}
	}
}