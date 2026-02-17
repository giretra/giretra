using Giretra.Core.Players;
using Giretra.Model.Entities;
using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

/// <summary>
/// Controller for managing game rooms.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly INotificationService _notifications;
    private readonly AiPlayerRegistry _aiRegistry;

    public RoomsController(IRoomService roomService, INotificationService notifications, AiPlayerRegistry aiRegistry)
    {
        _roomService = roomService;
        _notifications = notifications;
        _aiRegistry = aiRegistry;
    }

    /// <summary>
    /// Lists all rooms.
    /// </summary>
    [HttpGet]
    public ActionResult<RoomListResponse> GetRooms()
    {
        var user = GetAuthenticatedUser();
        return Ok(_roomService.GetAllRooms(user.Id));
    }

    /// <summary>
    /// Gets a specific room by ID.
    /// </summary>
    [HttpGet("{roomId}")]
    public ActionResult<RoomResponse> GetRoom(string roomId)
    {
        var user = GetAuthenticatedUser();
        var room = _roomService.GetRoom(roomId, user.Id);
        if (room == null)
            return NotFound();

        return Ok(room);
    }

    /// <summary>
    /// Creates a new room.
    /// </summary>
    [HttpPost]
    public ActionResult<JoinRoomResponse> CreateRoom([FromBody] CreateRoomRequest request)
    {
        var user = GetAuthenticatedUser();
        var response = _roomService.CreateRoom(request, user.EffectiveDisplayName, user.Id);
        return CreatedAtAction(nameof(GetRoom), new { roomId = response.Room.RoomId }, response);
    }

    /// <summary>
    /// Deletes a room.
    /// </summary>
    [HttpDelete("{roomId}")]
    public ActionResult DeleteRoom(string roomId)
    {
        var user = GetAuthenticatedUser();
        if (!_roomService.DeleteRoom(roomId, user.Id))
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Joins a room as a player.
    /// </summary>
    [HttpPost("{roomId}/join")]
    public async Task<ActionResult<JoinRoomResponse>> JoinRoom(string roomId, [FromBody] JoinRoomRequest request)
    {
        var user = GetAuthenticatedUser();
        var response = _roomService.JoinRoom(roomId, request, user.EffectiveDisplayName, user.Id);
        if (response == null)
            return BadRequest("Unable to join room. Room may be full, game already started, or seat is invite-only.");

        if (response.Position.HasValue)
            await _notifications.NotifyPlayerJoinedAsync(roomId, user.EffectiveDisplayName, response.Position.Value);

        return Ok(response);
    }

    /// <summary>
    /// Joins a room as a watcher.
    /// </summary>
    [HttpPost("{roomId}/watch")]
    public ActionResult<JoinRoomResponse> WatchRoom(string roomId, [FromBody] JoinRoomRequest request)
    {
        var user = GetAuthenticatedUser();
        var response = _roomService.WatchRoom(roomId, request, user.EffectiveDisplayName);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    /// <summary>
    /// Leaves a room.
    /// </summary>
    [HttpPost("{roomId}/leave")]
    public async Task<ActionResult> LeaveRoom(string roomId, [FromBody] LeaveRoomRequest request)
    {
        var (removed, playerName, position) = _roomService.LeaveRoom(roomId, request.ClientId);
        if (!removed)
            return NotFound();

        if (playerName != null && position.HasValue)
            await _notifications.NotifyPlayerLeftAsync(roomId, playerName, position.Value);

        return NoContent();
    }

    /// <summary>
    /// Starts the game in a room.
    /// </summary>
    [HttpPost("{roomId}/start")]
    public ActionResult<StartGameResponse> StartGame(string roomId, [FromBody] StartGameRequest request)
    {
        var user = GetAuthenticatedUser();
        var (response, error) = _roomService.StartGame(roomId, user.Id);
        if (response == null)
            return BadRequest(new { error = error ?? "Unable to start game" });

        return Ok(response);
    }

    /// <summary>
    /// Sets the access mode for a seat.
    /// </summary>
    [HttpPost("{roomId}/seats/{position}/mode")]
    public async Task<ActionResult> SetSeatMode(string roomId, PlayerPosition position, [FromBody] SetSeatModeRequest request)
    {
        var user = GetAuthenticatedUser();
        var (success, error) = _roomService.SetSeatMode(roomId, user.Id, position, request.AccessMode);
        if (!success)
            return BadRequest(new { error });

        await _notifications.NotifySeatModeChangedAsync(roomId, position, request.AccessMode);
        return Ok();
    }

    /// <summary>
    /// Generates an invite token for a seat.
    /// </summary>
    [HttpPost("{roomId}/seats/{position}/invite")]
    public async Task<ActionResult<InviteTokenResponse>> GenerateInvite(string roomId, PlayerPosition position)
    {
        var user = GetAuthenticatedUser();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = _roomService.GenerateInviteToken(roomId, user.Id, position, baseUrl);
        if (response == null)
            return BadRequest(new { error = "Unable to generate invite. Check that you are the room owner and the room is waiting." });

        await _notifications.NotifySeatModeChangedAsync(roomId, position, Domain.SeatAccessMode.InviteOnly);
        return Ok(response);
    }

    /// <summary>
    /// Kicks a player from a seat.
    /// </summary>
    [HttpPost("{roomId}/seats/{position}/kick")]
    public async Task<ActionResult> KickPlayer(string roomId, PlayerPosition position)
    {
        var user = GetAuthenticatedUser();
        var (success, error, kickedPosition, playerName) = _roomService.KickPlayer(roomId, user.Id, position);
        if (!success)
            return BadRequest(new { error });

        if (kickedPosition.HasValue && playerName != null)
            await _notifications.NotifyPlayerKickedAsync(roomId, playerName, kickedPosition.Value);

        return Ok();
    }

    /// <summary>
    /// Gets available AI player types.
    /// </summary>
    [HttpGet("/api/ai-types")]
    public ActionResult<IReadOnlyList<AiTypeInfo>> GetAiTypes()
    {
        return Ok(_aiRegistry.GetAvailableTypes());
    }

    private User GetAuthenticatedUser() => (User)HttpContext.Items["GiretraUser"]!;
}
