using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lily
{
	public class MachineLearning
	{
		private class PrivateRegex
		{
			[JsonProperty(PropertyName = "$regex")]
			public string Regex { get; set; }
			public PrivateRegex(string s)
			{
				Regex = s;
			}
		}
		// To contributors, and to GitHub:
		// I do not own this website, forgive me for the offensive words.
		// I am not a racist in any way.
		const string feedLink = "https://discord-spam-nigga.herokuapp.com/update";
		const string queryLink = "https://discord-spam-nigga.herokuapp.com/find";
		private Channel _channel;
		private Random _rand = new Random();
		private HttpClient _client = new HttpClient();
		public MachineLearning(Channel channel)
		{
			_channel = channel;
			// _channel.MessageReceive += OnMessageReceive;
		}

		public void FeedSentence(string context, string content)
		{
			Console.WriteLine($"[Lily]: I'll memorize this sentence: {content}");
			Console.WriteLine($"[Debug]: sentence: {content}");
			var words = content.Split();
			foreach (var word in words)
			{
				if (word.Contains("["))
				{
					continue;
				}
				if (word.Length == 0)
				{
					continue;
				}
				var realWord = string.Empty;
				if (word[word.Length - 1] == '.')
				{
					realWord = word.Substring(0, word.Length - 1);
				}
				else 
				{
					realWord = word;
				}
				realWord = realWord.ToLower();
				Console.WriteLine($"[Debug]: Sending word {realWord} with context {context}");
				var text = JsonConvert.SerializeObject(new {doc = new {word = realWord, category = "hangman_word_framgents", context = context}});
				Console.WriteLine($"[Debug]: {text}");
				_ = Feed(text);
			}
		}

		private async Task Feed(string json)
		{
			var request = new HttpRequestMessage();
			request.Content = new StringContent(json, Encoding.UTF8, "application/json");
			request.Method = HttpMethod.Post;
			request.RequestUri = new Uri(feedLink);

			await _client.SendAsync(request);
		}

		public void FeedSentenceAnswer(string context, string sentence, string word)
		{
			Console.WriteLine($"[Lily]: I'll keep a note that the answer to: {sentence} is {word}");
			var words = sentence.Split();
			foreach (var wword in words)
			{
				if (wword.Contains("[a-zA-Z*]"))
				{
					sentence = sentence.Replace(wword, word);
				}
			}
			var text = JsonConvert.SerializeObject(new {doc = new {question = sentence, answer = word, category = "hangman", context = context}});
			Console.WriteLine($"[Debug]: {text}");
			_ = Feed(text);
		}

		private string Sort(string str)
		{	
			var arr = str.ToCharArray();
			Array.Sort(arr);
			return new string(arr);
		}

		public void FeedScrambledWordAnswer(string context, string scrambled, string unscrambled)
		{
			Console.WriteLine($"[Lily]: I'll remember that {scrambled} is actually {unscrambled}, no need to look up my dictionary now.");
			scrambled = Sort(scrambled);
			var text = JsonConvert.SerializeObject(new {doc = new {question = scrambled, answer = unscrambled, category = "unscramble", context = context}});
			Console.WriteLine($"[Debug]: {text}");
			_ = Feed(text);
		}

        /// <summary>
        /// Queries the server for a find missing work challenge.
        /// </summary>
        /// <param name="context">The game context, such as fishing or working</param>
        /// <param name="content">The sentence, as a regular expression.</param>
        /// <returns></returns>		
		public async Task<(IEnumerable<string> Results, bool IsFromCloud)> QuerySentenceAsync(string context, string content)
		{
			Console.WriteLine($"[Lily]: Checking my notebooks, please wait.");
			
			var arr = await Query(JsonConvert.SerializeObject(new 
			{
				query = new 
				{
					question = new PrivateRegex(content), 
					category = new PrivateRegex("hangman"), 
					context = new PrivateRegex(context)
				}
			}));
			if (arr.Count != 0)
			{
				return (arr
						.Select(token => token.Value<string>("question"))
						.Where(x => x != null)
						.Select(x => Extract(x, content)), true);
			}

			Console.WriteLine($"[Lily]: This one's tough, I'll randomize a word for now.");
			
			return (FillRandom(content), false);
		}

		private string Extract(string original, string regexed)
		{
			var arr1 = original.Split();
			var arr2 = regexed.Split();
			for (int i = 0; i < arr2.Length; ++i)
			{
				if (arr2[i].Contains("["))
				{
					return arr1[i];
				}
			}
			throw new InvalidOperationException("What's wrong with the database?");
		}

		public IEnumerable<string> FillRandom(string content)
		{
			// This is more programmer friendly 
			content = content.Replace("[a-zA-Z*]", "*");
			var contents = content.Split();
			content = contents.FirstOrDefault(str => str.Contains("*"));
			while (true)
			{
				var arr = content.ToCharArray();
				for (int i = 0; i < arr.Length; ++i)
				{
					if (arr[i] == '*')
					{
						arr[i] = (char)_rand.Next('a', 'z');
					}
				}
				yield return new string(arr);
			}
		}

		public async Task<(IEnumerable<string> Results, bool IsFromCloud)> QueryUnscrambleAsync(string context, string scrambled)
		{
			Console.WriteLine($"[Lily]: Checking my notebooks, please wait.");
			
			try
			{
				var arr = await Query(JsonConvert.SerializeObject(new 
				{
					query = new 
					{
						question = new PrivateRegex(Sort(scrambled)), 
						category = new PrivateRegex("unscramble"), 
						context = new PrivateRegex(context)
					}
				}));
				if (arr.Count != 0)
				{
					return (arr.Select(token => token.Value<string>("answer")).Where(x => x != null), true);
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine($"[Debug]: {e.GetType()}: {e.Message}");
			}

			// Must write the code here to use our async capabilities.
			Console.WriteLine($"[Lily]: I haven't got this, consulting my dictionary...");
			var httpRequest = new HttpRequestMessage();
			httpRequest.RequestUri = new Uri($"https://wordunscrambler.me/unscramble/{scrambled}");
			var response = await _client.SendAsync(httpRequest);
			var text = await response.Content.ReadAsStringAsync();
			var words = Regex.Matches(text, "data-word=\"[^\"]*\"");
			Console.Error.WriteLine($"[Debug]: {words.Count} words found.");

			return (UnscrambleFallback(scrambled, words
				.Select(match => match.Value)
				.Select(word => word.Substring("data-word=\"".Length, word.Length - 1 - "data-word=\"".Length))
				.Where(word => word.Length == scrambled.Length)), false);
		}

		public IEnumerable<string> UnscrambleFallback(string scrambled, IEnumerable<string> suggestions)
		{
			foreach (var suggestion in suggestions)
			{
				yield return suggestion;
			}

			Console.WriteLine($"[Lily]: This one's tough, I'll randomize a word for now.");
			
			var arr = scrambled.ToCharArray();

			while (true)
			{
				Randomize(arr);
				yield return new string(arr);
			}
		}

		private async Task<JArray> Query(string json)
		{
			Console.Error.WriteLine($"[Debug]: {json}");
			var request = new HttpRequestMessage();
			request.Content = new StringContent(json, Encoding.UTF8, "application/json");
			request.Method = HttpMethod.Post;
			request.RequestUri = new Uri(queryLink);
			
			var response = await _client.SendAsync(request);
			var responseJson = await response.Content.ReadAsStringAsync();
			
			response.EnsureSuccessStatusCode();

			return JsonConvert.DeserializeObject<JArray>(responseJson);
		}

		// private void OnMessageReceive(object sender, MessageReceiveEventArgs args)
		// {
		// 	args.Deferral.Lock();
		// 	Console.Error.WriteLine("[Debug]: Machine Learning routines...");
		// 	var message = args.Message;
			
		// 	var content = message.Value<string>("content");
		// 	var embeds = message.Value<JArray>("embed");

		// 	if (content.Contains("missing word", StringComparison.InvariantCultureIgnoreCase) ||
		// 		content.Contains("missing __word__", StringComparison.InvariantCultureIgnoreCase))
		// 	{
		// 		var matches = Regex.Matches(content, "`[^`]*`");
		// 		foreach (Match match in matches)
		// 		{
		// 			var text = match.Value;
		// 			if (!text.Contains("_")) continue;
		// 			text = text.Substring(1, text.Length - 2);
		// 			Console.WriteLine($"[Lily]: I'm sending this to my developers: {text}");
		// 		}
		// 	}

		// 	SearchForHangmanAnswers(content);

		// 	if (embeds != null)
		// 	{
		// 		// The answer may be embedded.
		// 		foreach (var embed in embeds)
		// 		{
		// 			var description = embed.Value<string>("description");
		// 			SearchForHangmanAnswers(description);
		// 		}
		// 	}

		// 	Console.Error.WriteLine("[Debug]: Machine Learning done.");
		// 	args.Deferral.Unlock();
		// }

		// void SearchForHangmanAnswers(string content)
		// {
		// 	const string prefix = "The word was `";
		// 	const string suffix = "`";
		// 	var matches = Regex.Matches(content, "The word was `[^`]*`");
		// 	foreach (Match match in matches)
		// 	{
		// 		var word = match.Value;
		// 		word = word.Substring(prefix.Length, word.Length - prefix.Length - suffix.Length);
		// 		Console.WriteLine($"[Lily]: I'm sending this to my developers: {word}");
		// 	}
		// }

		// public async Task<string> QueryHangmanAsync(string Question)
		// {
		// 	return string.Empty;
		// }

		// Stolen...
	    private void Randomize<T>(T[] items)
		{
			// For each spot in the array, pick
			// a random item to swap into that spot.
			for (int i = 0; i < items.Length - 1; i++)
			{
				int j = _rand.Next(i, items.Length);
				T temp = items[i];
				items[i] = items[j];
				items[j] = temp;
			}
		}
	}

}