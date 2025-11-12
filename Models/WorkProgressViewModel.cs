namespace Coordinator.Models;

public class EquipmentProgressViewModel
{
    public string EqpName { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public string? Note { get; set; }
    public List<ProcessItem> InProcess { get; set; } = new();
    public List<ProcessItem> Waiting { get; set; } = new();
    public List<ProcessItem> Reserved1 { get; set; } = new();
    public List<ProcessItem> Reserved2 { get; set; } = new();
    public List<ProcessItem> Reserved3 { get; set; } = new();
    public string? Reserved1BatchId { get; set; }
    public string? Reserved2BatchId { get; set; }
    public string? Reserved3BatchId { get; set; }
}

public class ProcessItem
{
    public string Carrier { get; set; } = string.Empty;
    public string Lot { get; set; } = string.Empty;
    public int Qty { get; set; }
    public string PPID { get; set; } = string.Empty;
    public string NextFurnace { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
