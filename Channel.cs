using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Timers;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp.NetCore;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Lily
{
    public class Channel
    {
        public class Payload
        {
            public int op { get; set; }
            public class D
            {
                public string token { get; set; }
                public int intents { get; set; }
                public class Properties
                {
                    [JsonProperty(PropertyName = "$os")]
                    public string os;
                    [JsonProperty(PropertyName = "$browser")]
                    public string browser;
                    [JsonProperty(PropertyName = "$device")]
                    public string device;
                }
                public Properties properties;
            }
            public D d;
        }

        public string ChannelId { get; private set; }
        public string ServerId { get; private set; }
        public string Token { get; private set; }

        public event EventHandler<MessageReceiveEventArgs> MessageReceive;

        private WebSocket _ws;
        private HttpClient _client = new HttpClient();
        private Dictionary<string, TaskCompletionSource<string>> _waitingMessages = new Dictionary<string, TaskCompletionSource<string>>();
        private BigInteger _nonce;
        private ConcurrentQueue<TaskCompletionSource<ChannelControl>> _controlRequestQueue = new ConcurrentQueue<TaskCompletionSource<ChannelControl>>();
        // private ConcurrentQueue<(string Content, Action<string> Callback, TaskCompletionSource<string> Tcs)> _messasgeQueue = new ConcurrentQueue<(string, Action<string>, TaskCompletionSource<string>)>();
        private int _haltQueue = 0;
        private bool _queueRunning = false;
        private ChannelControl _control;
        private Timer _timer;

        public Channel(string url, string token)
        {
            Token = token;
            var fragments = url.Split("/");
            ChannelId = fragments[fragments.Length - 1];
            ServerId = fragments[fragments.Length - 2];

            _nonce = BigInteger.Parse(ChannelId);
            var rand = new Random();
            var bytes = new byte[6];
            rand.NextBytes(bytes);
            _nonce += new BigInteger(bytes);
            _control = new ChannelControl(this);
            _control.Release();

            ++_haltQueue;
            SetupWebSocketAsync().ContinueWith((t) => 
            {
                Console.Error.WriteLine("[Debug]: WebSocket connected. Unhalting queue...");
                _ = RunControlRequestQueue();
                --_haltQueue;
            });
        }

        private async Task SetupWebSocketAsync()
        {
            _ws = new WebSocket("wss://gateway.discord.gg/?v=6&encoding=json");
            TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

            // Setup WebSocket
            _ws.OnOpen += (sender, args) =>
            {
                var payload = new
                {
                    op = 2,
                    d = new
                    {
                        token = Token,
                        intents = 513,
                        properties = new Dictionary<string, string>
                        {
                            // Only exists in your dreams...
                            { "$os",  "Windows 11 Mobile" },
                            { "$browser", "edge" },
                            { "$device", "surfacephone" }
                        }
                    }
                };
                var data = JsonConvert.SerializeObject(payload);
                _ws.Send(data);
                _tcs.SetResult(null);
            };

            _ws.OnMessage += MessageReceived;

            _ws.OnError += WebSocket_OnError;

            _ws.Connect();
            await _tcs.Task;
        }

        public async void WebSocket_OnError(object sender, ErrorEventArgs args)
        {
            ++_haltQueue;
            if (args?.Exception != null)
            {
                Console.WriteLine("Oops, I've lost touch with Discord!");
                Console.WriteLine($"Here is the error, if you need it: ");
                Console.WriteLine($"{args.Exception.GetType()}: {args.Exception.Message}");
            }
            else
            {
                Console.WriteLine("Oops, I've lost touch with Discord!");
            }

            Console.WriteLine("Be patient, I'm reaching out to Discord again...");

            _timer?.Stop();
            _ws.OnMessage -= MessageReceived;
            _ws.OnError -= WebSocket_OnError;

            await SetupWebSocketAsync();
            --_haltQueue;
            Console.Error.WriteLine("[Debug]: Control request queue resumed.");
            _ = RunControlRequestQueue();
        }

        /// <summary>
        /// Sends message without passing through queue. Needed for urgent messages, such as dragons.
        /// </summary>
        /// <param name="content">Message content</param>
        /// <param name="retry">Retries if you're rate limited</param>
        /// <returns></returns>
        public async Task<string> SendMessageRawAsync(string content, bool retry = false)
        {
retry:      await FakeTyping();

            var request = new HttpRequestMessage();
            request.RequestUri = new Uri($"https://discord.com/api/v9/channels/{ChannelId}/messages");
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
            request.Headers.AcceptLanguage.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("en-US"));
            request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(Token);
            request.Headers.Add("sec-ch-ua", "\" Not;A Brand\";v=\"99\", \"Microsoft Edge\";v=\"91\", \"Chromium\";v=\"91\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-origin");
            request.Headers.Add("x-super-properties", "eyJvcyI6IldpbmRvd3MiLCJicm93c2VyIjoiQ2hyb21lIiwiZGV2aWNlIjoiIiwic3lzdGVtX2xvY2FsZSI6ImVuLVVTIiwiYnJvd3Nlcl91c2VyX2FnZW50IjoiTW96aWxsYS81LjAgKFdpbmRvd3MgTlQgMTAuMDsgV2luNjQ7IHg2NCkgQXBwbGVXZWJLaXQvNTM3LjM2IChLSFRNTCwgbGlrZSBHZWNrbykgQ2hyb21lLzkxLjAuNDQ3Mi4xMTQgU2FmYXJpLzUzNy4zNiBFZGcvOTEuMC44NjQuNTkiLCJicm93c2VyX3ZlcnNpb24iOiI5MS4wLjQ0NzIuMTE0Iiwib3NfdmVyc2lvbiI6IjEwIiwicmVmZXJyZXIiOiJodHRwczovL2Rpc2NvcmQuY29tLyIsInJlZmVycmluZ19kb21haW4iOiJkaXNjb3JkLmNvbSIsInJlZmVycmVyX2N1cnJlbnQiOiIiLCJyZWZlcnJpbmdfZG9tYWluX2N1cnJlbnQiOiIiLCJyZWxlYXNlX2NoYW5uZWwiOiJzdGFibGUiLCJjbGllbnRfYnVpbGRfbnVtYmVyIjo4OTEyOSwiY2xpZW50X2V2ZW50X3NvdXJjZSI6bnVsbH0=");
            request.Headers.Referrer = new Uri($"https://discord.com/channels/{ServerId}/{ChannelId}");
            request.Method = HttpMethod.Post;

            var nonce = _nonce++;
            request.Content = new StringContent(JsonConvert.SerializeObject(new { content = content, nonce = nonce.ToString(), tts = false }), Encoding.UTF8, "application/json");

            HttpResponseMessage response;

            try
            {
                response = await _client.SendAsync(request);
            }
            catch (HttpRequestException)
            {
                // To-Do: Make it a while loop. I'm in a hurry.
                goto retry;
            }


            var text = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<JObject>(text);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                var message = data.Value<string>("message");
                Console.Error.WriteLine("[Debug]: Too many requests sent to server.");
                Console.Error.WriteLine($"[Debug]: Discord: {message}");
                var time = data.Value<double>("retry_after");
                Console.Error.WriteLine($"[Debug]: Try again after {time} seconds.");
                await Task.Delay((int)Math.Ceiling(time * 1000));
                if (retry)
                {
                    return await SendMessageRawAsync(content, true);
                }
                else
                {
                    return "";
                }
            }

            response.EnsureSuccessStatusCode();

            return data.Value<string>("id");
        }

        public async Task<ChannelControl> RequestControlAsync()
        {
            var tcs = new TaskCompletionSource<ChannelControl>();
            _controlRequestQueue.Enqueue(tcs);
            _ = RunControlRequestQueue();
            return await tcs.Task;
        }

        public async Task RunControlRequestQueue()
        {
            if (_queueRunning) return;
            _queueRunning = true;
            Console.Error.WriteLine("[Debug]: Control request queue running...");
            while (!_controlRequestQueue.IsEmpty)
            {
                if (_haltQueue != 0)
                {
                    Console.Error.WriteLine("[Debug]: Queue halted.");
                }    
                if (_controlRequestQueue.TryDequeue(out var tcs))
                {
                    Console.Error.WriteLine("[Debug]: Dequeued.");
                    await _control.Wait();
                    Console.Error.WriteLine("[Debug]: Other owner released.");
                    _control.Invalidate();
                    Console.Error.WriteLine("[Debug]: Controller reset.");
                    lock (_control)
                    {
                        _control = new ChannelControl(this);
                        tcs.SetResult(_control);
                    }
                    Console.Error.WriteLine("[Debug]: Done.");
                }
            }
            _queueRunning = false;
        }

        private async Task FakeTyping()
        {
            try
            {
                var request = new HttpRequestMessage();
                request.RequestUri = new Uri($"https://discord.com/api/v9/channels/{ChannelId}/typing");
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
                request.Headers.AcceptLanguage.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("en-US"));
                request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(Token);
                request.Headers.Add("sec-ch-ua", "\" Not;A Brand\";v=\"99\", \"Microsoft Edge\";v=\"91\", \"Chromium\";v=\"91\"");
                request.Headers.Add("sec-ch-ua-mobile", "?0");
                request.Headers.Add("sec-fetch-dest", "empty");
                request.Headers.Add("sec-fetch-mode", "cors");
                request.Headers.Add("sec-fetch-site", "same-origin");
                request.Headers.Add("x-super-properties", "eyJvcyI6IldpbmRvd3MiLCJicm93c2VyIjoiQ2hyb21lIiwiZGV2aWNlIjoiIiwic3lzdGVtX2xvY2FsZSI6ImVuLVVTIiwiYnJvd3Nlcl91c2VyX2FnZW50IjoiTW96aWxsYS81LjAgKFdpbmRvd3MgTlQgMTAuMDsgV2luNjQ7IHg2NCkgQXBwbGVXZWJLaXQvNTM3LjM2IChLSFRNTCwgbGlrZSBHZWNrbykgQ2hyb21lLzkxLjAuNDQ3Mi4xMTQgU2FmYXJpLzUzNy4zNiBFZGcvOTEuMC44NjQuNTkiLCJicm93c2VyX3ZlcnNpb24iOiI5MS4wLjQ0NzIuMTE0Iiwib3NfdmVyc2lvbiI6IjEwIiwicmVmZXJyZXIiOiJodHRwczovL2Rpc2NvcmQuY29tLyIsInJlZmVycmluZ19kb21haW4iOiJkaXNjb3JkLmNvbSIsInJlZmVycmVyX2N1cnJlbnQiOiIiLCJyZWZlcnJpbmdfZG9tYWluX2N1cnJlbnQiOiIiLCJyZWxlYXNlX2NoYW5uZWwiOiJzdGFibGUiLCJjbGllbnRfYnVpbGRfbnVtYmVyIjo4OTEyOSwiY2xpZW50X2V2ZW50X3NvdXJjZSI6bnVsbH0=");
                request.Headers.Referrer = new Uri($"https://discord.com/channels/{ServerId}/{ChannelId}");
                request.Method = HttpMethod.Post;

                var response = await _client.SendAsync(request);
            }
            catch
            {
                Console.Error.WriteLine("[Debug]: Cannot fake typing. This might make Dank Memer, and also Discord, detect self bots.");
            }
        }

        private async void MessageReceived(object sender, MessageEventArgs e)
        {
            var payload = JsonConvert.DeserializeObject<JObject>(e.Data);

            var op = payload.Value<int>("op");
            var t = payload.Value<string>("t");

            switch (op)
            {
                case 10:
                    var heartbeatInterval = payload["d"].Value<int>("heartbeat_interval");
                    Console.Error.WriteLine($"[Debug]: Channel connected to WebSocket, pinging every {heartbeatInterval} ms");
                    _timer = Heartbeat(heartbeatInterval);
                break;
            }

            switch (t)
            {
                case "MESSAGE_CREATE":
                {
                    var id = payload["d"].Value<string>("id");
                    var channelId = payload["d"].Value<string>("channel_id");
                    if (channelId != ChannelId) break;
                    var trueMessage = (await FetchMessageAsync(id))[0];
                    var author = trueMessage["author"].Value<string>("username");
                    var content = trueMessage.Value<string>("content");
                    //referenced_message
                    var referencedMessage = trueMessage["referenced_message"];
                    ++_haltQueue;
                    var eventTcs = new TaskCompletionSource<object>();
                    var deferral = new Deferral(eventTcs);
                    MessageReceive?.Invoke(this, new MessageReceiveEventArgs{ Message = trueMessage, Deferral = deferral });
                    deferral.StartWaiting();
                    await eventTcs.Task;
                    --_haltQueue;
                    _ = RunControlRequestQueue();
                    // Tasks will only know messages AFTER the events processer.
                    lock (_control)
                    {
                         _ = _control.Receive(id, trueMessage);
                    }
                        //Console.WriteLine($"{author}: {content}");
                }            
                break;
    			case "MESSAGE_UPDATE":
				{
					var id = payload["d"].Value<string>("id");
                    var channelId = payload["d"].Value<string>("channel_id");
                    if (channelId != ChannelId) break;
                    var trueMessage = (await FetchMessageAsync(id))[0];
                    lock (_control)
                    {
                        _control.Update(id, trueMessage);
                    }
                }
				break;
            }
        }

        private async Task<JArray> FetchMessageAsync(string id)
        {
            while (true)
            {
                var request = new HttpRequestMessage();
                request.RequestUri = new Uri($"https://discord.com/api/v9/channels/{ChannelId}/messages?limit=1&around={id}");
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("*/*"));
                request.Headers.AcceptLanguage.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("en-US"));
                request.Headers.Authorization = System.Net.Http.Headers.AuthenticationHeaderValue.Parse(Token);
                request.Headers.Add("sec-ch-ua", "\" Not;A Brand\";v=\"99\", \"Microsoft Edge\";v=\"91\", \"Chromium\";v=\"91\"");
                request.Headers.Add("sec-ch-ua-mobile", "?0");
                request.Headers.Add("sec-fetch-dest", "empty");
                request.Headers.Add("sec-fetch-mode", "cors");
                request.Headers.Add("sec-fetch-site", "same-origin");
                request.Headers.Add("x-super-properties", "eyJvcyI6IldpbmRvd3MiLCJicm93c2VyIjoiQ2hyb21lIiwiZGV2aWNlIjoiIiwic3lzdGVtX2xvY2FsZSI6ImVuLVVTIiwiYnJvd3Nlcl91c2VyX2FnZW50IjoiTW96aWxsYS81LjAgKFdpbmRvd3MgTlQgMTAuMDsgV2luNjQ7IHg2NCkgQXBwbGVXZWJLaXQvNTM3LjM2IChLSFRNTCwgbGlrZSBHZWNrbykgQ2hyb21lLzkxLjAuNDQ3Mi4xMTQgU2FmYXJpLzUzNy4zNiBFZGcvOTEuMC44NjQuNTkiLCJicm93c2VyX3ZlcnNpb24iOiI5MS4wLjQ0NzIuMTE0Iiwib3NfdmVyc2lvbiI6IjEwIiwicmVmZXJyZXIiOiJodHRwczovL2Rpc2NvcmQuY29tLyIsInJlZmVycmluZ19kb21haW4iOiJkaXNjb3JkLmNvbSIsInJlZmVycmVyX2N1cnJlbnQiOiIiLCJyZWZlcnJpbmdfZG9tYWluX2N1cnJlbnQiOiIiLCJyZWxlYXNlX2NoYW5uZWwiOiJzdGFibGUiLCJjbGllbnRfYnVpbGRfbnVtYmVyIjo4OTEyOSwiY2xpZW50X2V2ZW50X3NvdXJjZSI6bnVsbH0=");
                request.Headers.Referrer = new Uri($"https://discord.com/channels/{ServerId}/{ChannelId}/{id}");
                request.Method = HttpMethod.Get;

                var response = await _client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"[Debug]: Failed to fetch message {id}: {response.StatusCode}");
                    continue;
                }

                var str = await response.Content.ReadAsStringAsync();

                Debug.WriteLine(str);

                return JsonConvert.DeserializeObject<JArray>(str);
            }
        }

        private Timer Heartbeat(int milliseconds)
        {
            var timer = new Timer(milliseconds);
            timer.Elapsed += (sender, args) =>
            {
                try
                {
                    Console.Beep();
                    _ws.Send(JsonConvert.SerializeObject(new { op = 1, d = (string)null }));
                }
                catch (InvalidOperationException)
                {
                    WebSocket_OnError(null, null);
                }
            };
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
            return timer;
        }
    }
}
