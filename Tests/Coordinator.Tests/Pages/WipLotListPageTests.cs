using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;
using Moq;
using Coordinator.Data;
using Coordinator.Models;
using Coordinator.Pages;

namespace Coordinator.Tests.Pages;

/// <summary>
/// WipLotList ページのテストケース
/// </summary>
public class WipLotListPageTests
{
    private CoordinatorDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CoordinatorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CoordinatorDbContext(options);

        // サンプルWIPデータを追加
        context.DcWips.AddRange(
            new DcWip
            {
                Id = 1,
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
                TargetPPID = "GSIO3F4",
                State = "Ready",
                Next1 = "Step1",
                Next2 = "Step2",
                Next3 = "Step3"
            },
            new DcWip
            {
                Id = 2,
                Priority = 3,
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
                TargetPPID = "GSIO3F4",
                State = "Waiting",
                Next1 = "Step1",
                Next2 = "Step2",
                Next3 = "Step3"
            },
            new DcWip
            {
                Id = 3,
                Priority = 7,
                Technology = "T6-MV",
                Carrier = "C22669",
                LotId = "JM86148.1",
                Qty = 25,
                PartName = "WA0037-FN46-V-S-1",
                CurrentStage = "BL-AN",
                CurrentStep = "DAN01",
                TargetStage = "G-SIO",
                TargetStep = "FDP01",
                TargetEqpId = "DVETC26",
                TargetPPID = "GSIO3F4",
                State = "Ready",
                Next1 = "Step1",
                Next2 = "Step2",
                Next3 = "Step3"
            }
        );

        // バッチに登録済みのキャリア
        context.DcBatches.Add(new DcBatch
        {
            Id = 1,
            BatchId = "20231115001",
            Step = 1,
            CarrierId = "C22668",
            EqpId = "DVETC25",
            PPID = "PPID1",
            IsProcessed = 0,
            CreatedAt = DateTime.Now
        });

        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task OnGetAsync_指定装置のWIPロット一覧が取得できること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WipLotListModel(context)
        {
            EqpName = "DVETC25"
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.WipLots);
        // C22668はバッチに登録済みなので除外される
        Assert.Single(pageModel.WipLots); // C22667のみ
        Assert.Equal("C22667", pageModel.WipLots[0].Carrier);
    }

    [Fact]
    public async Task OnGetAsync_登録済みキャリアが除外されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WipLotListModel(context)
        {
            EqpName = "DVETC25"
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.DoesNotContain(pageModel.WipLots, w => w.Carrier == "C22668");
    }

    [Fact]
    public async Task OnGetAsync_優先度順にソートされていること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WipLotListModel(context)
        {
            EqpName = "DVETC25"
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        // Priority昇順でソート（低い方が優先度高い）
        for (int i = 0; i < pageModel.WipLots.Count - 1; i++)
        {
            Assert.True(pageModel.WipLots[i].Priority <= pageModel.WipLots[i + 1].Priority);
        }
    }

    [Fact]
    public async Task OnGetAsync_装置名が指定されていない場合は空リストを返すこと()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WipLotListModel(context)
        {
            EqpName = null
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.WipLots);
        Assert.Empty(pageModel.WipLots);
    }

    [Fact]
    public async Task OnGetAsync_存在しない装置名の場合は空リストを返すこと()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WipLotListModel(context)
        {
            EqpName = "NONEXISTENT"
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.WipLots);
        Assert.Empty(pageModel.WipLots);
    }

    [Fact]
    public void OnPost_キャリアが選択されていない場合は同じページにリダイレクトすること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new WipLotListModel(context)
        {
            EqpName = "DVETC25",
            SelectedIndices = new List<int>(),
            TempData = new Mock<ITempDataDictionary>().Object
        };

        // Act
        var result = pageModel.OnPost();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var redirectResult = result as RedirectToPageResult;
        Assert.Null(redirectResult?.PageName); // 同じページ
    }

    [Fact]
    public void OnPost_選択されたキャリアがCreateBatchに渡されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var tempDataMock = new Mock<ITempDataDictionary>();

        var pageModel = new WipLotListModel(context)
        {
            EqpName = "DVETC25",
            SelectedIndices = new List<int> { 0 },
            WipData = new List<WipDataItem>
            {
                new WipDataItem
                {
                    Carrier = "C22667",
                    LotId = "JM86146.1",
                    Technology = "T6-MV",
                    Qty = 25,
                    State = "Ready",
                    Next1 = "Step1",
                    Next2 = "Step2",
                    Next3 = "Step3"
                }
            },
            TempData = tempDataMock.Object
        };

        // Act
        var result = pageModel.OnPost();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var redirectResult = result as RedirectToPageResult;
        Assert.Equal("CreateBatch", redirectResult?.PageName);
        Assert.NotNull(redirectResult?.RouteValues);
        Assert.True(redirectResult.RouteValues.ContainsKey("lotIds"));
        Assert.Equal("JM86146.1", redirectResult.RouteValues["lotIds"]);
    }

    [Fact]
    public void OnPost_複数キャリア選択時に正しく処理されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var tempDataMock = new Mock<ITempDataDictionary>();

        var pageModel = new WipLotListModel(context)
        {
            EqpName = "DVETC25",
            SelectedIndices = new List<int> { 0, 1 },
            WipData = new List<WipDataItem>
            {
                new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" },
                new WipDataItem { Carrier = "C22669", LotId = "JM86148.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
            },
            TempData = tempDataMock.Object
        };

        // Act
        var result = pageModel.OnPost();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var redirectResult = result as RedirectToPageResult;
        Assert.Equal("CreateBatch", redirectResult?.PageName);
        Assert.NotNull(redirectResult?.RouteValues);
        Assert.Contains("JM86146.1", redirectResult.RouteValues["lotIds"]?.ToString());
        Assert.Contains("JM86148.1", redirectResult.RouteValues["lotIds"]?.ToString());
    }

    [Fact]
    public void OnPost_無効なインデックスが除外されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var tempDataMock = new Mock<ITempDataDictionary>();

        var pageModel = new WipLotListModel(context)
        {
            EqpName = "DVETC25",
            SelectedIndices = new List<int> { 0, 999 }, // 999は無効
            WipData = new List<WipDataItem>
            {
                new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
            },
            TempData = tempDataMock.Object
        };

        // Act
        var result = pageModel.OnPost();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var redirectResult = result as RedirectToPageResult;
        Assert.Equal("CreateBatch", redirectResult?.PageName);
        // 無効なインデックスは除外され、有効なデータのみが渡される
        Assert.Equal("JM86146.1", redirectResult?.RouteValues?["lotIds"]);
    }
}
