using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Lily
{
	public class ChannelControl
	{
		private Channel _channel;
		private TaskCompletionSource<object> _tcs;
		private Dictionary<string, TaskCompletionSource<JToken>> _waitingMessages = new Dictionary<string, TaskCompletionSource<JToken>>();

		internal ChannelControl(Channel channel)
		{
			_channel = channel;
			_tcs = new TaskCompletionSource<object>();
			_tcs.SetResult(null);
		}

		internal void Reset()
		{
			_tcs = new TaskCompletionSource<object>();
			_waitingMessages = new Dictionary<string, TaskCompletionSource<JToken>>();
			foreach (Delegate d in _delegates)
			{
				_messageReceived -= (EventHandler<MessageReceiveEventArgs>)d;
			}
			_delegates.Clear();
			foreach (Delegate d in _messageUpdatedDelegates)
			{
				_messageUpdated -= (EventHandler<MessageReceiveEventArgs>)d;
			}
			_messageUpdatedDelegates.Clear();
		}

		internal async Task Receive(string id, JToken message)
		{
			var deferralTcs = new TaskCompletionSource<object>();
			var deferral = new Deferral(deferralTcs);
			_messageReceived?.Invoke(this, new MessageReceiveEventArgs { Message = message, Deferral = deferral } );
			deferral.StartWaiting();
			await deferralTcs.Task;
			lock (_waitingMessages)
			{
				var username = message["author"].Value<string>("username");
				if (_waitingMessages.TryGetValue(username, out var tcs))
				{
					tcs.SetResult(message);
					_waitingMessages.Remove(username);
				}
			}
		}

		internal void Update(string id, JToken message)
		{
			var deferral = new Deferral(new TaskCompletionSource<object>());
			_messageUpdated?.Invoke(this, new MessageReceiveEventArgs { Message = message, Deferral = deferral } );
		}

		private event EventHandler<MessageReceiveEventArgs> _messageReceived;
		HashSet<Delegate> _delegates = new HashSet<Delegate>();
		public event EventHandler<MessageReceiveEventArgs> MessageReceived
		{
			add
			{
				_messageReceived += value;
				_delegates.Add(value);
			}
			remove
			{
				_messageReceived -= value;
				_delegates.Remove(value);
			}
		}

		private event EventHandler<MessageReceiveEventArgs> _messageUpdated;
		HashSet<Delegate> _messageUpdatedDelegates = new HashSet<Delegate>();
		public event EventHandler<MessageReceiveEventArgs> MessageUpdated
		{
			add
			{
				_messageUpdated += value;
				_messageUpdatedDelegates.Add(value);
			}
			remove
			{
				_messageUpdated -= value;
				_messageUpdatedDelegates.Remove(value);
			}
		}

		public void Release()
		{
			_tcs.SetResult(null);
		}

		public Task Wait()
		{
			return _tcs.Task;
		}

		public async Task<string> SendMessageAsync(string content)
		{
			return await _channel.SendMessageRawAsync(content, true);
		}

		// Waits for a repsonse from a user with the specified name.
		public async Task<JToken> WaitForResponseAsync(string username)
		{
			var tcs = new TaskCompletionSource<JToken>();
			lock (_waitingMessages)
			{
				_waitingMessages.Add(username, tcs);
			}
			return await tcs.Task;
		}
	}
}