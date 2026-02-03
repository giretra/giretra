using Giretra.Core.Players;

namespace Giretra.Benchmark.Factories;

/// <summary>
/// Factory that creates RandomPlayerAgent instances with optional seeding for reproducibility.
/// </summary>
public sealed class RandomPlayerAgentFactory : IPlayerAgentFactory
{
    private readonly int? _baseSeed;
    private int _counter;

    public string AgentName => "RandomPlayer";

    /// <summary>
    /// Creates a factory with optional base seed for reproducibility.
    /// </summary>
    /// <param name="seed">Base seed. If provided, agents will be created with deterministic seeds.</param>
    public RandomPlayerAgentFactory(int? seed = null)
    {
        _baseSeed = seed;
    }

    public IPlayerAgent Create(PlayerPosition position)
    {
        if (_baseSeed.HasValue)
        {
            var seed = unchecked(_baseSeed.Value + _counter++);
            return new RandomPlayerAgent(position, seed);
        }

        return new RandomPlayerAgent(position);
    }
}
