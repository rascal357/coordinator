namespace Coordinator.Models;

public class BatchProcessingOptions
{
    public const string SectionName = "BatchProcessing";

    public bool Enabled { get; set; } = true;
    public int UpdateIntervalSeconds { get; set; } = 30;
}
