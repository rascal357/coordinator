using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;
using Moq;
using Coordinator.Data;
using Coordinator.Models;
using Coordinator.Pages;
using System.Text.Json;

namespace Coordinator.Tests.Pages;

/// <summary>
/// CreateBatch ページのテストケース
/// </summary>
public class CreateBatchPageTests
{
    private CoordinatorDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CoordinatorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CoordinatorDbContext(options);

        // サンプルキャリアステップデータを追加
        context.DcCarrierSteps.AddRange(
            new DcCarrierStep { Id = 1, Carrier = "C22667", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcCarrierStep { Id = 2, Carrier = "C22667", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" },
            new DcCarrierStep { Id = 3, Carrier = "C22667", Qty = 25, Step = 3, EqpId = "DVETC27", PPID = "PPID3" },
            new DcCarrierStep { Id = 4, Carrier = "C22667", Qty = 25, Step = 4, EqpId = "DVETC28", PPID = "PPID4" },
            new DcCarrierStep { Id = 5, Carrier = "C22668", Qty = 25, Step = 1, EqpId = "DVETC25", PPID = "PPID1" },
            new DcCarrierStep { Id = 6, Carrier = "C22668", Qty = 25, Step = 2, EqpId = "DVETC26", PPID = "PPID2" }
        );

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
                TargetPPID = "GSIO3F4"
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
                TargetPPID = "GSIO3F4"
            }
        );

        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task OnGetAsync_キャリアステップ情報が正しく表示されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var tempDataMock = new Mock<ITempDataDictionary>();

        var wipDataList = new List<WipDataItem>
        {
            new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
        };

        tempDataMock.Setup(t => t["SelectedWipData"])
            .Returns(JsonSerializer.Serialize(wipDataList));

        var pageModel = new CreateBatchModel(context)
        {
            LotIds = "JM86146.1",
            TempData = tempDataMock.Object
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.CarrierSteps);
        Assert.Single(pageModel.CarrierSteps);

        var carrierStep = pageModel.CarrierSteps[0];
        Assert.Equal("C22667", carrierStep.Carrier);
        Assert.Equal("JM86146.1", carrierStep.LotId);
        Assert.Equal(25, carrierStep.Qty);

        // 4ステップすべてが設定されていること
        Assert.NotNull(carrierStep.Step1);
        Assert.NotNull(carrierStep.Step2);
        Assert.NotNull(carrierStep.Step3);
        Assert.NotNull(carrierStep.Step4);
    }

    [Fact]
    public async Task OnGetAsync_複数キャリアの処理が正しく行われること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var tempDataMock = new Mock<ITempDataDictionary>();

        var wipDataList = new List<WipDataItem>
        {
            new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" },
            new WipDataItem { Carrier = "C22668", LotId = "JM86147.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
        };

        tempDataMock.Setup(t => t["SelectedWipData"])
            .Returns(JsonSerializer.Serialize(wipDataList));

        var pageModel = new CreateBatchModel(context)
        {
            LotIds = "JM86146.1,JM86147.1",
            TempData = tempDataMock.Object
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.CarrierSteps);
        Assert.Equal(2, pageModel.CarrierSteps.Count);
        Assert.Contains(pageModel.CarrierSteps, cs => cs.Carrier == "C22667");
        Assert.Contains(pageModel.CarrierSteps, cs => cs.Carrier == "C22668");
    }

    [Fact]
    public async Task OnGetAsync_重複LotIdが除外されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var tempDataMock = new Mock<ITempDataDictionary>();

        var wipDataList = new List<WipDataItem>
        {
            new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
        };

        tempDataMock.Setup(t => t["SelectedWipData"])
            .Returns(JsonSerializer.Serialize(wipDataList));

        var pageModel = new CreateBatchModel(context)
        {
            LotIds = "JM86146.1,JM86146.1", // 重複
            TempData = tempDataMock.Object
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.Single(pageModel.CarrierSteps); // 重複は除外される
    }

    [Fact]
    public async Task OnPostAsync_バッチが正常に作成されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new CreateBatchModel(context)
        {
            LotIds = "JM86146.1",
            CarrierData = new List<WipDataItem>
            {
                new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
            }
        };

        // フォームデータのモック
        var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "ppid_C22667_1", "PPID1" },
            { "eqpid_C22667_1", "DVETC25" }
        });

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.Request.Form).Returns(formCollection);

        var pageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
        {
            HttpContext = httpContextMock.Object
        };

        pageModel.PageContext = pageContext;

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(context.DcBatches.Any());
        Assert.True(context.DcBatchMembers.Any());

        var batch = context.DcBatches.First();
        Assert.Equal("C22667", batch.CarrierId);
        Assert.Equal("DVETC25", batch.EqpId);
        Assert.Equal("PPID1", batch.PPID);
        Assert.False(batch.IsProcessed);
    }

    [Fact]
    public async Task OnPostAsync_同じBatchIdが全レコードに設定されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new CreateBatchModel(context)
        {
            LotIds = "JM86146.1",
            CarrierData = new List<WipDataItem>
            {
                new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
            }
        };

        var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "ppid_C22667_1", "PPID1" },
            { "eqpid_C22667_1", "DVETC25" },
            { "ppid_C22667_2", "PPID2" },
            { "eqpid_C22667_2", "DVETC26" }
        });

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.Request.Form).Returns(formCollection);

        var pageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
        {
            HttpContext = httpContextMock.Object
        };

        pageModel.PageContext = pageContext;

        // Act
        await pageModel.OnPostAsync();

        // Assert
        var batches = context.DcBatches.ToList();
        Assert.True(batches.Count >= 2);

        var batchId = batches[0].BatchId;
        Assert.All(batches, b => Assert.Equal(batchId, b.BatchId));
    }

    [Fact]
    public async Task OnPostAsync_同じCreatedAtが全レコードに設定されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new CreateBatchModel(context)
        {
            LotIds = "JM86146.1",
            CarrierData = new List<WipDataItem>
            {
                new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
            }
        };

        var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "ppid_C22667_1", "PPID1" },
            { "eqpid_C22667_1", "DVETC25" },
            { "ppid_C22667_2", "PPID2" },
            { "eqpid_C22667_2", "DVETC26" }
        });

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.Request.Form).Returns(formCollection);

        var pageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
        {
            HttpContext = httpContextMock.Object
        };

        pageModel.PageContext = pageContext;

        // Act
        await pageModel.OnPostAsync();

        // Assert
        var batches = context.DcBatches.ToList();
        var createdAt = batches[0].CreatedAt;
        Assert.All(batches, b => Assert.Equal(createdAt, b.CreatedAt));
    }

    [Fact]
    public async Task OnPostAsync_BatchMemberが正しく作成されること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new CreateBatchModel(context)
        {
            LotIds = "JM86146.1",
            CarrierData = new List<WipDataItem>
            {
                new WipDataItem { Carrier = "C22667", LotId = "JM86146.1", Technology = "T6-MV", Qty = 25, State = "Ready", Next1 = "Step1", Next2 = "Step2", Next3 = "Step3" }
            }
        };

        var formCollection = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            { "ppid_C22667_1", "PPID1" },
            { "eqpid_C22667_1", "DVETC25" }
        });

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(c => c.Request.Form).Returns(formCollection);

        var pageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext
        {
            HttpContext = httpContextMock.Object
        };

        pageModel.PageContext = pageContext;

        // Act
        await pageModel.OnPostAsync();

        // Assert
        var batchMember = context.DcBatchMembers.FirstOrDefault();
        Assert.NotNull(batchMember);
        Assert.Equal("C22667", batchMember.CarrierId);
        Assert.Equal("JM86146.1", batchMember.LotId);
        Assert.Equal(25, batchMember.Qty);
        Assert.Equal("T6-MV", batchMember.Technology);
    }

    [Fact]
    public async Task OnPostAsync_LotIdsが空の場合はIndexにリダイレクトすること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new CreateBatchModel(context)
        {
            LotIds = null
        };

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var redirectResult = result as RedirectToPageResult;
        Assert.Equal("Index", redirectResult?.PageName);
    }
}
