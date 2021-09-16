using Lily.Strategies;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Lily
{
    class Program
    {
        static ManualResetEvent _quitEvent = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            Console.WriteLine("Hi! I'm Lily, your professional dank player!");
            Console.WriteLine("Enter your channel link (Promise I won't spam there): ");
            var link = Console.ReadLine();
            Console.WriteLine("Enter your authorization key (Trust me, I won't send it to the Chinese government!): ");
            var authorizationKey = Console.ReadLine();

            Console.WriteLine("To help me learn to find missing words, I might collect some data.");
            Console.WriteLine("The data I collect does not include any usernames, passwords, or access tokens.");
            Console.WriteLine("It only includes messages containing questions and answers sent by Dank Memer.");
            Console.WriteLine("All data collected will be displayed in the console.");
            Console.WriteLine("By hiring me, you agree to allow me collect these data.");

            Console.WriteLine("Initializing my logic, please wait...");

            var channel = new Channel(link, authorizationKey);
            var machineLearning = new MachineLearning(channel);
            var engine = new SpamEngine(channel, 7200000, 3600000);

            engine.AddStrategy(new Work(machineLearning));
            Thread.Sleep(10000);
            engine.AddStrategy(new Beg());
            Thread.Sleep(10000);
            engine.AddStrategy(new Search());
            Thread.Sleep(10000);
            engine.AddStrategy(new Fish(machineLearning));
            Thread.Sleep(10000);
            engine.AddStrategy(new Hunt());
            Thread.Sleep(10000);
            // Experimental
            engine.AddStrategy(new Dig(machineLearning));
            Thread.Sleep(10000);
            engine.AddStrategy(new Trivia());
            Thread.Sleep(10000);

            // Guess the number game is really time
            // consuming and inefficient. Should only 
            // be enabled when aiming for 1000 wins to get
            // the multiplier.
            if (args.Contains("--enable-guess"))
            {
                engine.AddStrategy(new Guess());
                Thread.Sleep(10000);
            }

            engine.AddStrategy(new Highlow());
            Thread.Sleep(10000);
            engine.AddStrategy(new Deposit());

            _quitEvent.WaitOne();
        }
    }
}
