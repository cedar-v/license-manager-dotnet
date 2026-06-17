using LicenseManager.DotNet.Models;

namespace LicenseManager.DotNet.Services;

public sealed class HeartbeatManager
{
    private readonly HeartbeatService service;
    private readonly HeartbeatRequest request;
    private readonly TimeSpan interval;
    private readonly Func<HeartbeatResponse, Task>? onLicenseUpdated;
    private readonly Func<Exception, Task>? onError;
    private readonly Func<Task>? onPing;
    private readonly object sync = new();
    private CancellationTokenSource? cts;
    private bool paused;

    public HeartbeatManager(
        HeartbeatService service,
        HeartbeatRequest request,
        TimeSpan interval,
        Func<HeartbeatResponse, Task>? onLicenseUpdated = null,
        Func<Exception, Task>? onError = null,
        Func<Task>? onPing = null)
    {
        this.service = service;
        this.request = request;
        this.interval = interval;
        this.onLicenseUpdated = onLicenseUpdated;
        this.onError = onError;
        this.onPing = onPing;
    }

    public bool IsRunning { get; private set; }

    public void Start()
    {
        lock (sync)
        {
            if (IsRunning)
            {
                return;
            }

            cts = new CancellationTokenSource();
            IsRunning = true;
            _ = Task.Run(() => LoopAsync(cts.Token));
        }
    }

    public void Stop()
    {
        lock (sync)
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            IsRunning = false;
        }
    }

    public void Pause()
    {
        lock (sync)
        {
            paused = true;
        }
    }

    public void Resume()
    {
        lock (sync)
        {
            paused = false;
        }
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        var currentInterval = interval;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(currentInterval, cancellationToken).ConfigureAwait(false);

                lock (sync)
                {
                    if (paused)
                    {
                        continue;
                    }
                }

                try
                {
                    var response = await service.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (onPing is not null)
                    {
                        _ = Task.Run(onPing, CancellationToken.None);
                    }

                    currentInterval = response.HeartbeatInterval is > 0
                        ? TimeSpan.FromSeconds(response.HeartbeatInterval.Value)
                        : interval;

                    if (!string.IsNullOrWhiteSpace(response.LicenseFile) && onLicenseUpdated is not null)
                    {
                        _ = Task.Run(() => onLicenseUpdated(response), CancellationToken.None);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    currentInterval = Backoff(currentInterval);
                    if (onError is not null)
                    {
                        _ = Task.Run(() => onError(ex), CancellationToken.None);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            lock (sync)
            {
                IsRunning = false;
            }
        }
    }

    private static TimeSpan Backoff(TimeSpan current)
    {
        var next = TimeSpan.FromTicks(current.Ticks * 2);
        if (next > TimeSpan.FromMinutes(30))
        {
            next = TimeSpan.FromMinutes(30);
        }

        if (next < TimeSpan.FromSeconds(30))
        {
            next = TimeSpan.FromSeconds(30);
        }

        return next;
    }
}
