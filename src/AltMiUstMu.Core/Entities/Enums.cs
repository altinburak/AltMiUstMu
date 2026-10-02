namespace AltMiUstMu.Core.Entities;

public enum SeasonStatus
{
    Upcoming = 0,
    Active = 1,
    Finished = 2,
}

public enum Conference
{
    East = 0,
    West = 1,
}

public enum PickSide
{
    Over = 0,
    Under = 1,
}

public enum RecordSource
{
    Api = 0,
    Manual = 1,
}

public enum SyncRunStatus
{
    Running = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3,
}

public enum SyncTrigger
{
    Cli = 0,
    Cron = 1,
    Admin = 2,
}
