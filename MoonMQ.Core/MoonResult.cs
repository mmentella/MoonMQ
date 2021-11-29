namespace MoonMQ.Core
{
    public sealed partial class MoonResult
    {
        public MoonResult(int term, bool success)
        {
            Term = term;
            Success = success;
        }
    }
}
