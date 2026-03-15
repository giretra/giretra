using System.Collections.Concurrent;
using Giretra.Core.State;
using Giretra.Web.Domain;
using Giretra.Web.Repositories;

namespace Giretra.Web.Services;

public sealed class ChatService : IChatService
{
    private const int MaxMessagesPerRoom = 200;
    private const int MaxContentLength = 300;

    // Rate limits per client
    private const int BurstLimit = 5;
    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(5);
    private const int MinuteLimit = 30;
    private static readonly TimeSpan MinuteWindow = TimeSpan.FromMinutes(1);
    private const int HourLimit = 500;
    private static readonly TimeSpan HourWindow = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, RoomChat> _rooms = new();
    private readonly ConcurrentDictionary<string, ClientRateLimit> _rateLimits = new();
    private readonly IRoomRepository _roomRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IRoomRepository roomRepository, IGameRepository gameRepository, ILogger<ChatService> logger)
    {
        _roomRepository = roomRepository;
        _gameRepository = gameRepository;
        _logger = logger;
    }

    public ChatMessage? SendMessage(string roomId, string clientId, string content)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null)
        {
            _logger.LogWarning("Chat send failed: room {RoomId} not found", roomId);
            return null;
        }

        var client = room.GetClient(clientId);
        if (client == null)
        {
            _logger.LogWarning("Chat send failed: client {ClientId} not found in room {RoomId}", clientId, roomId);
            return null;
        }

        if (!IsChatEnabled(roomId))
        {
            _logger.LogDebug("Chat send failed: chat disabled in room {RoomId}", roomId);
            return null;
        }

        if (IsRateLimited(clientId))
        {
            _logger.LogWarning("Chat send failed: rate limited client {ClientId} in room {RoomId}", clientId, roomId);
            return null;
        }

        var trimmed = content.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxContentLength)
        {
            _logger.LogDebug("Chat send failed: invalid content length ({Length}) in room {RoomId}", content.Trim().Length, roomId);
            return null;
        }

        var chat = _rooms.GetOrAdd(roomId, _ => new RoomChat());

        ChatMessage message;
        lock (chat.Lock)
        {
            var seq = ++chat.SequenceCounter;
            message = new ChatMessage
            {
                SequenceNumber = seq,
                SenderName = client.DisplayName,
                IsPlayer = client.IsPlayer,
                Content = trimmed,
                SentAt = DateTime.UtcNow
            };

            chat.Messages.Add(message);

            // Drop oldest beyond capacity
            if (chat.Messages.Count > MaxMessagesPerRoom)
            {
                chat.Messages.RemoveRange(0, chat.Messages.Count - MaxMessagesPerRoom);
            }
        }

        _logger.LogInformation(
            "Chat message in {RoomId} from {DisplayName} (#{SequenceNumber}): {Content}",
            roomId, client.DisplayName, message.SequenceNumber, trimmed);

        return message;
    }

    public ChatMessage AddSystemMessage(string roomId, string content)
    {
        var chat = _rooms.GetOrAdd(roomId, _ => new RoomChat());

        ChatMessage message;
        lock (chat.Lock)
        {
            var seq = ++chat.SequenceCounter;
            message = new ChatMessage
            {
                SequenceNumber = seq,
                SenderName = "",
                IsPlayer = false,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsSystem = true
            };

            chat.Messages.Add(message);

            if (chat.Messages.Count > MaxMessagesPerRoom)
            {
                chat.Messages.RemoveRange(0, chat.Messages.Count - MaxMessagesPerRoom);
            }
        }

        return message;
    }

    public IReadOnlyList<ChatMessage> GetHistory(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var chat))
            return [];

        lock (chat.Lock)
        {
            return chat.Messages.ToList();
        }
    }

    public bool IsChatEnabled(string roomId)
    {
        var room = _roomRepository.GetById(roomId);
        if (room == null) return false;

        if (room.Status == RoomStatus.Waiting) return true;

        if (room.Status != RoomStatus.Playing || room.GameSessionId == null) return true;

        var session = _gameRepository.GetById(room.GameSessionId);
        if (session == null) return true;

        var matchState = session.MatchState;
        if (matchState == null) return true;

        // Match is complete
        if (matchState.IsComplete) return true;

        var currentDeal = matchState.CurrentDeal;
        if (currentDeal == null) return true; // Between deals

        return currentDeal.Phase is DealPhase.AwaitingCut or DealPhase.Completed;
    }

    public void ClearRoom(string roomId)
    {
        _rooms.TryRemove(roomId, out _);
    }

    private bool IsRateLimited(string clientId)
    {
        var now = DateTime.UtcNow;
        var limit = _rateLimits.GetOrAdd(clientId, _ => new ClientRateLimit());

        lock (limit)
        {
            // Prune old timestamps
            limit.Timestamps.RemoveAll(t => now - t > HourWindow);

            var burstCount = 0;
            var minuteCount = 0;
            var burstCutoff = now - BurstWindow;
            var minuteCutoff = now - MinuteWindow;

            foreach (var t in limit.Timestamps)
            {
                if (t >= burstCutoff) burstCount++;
                if (t >= minuteCutoff) minuteCount++;
            }

            if (burstCount >= BurstLimit || minuteCount >= MinuteLimit || limit.Timestamps.Count >= HourLimit)
                return true;

            limit.Timestamps.Add(now);
            return false;
        }
    }

    private sealed class RoomChat
    {
        public readonly List<ChatMessage> Messages = [];
        public readonly object Lock = new();
        public long SequenceCounter;
    }

    private sealed class ClientRateLimit
    {
        public readonly List<DateTime> Timestamps = [];
    }
}
