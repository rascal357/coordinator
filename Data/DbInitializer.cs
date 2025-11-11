using Coordinator.Models;

namespace Coordinator.Data;

public static class DbInitializer
{
    public static void Initialize(CoordinatorDbContext context)
    {
        // Check if database already has data
        if (context.DcEqps.Any())
        {
            return; // DB has been seeded
        }

        // Add sample equipment
        var equipments = new DcEqp[]
        {
            new DcEqp { Name = "DVETC25", Type = "G_SIO", Line = "A" },
            new DcEqp { Name = "DVETC26", Type = "G_SIO", Line = "A" },
            new DcEqp { Name = "DVETC27", Type = "G_SIO", Line = "B" },
            new DcEqp { Name = "DVETC28", Type = "G_SIO", Line = "B" },
            new DcEqp { Name = "DVETC38", Type = "G_POLY", Line = "A" },
            new DcEqp { Name = "DVETC39", Type = "G_POLY", Line = "A" },
        };
        context.DcEqps.AddRange(equipments);
        context.SaveChanges();

        // Add sample WIP data
        var wips = new DcWip[]
        {
            new DcWip
            {
                Priority = 5,
                Technology = "T6-MV",
                Carrier = "C22667",
                LotId = "JM86146.1",
                Qty = 25,
                PartName = "WA0037-FN46-V-S-1",
                CurrentStage = "BL-AN",
                CurrentStep = "DAN01",
                TargetStage = "G-SIO",
                TargetStep = "FDP01",
                TargetEqpId = "DVETC25",
                TargetPPID = "GSIO3F4"
            },
            new DcWip
            {
                Priority = 5,
                Technology = "T6-MV",
                Carrier = "C22668",
                LotId = "JM86147.1",
                Qty = 25,
                PartName = "WA0037-FN46-V-S-1",
                CurrentStage = "BL-AN",
                CurrentStep = "DAN01",
                TargetStage = "G-SIO",
                TargetStep = "FDP01",
                TargetEqpId = "DVETC25",
                TargetPPID = "GSIO3F4"
            }
        };
        context.DcWips.AddRange(wips);
        context.SaveChanges();

        // Add sample carrier steps
        var carrierSteps = new DcCarrierStep[]
        {
            new DcCarrierStep { Carrier = "C22667", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcCarrierStep { Carrier = "C22667", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" },
            new DcCarrierStep { Carrier = "C22667", Qty = 25, Step = 3, EqpId = "DVETC27", PPID = "PPID3" },
            new DcCarrierStep { Carrier = "C22667", Qty = 25, Step = 4, EqpId = "DVETC28", PPID = "PPID4" },
            new DcCarrierStep { Carrier = "C22668", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcCarrierStep { Carrier = "C22668", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" },
            new DcCarrierStep { Carrier = "C22668", Qty = 25, Step = 3, EqpId = "DVETC27", PPID = "PPID3" },
            new DcCarrierStep { Carrier = "C22668", Qty = 25, Step = 4, EqpId = "DVETC28", PPID = "PPID4" },
        };
        context.DcCarrierSteps.AddRange(carrierSteps);
        context.SaveChanges();

        // Add sample actual processing data
        var actls = new DcActl[]
        {
            new DcActl { EqpId = "DVETC38", LotId = "SY79874.1", LotType = "PS", TrackInTime = DateTime.Now.AddHours(-3) },
            new DcActl { EqpId = "DVETC38", LotId = "SY79872.1", LotType = "PS", TrackInTime = DateTime.Now.AddHours(-3) },
            new DcActl { EqpId = "DVETC38", LotId = "SY79906.1", LotType = "PS", TrackInTime = DateTime.Now.AddHours(-3) },
            new DcActl { EqpId = "DVETC38", LotId = "SY78841.1", LotType = "PS", TrackInTime = DateTime.Now.AddHours(-3) },
            new DcActl { EqpId = "DVETC38", LotId = "SY79885.1", LotType = "PS", TrackInTime = DateTime.Now.AddHours(-3) },
            new DcActl { EqpId = "DVETC38", LotId = "SY79903.1", LotType = "PS", TrackInTime = DateTime.Now.AddHours(-3) },
            new DcActl { EqpId = "DVETC38", LotId = "SY78840.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-15) },
            new DcActl { EqpId = "DVETC38", LotId = "SY79506.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-15) },
            new DcActl { EqpId = "DVETC38", LotId = "SY78842.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-15) },
            new DcActl { EqpId = "DVETC38", LotId = "SY78360.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-15) },
            new DcActl { EqpId = "DVETC38", LotId = "SY79472.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-15) },
            new DcActl { EqpId = "DVETC38", LotId = "SY79509.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-15) },
        };
        context.DcActls.AddRange(actls);
        context.SaveChanges();
    }
}
