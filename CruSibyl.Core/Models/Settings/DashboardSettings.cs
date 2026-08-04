namespace CruSibyl.Core.Models.Settings;

public class DashboardSettings
{
    public int ConsecutiveFailureAttempts { get; set; } = 3;

    public int RecentFailureWindowHours { get; set; } = 24;

    public int HistoryWindowDays { get; set; } = 30;

    public int StaleScanDays { get; set; } = 7;
}
