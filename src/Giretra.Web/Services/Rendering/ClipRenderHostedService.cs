using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Rendering;

/// <summary>Polls the clip queue and renders one clip at a time.</summary>
public sealed class ClipRenderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ClipRenderer _renderer;
    private readonly ClipRenderOptions _options;
    private readonly ILogger<ClipRenderHostedService> _logger;

    public ClipRenderHostedService(
        IServiceScopeFactory scopes,
        ClipRenderer renderer,
        IOptions<ClipRenderOptions> options,
        ILogger<ClipRenderHostedService> logger)
    {
        _scopes = scopes;
        _renderer = renderer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        _logger.LogInformation("Clip render loop started (pages from {WebBaseUrl})", _options.WebBaseUrl);
        var idleDelay = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            var rendered = false;
            try
            {
                rendered = await RenderNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Typically the database is not reachable yet or the clips table does not exist.
                _logger.LogWarning(ex, "Clip queue unavailable, retrying in 10 s");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            if (!rendered)
                await Task.Delay(idleDelay, stoppingToken);
        }
    }

    private async Task<bool> RenderNextAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopes.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IClipJobQueue>();

        var job = await queue.ClaimAsync(stoppingToken);
        if (job == null)
            return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.JobTimeoutSeconds));
        var watch = Stopwatch.StartNew();

        try
        {
            var output = await _renderer.RenderAsync(job, timeout.Token);
            await queue.CompleteAsync(job, output, stoppingToken);
            _logger.LogInformation("Clip {ShortId} rendered in {Elapsed} ms (poster {Poster}, video {Video})",
                job.ShortId, watch.ElapsedMilliseconds, output.HasPoster, output.HasVideo);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var reason = ex is OperationCanceledException ? "Render timed out" : ex.Message;
            _logger.LogError(ex, "Clip {ShortId} failed (attempt {Attempt}): {Reason}", job.ShortId, job.Attempts, reason);
            await _renderer.ResetAsync();
            await queue.FailAsync(job, reason, stoppingToken);
        }

        return true;
    }
}
