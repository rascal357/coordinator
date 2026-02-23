using Coordinator.Models;

namespace Coordinator.Data;

public static class DbInitializer
{
    private static readonly (string Name, string Type, string Line)[] AllEquipment =
    {
        // Row 1: Hi_temp_Pyro (LINE A)
        ("DVETD05", "Hi_temp_Pyro", "A"),
        ("DVETD30", "Hi_temp_Pyro", "A"),
        ("DVETD31", "Hi_temp_Pyro", "A"),
        ("DVETD41", "Hi_temp_Pyro", "A"),
        ("DVETD42", "Hi_temp_Pyro", "A"),
        ("DVETD43", "Hi_temp_Pyro", "A"),
        ("DVETD44", "Hi_temp_Pyro", "A"),
        ("DVETD45", "Hi_temp_Pyro", "A"),
        ("DVETD60", "Hi_temp_Pyro", "A"),
        ("DVETD65", "Hi_temp_Pyro", "A"),
        ("DVETD70", "Hi_temp_Pyro", "A"),
        ("DVETD85", "Hi_temp_Pyro", "A"),
        // Row 1: VDF_BAOX
        ("DVETD79", "VDF_BAOX", "A"),
        // Row 1: ONO_OX
        ("DVETD81", "ONO_OX", "A"),
        // Row 2: Hi_Temp_Dry_Pre
        ("DVETD48", "Hi_Temp_Dry_Pre", "A"),
        ("DVETD55", "Hi_Temp_Dry_Pre", "A"),
        ("DVETD56", "Hi_Temp_Dry_Pre", "A"),
        ("DVETD57", "Hi_Temp_Dry_Pre", "A"),
        ("DVETD71", "Hi_Temp_Dry_Pre", "A"),
        ("DVETD73", "Hi_Temp_Dry_Pre", "A"),
        // Row 2: Hi_temp_Dry_Post
        ("DVETD53", "Hi_temp_Dry_Post", "A"),
        ("DVETD54", "Hi_temp_Dry_Post", "A"),
        ("DVETD62", "Hi_temp_Dry_Post", "A"),
        ("DVETD67", "Hi_temp_Dry_Post", "A"),
        ("DVETD75", "Hi_temp_Dry_Post", "A"),
        ("DVETD76", "Hi_temp_Dry_Post", "A"),
        ("DVETD77", "Hi_temp_Dry_Post", "A"),
        ("DVETD82", "Hi_temp_Dry_Post", "A"),
        // Row 3: S_hi_temp_Pyro
        ("DVETD49", "S_hi_temp_Pyro", "A"),
        ("DVETD87", "S_hi_temp_Pyro", "A"),
        ("DVETD88", "S_hi_temp_Pyro", "A"),
        ("DVETV57", "S_hi_temp_Pyro", "A"),
        // Row 3: S_hi_temp_Dry_VHVIC
        ("DVETD04", "S_hi_temp_Dry_VHVIC", "A"),
        ("DVETD83", "S_hi_temp_Dry_VHVIC", "A"),
        // Row 3: Pyro_Gate
        ("DVETD27", "Pyro_Gate", "A"),
        ("DVETD28", "Pyro_Gate", "A"),
        ("DVETD66", "Pyro_Gate", "A"),
        ("DVETD80", "Pyro_Gate", "A"),
        ("DVETD84", "Pyro_Gate", "A"),
        // Row 3: FRAM_Pyro_Gate
        ("DVETD59", "FRAM_Pyro_Gate", "A"),
        // Row 4: VCF_DASI_ON (LINE B)
        ("DVETC07", "VCF_DASI_ON", "B"),
        ("DVETC27", "VCF_DASI_ON", "B"),
        ("DVETC33", "VCF_DASI_ON", "B"),
        ("DVETC35", "VCF_DASI_ON", "B"),
        ("DVETC36", "VCF_DASI_ON", "B"),
        ("DVETC38", "VCF_DASI_ON", "B"),
        ("DVETC51", "VCF_DASI_ON", "B"),
        ("DVETC52", "VCF_DASI_ON", "B"),
        ("DVETC68", "VCF_DASI_ON", "B"),
        ("DVETC74", "VCF_DASI_ON", "B"),
        // Row 4: IGBT_DASI
        ("DVETC77", "IGBT_DASI", "B"),
        // Row 5: VCF_POLY
        ("DVETC37", "VCF_POLY", "B"),
        ("DVETC53", "VCF_POLY", "B"),
        ("DVETC71", "VCF_POLY", "B"),
        // Row 5: ONO_SIN
        ("DVETC54", "ONO_SIN", "B"),
        ("DVETC61", "ONO_SIN", "B"),
        ("DVETC70", "ONO_SIN", "B"),
        // Row 5: ISO_SIN
        ("DVETC55", "ISO_SIN", "B"),
        ("DVETC56", "ISO_SIN", "B"),
        // Row 5: VCF_HTO
        ("DVETC57", "VCF_HTO", "B"),
        ("DVETC59", "VCF_HTO", "B"),
        // Row 5: VCF_HTO_ONO
        ("DVETC63", "VCF_HTO_ONO", "B"),
        // Row 6: G_SIO
        ("DVETC25", "G_SIO", "B"),
        ("DVETC28", "G_SIO", "B"),
        ("DVETC40", "G_SIO", "B"),
        ("DVETC60", "G_SIO", "B"),
        // Row 6: VCF_TEOS
        ("DVETC26", "VCF_TEOS", "B"),
        ("DVETC31", "VCF_TEOS", "B"),
        ("DVETC32", "VCF_TEOS", "B"),
        ("DVETC34", "VCF_TEOS", "B"),
        ("DVETC39", "VCF_TEOS", "B"),
    };

    public static void Initialize(CoordinatorDbContext context)
    {
        // 装置データが期待する件数と一致しない場合は全件置き換え
        if (context.DcEqps.Count() != AllEquipment.Length)
        {
            context.DcEqps.RemoveRange(context.DcEqps);
            context.SaveChanges();

            var equipments = AllEquipment
                .Select(d => new DcEqp { Name = d.Name, Type = d.Type, Line = d.Line })
                .ToArray();
            context.DcEqps.AddRange(equipments);
            context.SaveChanges();
        }

        // WIPデータが既に存在する場合はスキップ
        if (context.DcWips.Any())
        {
            return;
        }

        // サンプル WIP データ
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

        // サンプル CarrierStep データ
        var carrierSteps = new DcCarrierStep[]
        {
            new DcCarrierStep { Carrier = "C22667", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcCarrierStep { Carrier = "C22667", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" },
            new DcCarrierStep { Carrier = "C22668", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcCarrierStep { Carrier = "C22668", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" },
        };
        context.DcCarrierSteps.AddRange(carrierSteps);
        context.SaveChanges();

        // サンプル LotStep データ
        var lotSteps = new DcLotStep[]
        {
            new DcLotStep { LotId = "JM86146.1", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcLotStep { LotId = "JM86146.1", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" },
            new DcLotStep { LotId = "JM86147.1", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcLotStep { LotId = "JM86147.1", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" },
        };
        context.DcLotSteps.AddRange(lotSteps);
        context.SaveChanges();

        // サンプル Actl データ (DVETC38 = VCF_DASI_ON)
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
