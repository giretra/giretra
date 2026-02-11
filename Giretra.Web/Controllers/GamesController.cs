using Giretra.Core.Cards;
using Giretra.Core.Negotiation;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

/// <summary>
/// Controller for game actions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly IGameService _gameService;

    public GamesController(IGameService gameService)
    {
        _gameService = gameService;
    }

    /// <summary>
    /// Gets the full game state.
    /// </summary>
    [HttpGet("{gameId}")]
    public ActionResult<GameStateResponse> GetGameState(string gameId)
    {
        var response = _gameService.GetGameState(gameId);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    /// <summary>
    /// Gets the player-specific view of the game state.
    /// </summary>
    [HttpGet("{gameId}/player/{clientId}")]
    public ActionResult<PlayerStateResponse> GetPlayerState(string gameId, string clientId)
    {
        var response = _gameService.GetPlayerState(gameId, clientId);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    /// <summary>
    /// Gets the watcher view of the game state (hides player hands).
    /// </summary>
    [HttpGet("{gameId}/watch")]
    public ActionResult<WatcherStateResponse> GetWatcherState(string gameId)
    {
        var response = _gameService.GetWatcherState(gameId);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    /// <summary>
    /// Submits a cut decision.
    /// </summary>
    [HttpPost("{gameId}/cut")]
    public ActionResult SubmitCut(string gameId, [FromBody] CutRequest request)
    {
        if (request.Position < 6 || request.Position > 26)
            return BadRequest("Cut position must be between 6 and 26.");

        if (!_gameService.SubmitCut(gameId, request.ClientId, request.Position, request.FromTop))
            return BadRequest("Invalid cut submission. Either it's not your turn or the game doesn't exist.");

        return Ok();
    }

    /// <summary>
    /// Submits a negotiation action.
    /// </summary>
    [HttpPost("{gameId}/negotiate")]
    public ActionResult SubmitNegotiation(string gameId, [FromBody] NegotiateRequest request)
    {
        var session = _gameService.GetGame(gameId);
        if (session == null)
            return NotFound();

        var position = session.GetPositionForClient(request.ClientId);
        if (position == null)
            return BadRequest("Client is not a player in this game.");

        // Parse the action
        NegotiationAction action;
        switch (request.ActionType.ToLowerInvariant())
        {
            case "accept":
                action = new AcceptAction(position.Value);
                break;
            case "announce":
                if (request.Mode == null)
                    return BadRequest("Mode is required for Announce action.");
                action = new AnnouncementAction(position.Value, request.Mode.Value);
                break;
            case "double":
                if (request.Mode == null)
                    return BadRequest("Mode is required for Double action.");
                action = new DoubleAction(position.Value, request.Mode.Value);
                break;
            case "redouble":
                if (request.Mode == null)
                    return BadRequest("Mode is required for Redouble action.");
                action = new RedoubleAction(position.Value, request.Mode.Value);
                break;
            default:
                return BadRequest($"Unknown action type: {request.ActionType}");
        }

        if (!_gameService.SubmitNegotiation(gameId, request.ClientId, action))
            return BadRequest("Invalid negotiation action. Either it's not your turn or the action is not valid.");

        return Ok();
    }

    /// <summary>
    /// Submits a card play.
    /// </summary>
    [HttpPost("{gameId}/play")]
    public ActionResult SubmitCardPlay(string gameId, [FromBody] PlayCardRequest request)
    {
        var card = new Card(request.Rank, request.Suit);

        if (!_gameService.SubmitCardPlay(gameId, request.ClientId, card))
            return BadRequest("Invalid card play. Either it's not your turn or the card is not valid.");

        return Ok();
    }

    /// <summary>
    /// Submits confirmation to continue to the next deal.
    /// </summary>
    [HttpPost("{gameId}/continue")]
    public ActionResult SubmitContinueDeal(string gameId, [FromBody] ContinueDealRequest request)
    {
        if (!_gameService.SubmitContinueDeal(gameId, request.ClientId))
            return BadRequest("Invalid continue request. Either it's not your turn or the game is not waiting for confirmation.");

        return Ok();
    }

    /// <summary>
    /// Submits confirmation to continue after match ends.
    /// </summary>
    [HttpPost("{gameId}/continue-match")]
    public ActionResult SubmitContinueMatch(string gameId, [FromBody] ContinueDealRequest request)
    {
        if (!_gameService.SubmitContinueMatch(gameId, request.ClientId))
            return BadRequest("Invalid continue match request. Either it's not your turn or the game is not waiting for confirmation.");

        return Ok();
    }
}
