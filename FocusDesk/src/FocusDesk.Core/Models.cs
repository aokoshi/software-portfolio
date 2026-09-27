namespace FocusDesk.Core;

public enum WorkStatus { Planned, Active, Done }
public enum Priority { Low, Normal, High }
public record Project(long Id, string Name, string Color);
public record WorkItem(long Id, long ProjectId, string Title, string Notes, WorkStatus Status,
    Priority Priority, DateOnly? DueDate, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, bool Archived);
public record FocusSession(string Id, long? TaskId, string TaskTitle, DateTimeOffset StartedAt,
    DateTimeOffset EndedAt, int Seconds, bool Completed);
public record TimerDraft(string Id, long? TaskId, string TaskTitle, DateTimeOffset StartedAt, int TargetSeconds, int ElapsedSeconds);

// Monotonic elapsed time is supplied by the desktop layer; tests can supply a deterministic clock.
public sealed class FocusClock(Func<double> seconds)
{
    double anchor, accumulated;
    public bool Running { get; private set; }
    public int TargetSeconds { get; private set; } = 25 * 60;
    public double Elapsed => Math.Clamp(accumulated + (Running ? Math.Max(0, seconds() - anchor) : 0), 0, TargetSeconds);
    public int Remaining => Math.Max(0, (int)Math.Ceiling(TargetSeconds - Elapsed));
    public bool Finished => Elapsed >= TargetSeconds;
    public void Configure(int target, int elapsed = 0)
    {
        if (target is < 60 or > 7200 || elapsed < 0 || elapsed > target) throw new ArgumentException("Некорректная длительность.");
        TargetSeconds = target; accumulated = elapsed; Running = false;
    }
    public void Start() { if (!Running && !Finished) { anchor = seconds(); Running = true; } }
    public void Pause() { accumulated = Elapsed; Running = false; }
}
