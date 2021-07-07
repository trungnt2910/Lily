using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lily.Strategies
{
    class Fish : SpamStrategy
    {
        private MachineLearning _learning;
        private TaskCompletionSource<object> _tcs;
        public Fish(MachineLearning machineLearning)
        {
            Command = "pls fish";
            Interval = 40000;
            _learning = machineLearning;
        }
        private ChannelControl _control;
        private bool _isInGame;
        private string _currentQuestion;
        private string _currentAnswer;
        private string _gameType;
        private bool _lilyAlreadyKnew;
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
                    _learning.FeedSentence("fish", _currentQuestion);
                    var result = await _learning.QuerySentenceAsync("fish", _currentQuestion);
                    _lilyAlreadyKnew = result.IsFromCloud;
                    _suggested = result.Results;
                    _spamTask = SpamSuggestedAsync();
                }
                else if (content.Contains("unscramble"))
                {
                    _isInGame = true;
                    _gameType = "unscramble";
                    var word = Regex.Match(content, "`[^`]*`").Value;
                    _currentQuestion = word.Substring(1, word.Length - 2);
                    var result = await _learning.QueryUnscrambleAsync("fish", _currentQuestion);
                    _lilyAlreadyKnew = result.IsFromCloud;
                    _suggested = result.Results;
                    _spamTask = SpamSuggestedAsync();
                }
            }
            
            if (content.Contains("You cast out your line and brought back"))
            {
                Console.Error.WriteLine("[Debug]: Successful fishing attempt.");
                // This indicates success.
                if (_isInGame)
                {
                    if (!_lilyAlreadyKnew)
                    {
                        if (_gameType == "hangman")
                        {
                            _learning.FeedSentenceAnswer("fish", _currentQuestion, _currentAnswer);
                        }
                        else if (_gameType == "unscramble")
                        {
                            _learning.FeedScrambledWordAnswer("fish", _currentQuestion, _currentAnswer);
                        }
                    }
                }
                _isInGame = false;
                _tcs.SetResult(null);
            }

            if (content.Contains("The word was"))
            {
                _isInGame = false;
                await _spamTask;
                _currentAnswer = Regex.Match(content, "`[^`]*`").Value;
                _currentAnswer = _currentAnswer.Substring(1, _currentAnswer.Length - 2);
                if (_gameType == "hangman")
                {
                    _learning.FeedSentenceAnswer("fish", _currentQuestion, _currentAnswer);
                }
                else if (_gameType == "unscramble")
                {
                    _learning.FeedScrambledWordAnswer("fish", _currentQuestion, _currentAnswer);
                }
            }

            if (    content.Contains("oh snap, your fishing pole fell in the water") ||
                    content.Contains("the massive fish got away, you suck at this and should consider a life of crime instead"))
            {
                Console.Error.WriteLine("[Debug]: Failed fishing attempt.");
                _isInGame = false;
                _tcs.SetResult(null);
            }

            if (content.Contains("LMAO you found nothing. NICE!"))
            {
                Console.Error.WriteLine("[Debug]: Empty fishing attempt.");
                _tcs.SetResult(null);
            }
        }

        protected override async Task RunInternal(Channel channel)
        {
            _control = await channel.RequestControlAsync();
            _control.MessageReceived += OnMessageReceive;
            _tcs = new TaskCompletionSource<object>();
            await _control.SendMessageAsync(Command);
            await _tcs.Task;
            if (_spamTask != null)
            {
                await _spamTask;
            }
            _control.Release();
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
