namespace MoonMQ.Core
{
    public enum ServerState
    {
        Follower,
        Candidate,
        Leader,
        Running,
        Stopped
    }
}
