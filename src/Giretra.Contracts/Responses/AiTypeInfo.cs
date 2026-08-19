namespace Giretra.Web.Models.Responses;

/// <summary>
/// Response DTO describing an available AI player type.
/// </summary>
public record AiTypeInfo(
    string Name,
    string DisplayName,
    short Difficulty,
    int Rating,
    string? Pun,
    string? Description,
    string? Author);
