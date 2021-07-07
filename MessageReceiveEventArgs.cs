using Newtonsoft.Json.Linq;

namespace Lily
{
	public class MessageReceiveEventArgs
	{
		public JToken Message { get; set; }
		public Deferral Deferral { get; set; }
	}
}