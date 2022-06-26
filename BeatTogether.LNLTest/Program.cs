namespace BeatTogether.LNLTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var tester = new LNLTester();
            tester.Run().Wait();
        }
    }
}