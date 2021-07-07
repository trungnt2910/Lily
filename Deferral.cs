using System;
using System.Threading.Tasks;

namespace Lily
{
	public class Deferral
	{
		private int _count;
		private bool _isWaiting;
		private TaskCompletionSource<object> _tcs;
		public Deferral(TaskCompletionSource<object> tcs)
		{
			_tcs = tcs;
			_count = 0;
			_isWaiting = false;
		}
		
		public void Lock()
		{
			++_count;
			Console.Error.WriteLine($"[Debug]: Deferral lock: {_count}");
		}

		public void Unlock()
		{
			--_count;
			Console.Error.WriteLine($"[Debug]: Deferral unlock: {_count}");
			if (_isWaiting && _count == 0)
			{
				_tcs.SetResult(null);
			}
		}

		public void StartWaiting()
		{
			_isWaiting = true;
			if (_count == 0)
			{
				_tcs.SetResult(true);
			}
		}
	}
}