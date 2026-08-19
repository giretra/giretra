
namespace Giretra.Web.Models.Requests;

public sealed class BlockUserRequest
{
    public required string Username { get; init; }

    public string? Reason { get; init; }
}
