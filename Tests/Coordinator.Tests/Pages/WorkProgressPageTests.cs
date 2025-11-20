using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Coordinator.Data;
using Coordinator.Models;
using Coordinator.Pages;

namespace Coordinator.Tests.Pages;

/// <summary>
/// WorkProgress ページのテストケース
/// </summary>
public class WorkProgressPageTests
{
    private CoordinatorDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CoordinatorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CoordinatorDbContext(options);

        // 装置データ
        context.DcEqps.AddRange(
            new DcEqp { Id = 1, Name = "DVETC25", Type = "G_SIO", Line = "A", Note = "テスト装置1" },
            new DcEqp { Id = 2, Name = "DVETC26", Type = "G_SIO", Line = "B", Note = "テスト装置2" },
            new DcEqp { Id = 3, Name = "DVETC38", Type = "G_POLY", Line = "A", Note = "テスト装置3" }
        );

        // 実績データ (In Process / Waiting用)
        var baseTime = DateTime.Now.AddHours(-3);
        context.DcActls.AddRange(
            // In Process グループ (3時間前)
            new DcActl { Id = 1, EqpId = "DVETC38", LotId = "SY79874.1", LotType = "PS", TrackInTime = baseTime, Carrier = "C22667", Qty = 25, PPID = "GSIO3F4", Next = "DVETC39", Location = "Bay1", EndTime = baseTime.AddHours(2).ToString("HH:mm") },
            new DcActl { Id = 2, EqpId = "DVETC38", LotId = "SY79872.1", LotType = "PS", TrackInTime = baseTime.AddMinutes(2), Carrier = "C22668", Qty = 25, PPID = "GSIO3F4", Next = "DVETC39", Location = "Bay1", EndTime = baseTime.AddHours(2).AddMinutes(2).ToString("HH:mm") },
            new DcActl { Id = 3, EqpId = "DVETC38", LotId = "SY79906.1", LotType = "PS", TrackInTime = baseTime.AddMinutes(4), Carrier = "C22669", Qty = 25, PPID = "GSIO3F4", Next = "DVETC39", Location = "Bay1", EndTime = baseTime.AddHours(2).AddMinutes(4).ToString("HH:mm") },

            // Waiting グループ (15分前)
            new DcActl { Id = 4, EqpId = "DVETC38", LotId = "SY78840.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-15), Carrier = "C22670", Qty = 25, PPID = "GSIO3F5", Next = "DVETC40", Location = "Bay2", EndTime = DateTime.Now.AddMinutes(105).ToString("HH:mm") },
            new DcActl { Id = 5, EqpId = "DVETC38", LotId = "SY79506.1", LotType = "PS", TrackInTime = DateTime.Now.AddMinutes(-13), Carrier = "C22671", Qty = 25, PPID = "GSIO3F5", Next = "DVETC40", Location = "Bay2", EndTime = DateTime.Now.AddMinutes(107).ToString("HH:mm") }
        );

        // WIPデータ
        context.DcWips.AddRange(
            new DcWip
            {
                Id = 1,
                Priority = 5,
                Technology = "T6-MV",
                Carrier = "C22667",
                LotId = "SY79874.1",
                Qty = 25,
                PartName = "WA0037-FN46-V-S-1",
                CurrentStage = "BL-AN",
                CurrentStep = "DAN01",
                TargetStage = "G-SIO",
                TargetStep = "FDP01",
                TargetEqpId = "DVETC38",
                TargetPPID = "GSIO3F4",
                State = "Ready",
                Next1 = "Step1",
                Next2 = "Step2",
                Next3 = "Step3"
            }
        );

        // バッチデータ (Reserved用)
        var batchId1 = "20231115001";
        var batchId2 = "20231115002";
        var createdAt1 = DateTime.Now.AddHours(-1);
        var createdAt2 = DateTime.Now.AddMinutes(-30);

        context.DcBatches.AddRange(
            new DcBatch { Id = 1, BatchId = batchId1, Step = 1, CarrierId = "C22667", EqpId = "DVETC25", PPID = "PPID1", IsProcessed = 0, CreatedAt = createdAt1 },
            new DcBatch { Id = 2, BatchId = batchId2, Step = 1, CarrierId = "C22668", EqpId = "DVETC25", PPID = "PPID1", IsProcessed = 0, CreatedAt = createdAt2 }
        );

        context.DcBatchMembers.AddRange(
            new DcBatchMember { Id = 1, BatchId = batchId1, CarrierId = "C22667", LotId = "JM86146.1", Qty = 25, Technology = "T6-MV" },
            new DcBatchMember { Id = 2, BatchId = batchId2, CarrierId = "C22668", LotId = "JM86147.1", Qty = 25, Technology = "T6-MV" }
        );

        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task OnGetAsync_進捗データが正常に取得できること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.EquipmentsByType);
        Assert.NotEmpty(pageModel.EquipmentsByType);
    }

    [Fact]
    public async Task OnGetAsync_TYPE別にグルーピングされていること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.True(pageModel.EquipmentsByType.ContainsKey("G_SIO"));
        Assert.True(pageModel.EquipmentsByType.ContainsKey("G_POLY"));
    }

    [Fact]
    public async Task OnGetAsync_TYPEフィルタリングが正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context)
        {
            TypeFilter = new List<string> { "G_SIO" }
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.Single(pageModel.EquipmentsByType);
        Assert.True(pageModel.EquipmentsByType.ContainsKey("G_SIO"));
        Assert.False(pageModel.EquipmentsByType.ContainsKey("G_POLY"));
    }

    [Fact]
    public async Task OnGetAsync_LINEフィルタリングが正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context)
        {
            LineFilter = "A"
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        foreach (var equipmentGroup in pageModel.EquipmentsByType.Values)
        {
            Assert.All(equipmentGroup, eqp => Assert.Equal("A", eqp.Line));
        }
    }

    [Fact]
    public async Task OnGetAsync_InProcessとWaitingが正しく分類されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        var polyEquipments = pageModel.EquipmentsByType["G_POLY"];
        var dvetc38 = polyEquipments.First(e => e.EqpName == "DVETC38");

        Assert.NotNull(dvetc38.InProcess);
        Assert.NotEmpty(dvetc38.InProcess);
        Assert.NotNull(dvetc38.Waiting);
        Assert.NotEmpty(dvetc38.Waiting);

        // In Processは古いグループ
        Assert.Equal(3, dvetc38.InProcess.Count);
        // Waitingは新しいグループ
        Assert.Equal(2, dvetc38.Waiting.Count);
    }

    [Fact]
    public async Task OnGetAsync_Reservedが正しく表示されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        var sioEquipments = pageModel.EquipmentsByType["G_SIO"];
        var dvetc25 = sioEquipments.First(e => e.EqpName == "DVETC25");

        Assert.NotNull(dvetc25.Reserved1);
        Assert.NotEmpty(dvetc25.Reserved1);
        Assert.NotNull(dvetc25.Reserved2);
        Assert.NotEmpty(dvetc25.Reserved2);
    }

    [Fact]
    public async Task OnGetAsync_Reserved順序がCreatedAt昇順であること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        var sioEquipments = pageModel.EquipmentsByType["G_SIO"];
        var dvetc25 = sioEquipments.First(e => e.EqpName == "DVETC25");

        // Reserved1が最も古いバッチ
        Assert.NotNull(dvetc25.Reserved1BatchId);
        Assert.Equal("20231115001", dvetc25.Reserved1BatchId);
        // Reserved2が2番目に古いバッチ
        Assert.NotNull(dvetc25.Reserved2BatchId);
        Assert.Equal("20231115002", dvetc25.Reserved2BatchId);
    }

    [Fact]
    public async Task OnPostUpdateNoteAsync_Note更新が正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        var result = await pageModel.OnPostUpdateNoteAsync("DVETC25", "新しいNote");

        // Assert
        Assert.IsType<JsonResult>(result);
        var jsonResult = result as JsonResult;
        var value = jsonResult?.Value;
        Assert.NotNull(value);

        var equipment = await context.DcEqps.FirstAsync(e => e.Name == "DVETC25");
        Assert.Equal("新しいNote", equipment.Note);
    }

    [Fact]
    public async Task OnPostUpdateNoteAsync_装置名が空の場合はエラーを返すこと()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        var result = await pageModel.OnPostUpdateNoteAsync("", "新しいNote");

        // Assert
        Assert.IsType<JsonResult>(result);
        var jsonResult = result as JsonResult;
        var value = jsonResult?.Value;
        Assert.NotNull(value);

        var successProperty = value.GetType().GetProperty("success");
        Assert.NotNull(successProperty);
        var success = (bool?)successProperty.GetValue(value);
        Assert.False(success);
    }

    [Fact]
    public async Task OnPostDeleteBatchAsync_バッチ削除が正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);
        var batchId = "20231115001";

        // Act
        var result = await pageModel.OnPostDeleteBatchAsync(batchId);

        // Assert
        Assert.IsType<JsonResult>(result);

        // バッチが削除されていること
        Assert.False(await context.DcBatches.AnyAsync(b => b.BatchId == batchId));
        Assert.False(await context.DcBatchMembers.AnyAsync(bm => bm.BatchId == batchId));
    }

    [Fact]
    public async Task OnPostDeleteBatchAsync_BatchIdが空の場合はエラーを返すこと()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        var result = await pageModel.OnPostDeleteBatchAsync("");

        // Assert
        Assert.IsType<JsonResult>(result);
        var jsonResult = result as JsonResult;
        var value = jsonResult?.Value;
        Assert.NotNull(value);

        var successProperty = value.GetType().GetProperty("success");
        Assert.NotNull(successProperty);
        var success = (bool?)successProperty.GetValue(value);
        Assert.False(success);
    }

    [Fact]
    public async Task OnGetResetProcessedAsync_IsProcessedフラグがリセットされること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        // 一部のバッチをIsProcessed=1に設定
        var batch = await context.DcBatches.FirstAsync();
        batch.IsProcessed = 1;
        await context.SaveChangesAsync();

        var pageModel = new WorkProgressModel(context);

        // Act
        var result = await pageModel.OnGetResetProcessedAsync();

        // Assert
        Assert.IsType<JsonResult>(result);

        // すべてのバッチがIsProcessed=0になっていること
        var allBatches = await context.DcBatches.ToListAsync();
        Assert.All(allBatches, b => Assert.Equal(0, b.IsProcessed));
    }

    [Fact]
    public async Task OnGetAsync_TypesとLinesが正しく取得できること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.Types);
        Assert.NotNull(pageModel.Lines);
        Assert.Contains("G_SIO", pageModel.Types);
        Assert.Contains("G_POLY", pageModel.Types);
        Assert.Contains("A", pageModel.Lines);
        Assert.Contains("B", pageModel.Lines);
    }

    [Fact]
    public async Task OnGetRefreshAsync_進捗データの更新が正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WorkProgressModel(context);

        // Act
        var result = await pageModel.OnGetRefreshAsync();

        // Assert
        Assert.IsType<JsonResult>(result);
        Assert.NotNull(pageModel.EquipmentsByType);
        Assert.NotEmpty(pageModel.EquipmentsByType);
    }
}
