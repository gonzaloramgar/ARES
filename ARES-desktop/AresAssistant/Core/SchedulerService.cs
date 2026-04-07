using System.Windows.Threading;

namespace AresAssistant.Core;

public sealed class SchedulerService
{
    private readonly ScheduledTaskStore _store;
    private readonly Func<ScheduledTaskItem, Task> _runTask;
    private readonly DispatcherTimer _timer;
    private bool _tickInProgress;

    public bool Enabled { get; set; } = true;

    public SchedulerService(ScheduledTaskStore store, Func<ScheduledTaskItem, Task> runTask)
    {
        _store = store;
        _runTask = runTask;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!Enabled || _tickInProgress) return;
        _tickInProgress = true;

        try
        {
            var now = DateTime.Now;
            var hhmm = now.ToString("HH:mm");

            var dueTasks = _store.GetAll()
                .Where(t => t.Enabled && t.Time == hhmm)
                .Where(task => task.LastRunAt is not { } last
                    || last.ToString("yyyy-MM-dd HH:mm") != now.ToString("yyyy-MM-dd HH:mm"))
                .ToList();

            if (dueTasks.Count == 0)
                return;

            foreach (var task in dueTasks)
            {
                task.LastRunAt = now;
                _store.Upsert(task);
            }

            var executions = dueTasks.Select(async task =>
            {
                try { await _runTask(task).ConfigureAwait(false); }
                catch { /* best effort scheduler */ }
            });

            await Task.WhenAll(executions).ConfigureAwait(false);
        }
        finally
        {
            _tickInProgress = false;
        }
    }
}
