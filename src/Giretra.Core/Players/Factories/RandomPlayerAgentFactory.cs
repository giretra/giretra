using Giretra.Core.Players.Agents;

namespace Giretra.Core.Players.Factories;

/// <summary>
/// Factory that creates RandomPlayerAgent instances with optional seeding for reproducibility.
/// </summary>
public sealed class RandomPlayerAgentFactory : IPlayerAgentFactory
{
    private int? _baseSeed;
    private int _counter;

    public Guid Identifier { get; } = Guid.Parse("55c0e9ae-bd69-4c48-6a93-8187ea811929");

    public string AgentName => "RandomPlayer";
    public string DisplayName => "Baomijijy";

    public int? Seed
    {
        get => _baseSeed;
        set { _baseSeed = value; _counter = 0; }
    }

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
