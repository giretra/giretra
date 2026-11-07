namespace Giretra.Model.Enums;

public enum ClipStatus
{
    /// <summary>Queued for the render worker.</summary>
    Pending = 0,
    /// <summary>Claimed by a render worker.</summary>
    Rendering = 1,
    /// <summary>Poster (and, once rendered, video) available on disk.</summary>
    Ready = 2,
    /// <summary>Rendering failed after the allowed attempts; see <c>Error</c>.</summary>
    Failed = 3,
    /// <summary>Files deleted by retention; re-queued on the next visit.</summary>
    Evicted = 4,
    /// <summary>Taken down by the creator or a moderator.</summary>
    Removed = 5
}
