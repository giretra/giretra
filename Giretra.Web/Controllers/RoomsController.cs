using Giretra.Web.Models.Requests;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Giretra.Web.Controllers;

/// <summary>
/// Controller for managing game rooms.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    /// <summary>
    /// Lists all rooms.
    /// </summary>
    [HttpGet]
    public ActionResult<RoomListResponse> GetRooms()
    {
        return Ok(_roomService.GetAllRooms());
    }

    /// <summary>
    /// Gets a specific room by ID.
    /// </summary>
    [HttpGet("{roomId}")]
    public ActionResult<RoomResponse> GetRoom(string roomId)
    {
        var room = _roomService.GetRoom(roomId);
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
        var response = _roomService.CreateRoom(request);
        return CreatedAtAction(nameof(GetRoom), new { roomId = response.Room.RoomId }, response);
    }

    /// <summary>
    /// Deletes a room.
    /// </summary>
    [HttpDelete("{roomId}")]
    public ActionResult DeleteRoom(string roomId, [FromQuery] string clientId)
    {
        if (string.IsNullOrEmpty(clientId))
            return BadRequest("clientId is required");

        if (!_roomService.DeleteRoom(roomId, clientId))
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Joins a room as a player.
    /// </summary>
    [HttpPost("{roomId}/join")]
    public ActionResult<JoinRoomResponse> JoinRoom(string roomId, [FromBody] JoinRoomRequest request)
    {
        var response = _roomService.JoinRoom(roomId, request);
        if (response == null)
            return BadRequest("Unable to join room. Room may be full or game already started.");

        return Ok(response);
    }

    /// <summary>
    /// Joins a room as a watcher.
    /// </summary>
    [HttpPost("{roomId}/watch")]
    public ActionResult<JoinRoomResponse> WatchRoom(string roomId, [FromBody] JoinRoomRequest request)
    {
        var response = _roomService.WatchRoom(roomId, request);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    /// <summary>
    /// Leaves a room.
    /// </summary>
    [HttpPost("{roomId}/leave")]
    public ActionResult LeaveRoom(string roomId, [FromBody] LeaveRoomRequest request)
    {
        if (!_roomService.LeaveRoom(roomId, request.ClientId))
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Starts the game in a room.
    /// </summary>
    [HttpPost("{roomId}/start")]
    public ActionResult<StartGameResponse> StartGame(string roomId, [FromBody] StartGameRequest request)
    {
        var (response, error) = _roomService.StartGame(roomId, request.ClientId);
        if (response == null)
            return BadRequest(new { error = error ?? "Unable to start game" });

        return Ok(response);
    }
}
