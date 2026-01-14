namespace AlienCyborgModernTempleOS
{
    public interface IAgent
    {
        string Name { get; }
        Task<string> RunAsync(string input, CancellationToken ct);
    }
}
