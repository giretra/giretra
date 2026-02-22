using System.ComponentModel.DataAnnotations;

namespace Giretra.Web.Models.Requests;

public sealed class SendFriendRequestRequest
{
    [Required]
    public required string Username { get; init; }
}
