using System.Diagnostics;

namespace WindowsUsageCleanupAssistant.Services;

public sealed class ProgramUsageTrackerService : IProgramUsageTracker
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ObservedMinutesPerPoll = TimeSpan.FromSeconds(30);

    private readonly IUsageRepository _usageRepository;
    private readonly object _syncRoot = new();
    private readonly HashSet<string> _activeRecordKeys = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;
    private Task? _backgroundTask;

    public ProgramUsageTrackerService(IUsageRepository usageRepository)
    {
        _usageRepository = usageRepository;
    }

    public event EventHandler? UsageUpdated;

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_backgroundTask is not null)
            {
                return;
            }

            _usageRepository.Initialize();
            _cts = new CancellationTokenSource();
            _backgroundTask = RunAsync(_cts.Token);
        }
    }

    public async Task StopAsync()
    {
        Task? backgroundTask;

        lock (_syncRoot)
        {
            if (_backgroundTask is null || _cts is null)
            {
                return;
            }

            _cts.Cancel();
            backgroundTask = _backgroundTask;
            _backgroundTask = null;
            _cts = null;
        }

        try
        {
            await backgroundTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        await PollProcessesAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await PollProcessesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private Task PollProcessesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var observedAtUtc = DateTime.UtcNow;
        var currentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var records = new Dictionary<string, (string ProcessName, string ExecutablePath)>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var processName = GetProcessName(process);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    continue;
                }

                var executablePath = TryGetExecutablePath(process);
                var recordKey = BuildRecordKey(processName, executablePath);

                currentKeys.Add(recordKey);
                if (!records.ContainsKey(recordKey))
                {
                    records[recordKey] = (processName, executablePath);
                }
            }
        }

        foreach (var entry in records)
        {
            var isNewLaunch = false;

            lock (_syncRoot)
            {
                isNewLaunch = _activeRecordKeys.Add(entry.Key);
            }

            _usageRepository.RecordObservation(
                entry.Value.ProcessName,
                entry.Value.ExecutablePath,
                observedAtUtc,
                ObservedMinutesPerPoll.TotalMinutes,
                isNewLaunch);
        }

        lock (_syncRoot)
        {
            _activeRecordKeys.RemoveWhere(recordKey => !currentKeys.Contains(recordKey));
        }

        UsageUpdated?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    private static string GetProcessName(Process process)
    {
        var processName = process.ProcessName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";
    }

    private static string TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildRecordKey(string processName, string executablePath)
    {
        return string.IsNullOrWhiteSpace(executablePath)
            ? $"name:{processName.ToLowerInvariant()}"
            : $"path:{executablePath.ToLowerInvariant()}";
    }
}
