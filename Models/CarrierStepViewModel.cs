namespace Coordinator.Models;

public class CarrierStepViewModel
{
    public string Carrier { get; set; } = string.Empty;
    public int Qty { get; set; }
    public string LotId { get; set; } = string.Empty;
    public string Technology { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Next1 { get; set; } = string.Empty;
    public string Next2 { get; set; } = string.Empty;
    public string Next3 { get; set; } = string.Empty;
    public StepInfo Step1 { get; set; } = new();
    public StepInfo Step2 { get; set; } = new();
    public StepInfo Step3 { get; set; } = new();
    public StepInfo Step4 { get; set; } = new();
}

public class StepInfo
{
    public string EqpId { get; set; } = string.Empty;
    public string PPID { get; set; } = string.Empty;
}
