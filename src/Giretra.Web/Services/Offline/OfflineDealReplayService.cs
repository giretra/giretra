using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.State;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Replay;

namespace Giretra.Web.Services.Offline;

/// <summary>
/// Offline mode has no persisted matches, so this serves a deterministic demo deal for any
/// match id: the cards are dealt and played legally by the engine from a seed derived from
/// the match id and deal number. Lets the replay UI and the clip renderer run locally.
/// </summary>
public sealed class OfflineDealReplayService : IDealReplayService
{
    private const int DemoTotalDeals = 10;

    public Task<DealReplayResult> GetReplayAsync(Guid matchId, int dealNumber, Guid userId)
    {
        if (dealNumber < 1 || dealNumber > DemoTotalDeals)
            return Task.FromResult(DealReplayResult.Of(DealReplayStatus.DealNotFound));

        var seed = HashCode.Combine(matchId, dealNumber);
        var random = new Random(seed);

        var gameMode = (GameMode)random.Next(0, 6);
        var dealer = (PlayerPosition)random.Next(0, 4);
        var firstLeader = dealer.Next();

        var deck = Deck.CreateShuffled(random);
        var players = new Dictionary<PlayerPosition, Player>();
        foreach (var position in Enum.GetValues<PlayerPosition>())
        {
            var (cards, rest) = deck.Deal(8);
            deck = rest;
            players[position] = Player.Create(position, cards);
        }

        var state = HandState.Create(gameMode, firstLeader);
        var plays = new List<PlayedCard>(DealReplayAssembler.PlaysPerDeal);
        while (state.CurrentTrick is { CurrentPlayer: { } current } trick)
        {
            var valid = PlayValidator.GetValidPlays(players[current], trick, gameMode);
            var card = valid[random.Next(valid.Count)];
            players[current] = players[current].RemoveCard(card);
            plays.Add(new PlayedCard(current, card));
            state = state.PlayCard(card);
        }

        var assembled = DealReplayAssembler.Assemble(gameMode, plays);
        var announcer = (Team)random.Next(0, 2);
        var team1Won = assembled.Team1CardPoints > assembled.Team2CardPoints;

        var replay = new DealReplayResponse
        {
            MatchId = matchId,
            DealNumber = dealNumber,
            TotalDeals = DemoTotalDeals,
            IsRanked = false,
            Dealer = dealer,
            GameMode = gameMode,
            Multiplier = MultiplierState.Normal,
            AnnouncerTeam = announcer,
            Team1ScoreBefore = (dealNumber - 1) * 2,
            Team2ScoreBefore = (dealNumber - 1) * 3,
            Team1MatchPointsEarned = team1Won ? 2 : 0,
            Team2MatchPointsEarned = team1Won ? 0 : 2,
            Team1CardPoints = assembled.Team1CardPoints,
            Team2CardPoints = assembled.Team2CardPoints,
            WasSweep = state.SweepingTeam != null,
            SweepingTeam = state.SweepingTeam,
            IsInstantWin = false,
            Players =
            [
                DemoPlayer(PlayerPosition.Bottom, "You", isBot: false),
                DemoPlayer(PlayerPosition.Left, "Rakoto", isBot: true),
                DemoPlayer(PlayerPosition.Top, "Partner", isBot: false),
                DemoPlayer(PlayerPosition.Right, "Rasoa", isBot: true),
            ],
            Hands = assembled.Hands,
            Tricks = assembled.Tricks,
        };

        return Task.FromResult(DealReplayResult.Success(replay));
    }

    private static DealReplayPlayerResponse DemoPlayer(PlayerPosition position, string name, bool isBot) => new()
    {
        Position = position,
        Team = position.GetTeam(),
        DisplayName = name,
        IsBot = isBot,
        PlayerId = null,
    };
}
