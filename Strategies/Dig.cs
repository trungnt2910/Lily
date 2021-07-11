using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    class Dig : SpamStrategy
    {
        private MachineLearning _learning;
        private TaskCompletionSource<object> _tcs;
        public Dig(MachineLearning machineLearning)
        {
            Command = "pls dig";
            Interval = 40000;
            _learning = machineLearning;
        }
        private ChannelControl _control;
        private bool _isInGame;
        private string _currentQuestion;
        private string _currentAnswer;
        private string _gameType;
        private IEnumerable<string> _suggested;
        private Task _spamTask;

        public override async void OnMessageReceive(object sender, MessageReceiveEventArgs args)
        {
            var content = args.Message.Value<string>("content");
            var author = args.Message["author"].Value<string>("username");
            
            if (author != "Dank Memer")
            {
                return;
            }

            if (content.Contains("the fish is too strong!"))
            {
                if (content.Contains("missing word"))
                {
                    _isInGame = true;
                    _gameType = "hangman";
                    var sentence = Regex.Match(content, "`[^`]*`").Value;
                    _currentQuestion = sentence.Substring(1, sentence.Length - 2);
                    _currentQuestion = _currentQuestion.Replace(" _", "[a-zA-Z*]");
                    // There is a chance that Dank Memer doesn't give you the answer.
                    _ = _learning.FeedSentence("dig", _currentQuestion);
                    _suggested = await _learning.QuerySentenceAsync("dig", _currentQuestion);
                    _spamTask = SpamSuggestedAsync();
                }
                else if (content.Contains("unscramble"))
                {
                    _isInGame = true;
                    _gameType = "unscramble";
                    var word = Regex.Match(content, "`[^`]*`").Value;
                    _currentQuestion = word.Substring(1, word.Length - 2);
                    _suggested = await _learning.QueryUnscrambleAsync("dig", _currentQuestion);
                    _spamTask = SpamSuggestedAsync();
                }
                else if (content.Contains("re-type"))
                {
                    // The phrase is actually already typed, we just need to feed 
                    // it to machine learning.
                    try
                    {
                        var sentence = Regex.Match(content, "Type `([^`]*)`").Groups[1].Value;
                        sentence = Regex.Replace(sentence, @"[^\u0000-\u007F]+", string.Empty);
                        _ = _learning.FeedSentenceAnswer("dig", sentence, "");
                        _ = _learning.FeedSentence("dig", sentence);
                    }
                    catch (Exception e)
                    {
                        // This error is not critical.
                        Console.Error.WriteLine($"[Debug]: {e.GetType()} while trying to parse sentence.");
                    }
                }
            }
            
            if (content.Contains("You dig in the dirt and brought back"))
            {
                Console.Error.WriteLine("[Debug]: Successful digging attempt.");
                // This indicates success.
                if (_isInGame)
                {
                    if (_gameType == "hangman")
                    {
                        _ = _learning.FeedSentenceAnswer("dig", _currentQuestion, _currentAnswer);
                    }
                    else if (_gameType == "unscramble")
                    {
                        _ = _learning.FeedScrambledWordAnswer("dig", _currentQuestion, _currentAnswer);
                    }
                }
                _isInGame = false;
                _tcs.TrySetResult(null);
            }

            if (content.Contains("The word was"))
            {
                _isInGame = false;
                await _spamTask;
                _currentAnswer = Regex.Match(content, "`[^`]*`").Value;
                _currentAnswer = _currentAnswer.Substring(1, _currentAnswer.Length - 2);
                if (_gameType == "hangman")
                {
                    _ = _learning.FeedSentenceAnswer("dig", _currentQuestion, _currentAnswer);
                }
                else if (_gameType == "unscramble")
                {
                    _ = _learning.FeedScrambledWordAnswer("dig", _currentQuestion, _currentAnswer);
                }
            }

            if (    content.Contains("The thing in the ground was WAY too strong") ||
                    content.Contains("the massive fish got away, you suck at this and should consider a life of crime instead"))
            {
                Console.Error.WriteLine("[Debug]: Failed digging attempt.");
                _isInGame = false;
                _tcs.TrySetResult(null);
            }

            if (content.Contains("LMAO you found nothing in the ground."))
            {
                Console.Error.WriteLine("[Debug]: Empty digging attempt.");
                _tcs.TrySetResult(null);
            }
        }

        protected override async Task RunInternal(ChannelControl control)
        {
            _control = control;
            _control.MessageReceived += OnMessageReceive;
            _tcs = new TaskCompletionSource<object>();
            await _control.SendMessageAsync(Command);
            await _tcs.Task;
            if (_spamTask != null)
            {
                await _spamTask;
            }
            _control = null;
        }

        private async Task SpamSuggestedAsync()
        {
            foreach (var word in _suggested)
            {
                if (_isInGame)
                {
                    _currentAnswer = word;
                    await _control?.SendMessageAsync(word);
                    await _control?.WaitForResponseAsync("Dank Memer");
                    await Task.Delay(1500);
                }
                else return;
            }
        }

        private void Reset()
        {
            
        }

        private void PlayUnscramble(ChannelControl control)
        {

        }

        private void PlayFindMissingWord(ChannelControl control)
        {

        }
    }
}
