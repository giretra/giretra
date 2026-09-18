using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Web.Domain;
using Giretra.Web.Players;
using Giretra.Web.Services;
using NSubstitute;

namespace Giretra.Web.Tests;

/// <summary>
/// The pending-action completion sources must not run the game loop inline on
/// the thread that resolves them (the HTTP request handling the player's
/// action), otherwise that request only returns once the engine has applied
/// the move, notified every agent and reached the next await.
/// </summary>
public sealed class WebApiPlayerAgentContinuationTests
{
    private static GameSession CreateSession() => new()
    {
        GameId = "game_test",
        RoomId = "room_test",
        ClientPositions = new Dictionary<string, PlayerPosition> { ["client1"] = PlayerPosition.Bottom },
        PlayerComposition = new Dictionary<PlayerPosition, MatchPlayerInfo>()
    };

    [Fact]
    public async Task ResolvingPendingCard_DoesNotRunAgentContinuationOnResolvingThread()
    {
        // Run without a synchronization context, like ASP.NET Core request threads,
        // so continuations are eligible for inlining exactly as in production.
        await Task.Run(async () =>
        {
            var session = CreateSession();
            var agent = new WebApiPlayerAgent(PlayerPosition.Bottom, "client1", session,
                Substitute.For<INotificationService>(), TimeSpan.FromSeconds(10));
            var card = new Card(CardRank.Ace, CardSuit.Hearts);

            var chooseTask = agent.ChooseCardAsync([card], null!, null!, [card]);
            var pending = session.PendingActions[PlayerPosition.Bottom];

            var gate = new object();
            var continued = false;
            var continuation = chooseTask.ContinueWith(_ =>
            {
                // Runs inline on the resolving thread only if continuations are
                // inlined; Monitor is re-entrant, so that path would pass through.
                lock (gate) { }
                continued = true;
            }, TaskContinuationOptions.ExecuteSynchronously);

            lock (gate)
            {
                pending.PlayCardTcs!.TrySetResult(card);
                Assert.False(continued, "the agent continuation ran synchronously on the resolving thread");
            }

            await continuation;
            Assert.True(continued);
            Assert.Equal(card, await chooseTask);
        });
    }
}
