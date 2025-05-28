namespace CruSibyl.Core.Domain;

public enum ScanStatus
{
    /// <summary>
    /// The scan is currently in progress.
    /// </summary>
    InProgress,

    /// <summary>
    /// The scan has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The scan has failed due to an error.
    /// </summary>
    Failed,
}