namespace Coordinator.Models;

/// <summary>
/// WIP data item used for passing data from WipLotList to CreateBatch
/// </summary>
public class WipDataItem
{
    public string Carrier { get; set; } = "";
    public string LotId { get; set; } = "";
    public string Technology { get; set; } = "";
    public int Qty { get; set; }
    public string TargetStage { get; set; } = "";
    public string TargetStep { get; set; } = "";
    public string State { get; set; } = "";
    public string Next1 { get; set; } = "";
    public string Next2 { get; set; } = "";
    public string Next3 { get; set; } = "";
}
