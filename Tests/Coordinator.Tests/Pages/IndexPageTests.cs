using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Coordinator.Data;
using Coordinator.Models;
using Coordinator.Pages;

namespace Coordinator.Tests.Pages;

/// <summary>
/// Index (Dashboard) ページのテストケース
/// </summary>
public class IndexPageTests
{
    private CoordinatorDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<CoordinatorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new CoordinatorDbContext(options);

        // サンプルデータを追加
        context.DcEqps.AddRange(
            new DcEqp { Id = 1, Name = "DVETC25", Type = "G_SIO", Line = "A", Note = "テスト装置1" },
            new DcEqp { Id = 2, Name = "DVETC26", Type = "G_SIO", Line = "A", Note = "テスト装置2" },
            new DcEqp { Id = 3, Name = "DVETC27", Type = "G_SIO", Line = "B", Note = "テスト装置3" },
            new DcEqp { Id = 4, Name = "DVETC28", Type = "G_POLY", Line = "B", Note = "テスト装置4" }
        );
        context.SaveChanges();

        return context;
    }

    [Fact]
    public async Task OnGetAsync_装置一覧が正常に取得できること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new IndexModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.Equipments);
        Assert.Equal(4, pageModel.Equipments.Count);
    }

    [Fact]
    public async Task OnGetAsync_TYPEフィルタリングが正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new IndexModel(context)
        {
            TypeFilter = new List<string> { "G_SIO" }
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.Equal(3, pageModel.Equipments.Count);
        Assert.All(pageModel.Equipments, eqp => Assert.Equal("G_SIO", eqp.Type));
    }

    [Fact]
    public async Task OnGetAsync_LINEフィルタリングが正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new IndexModel(context)
        {
            LineFilter = "A"
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.Equal(2, pageModel.Equipments.Count);
        Assert.All(pageModel.Equipments, eqp => Assert.Equal("A", eqp.Line));
    }

    [Fact]
    public async Task OnGetAsync_TYPE_LINE複合フィルタリングが正常に動作すること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new IndexModel(context)
        {
            TypeFilter = new List<string> { "G_SIO" },
            LineFilter = "A"
        };

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.Equal(2, pageModel.Equipments.Count);
        Assert.All(pageModel.Equipments, eqp =>
        {
            Assert.Equal("A", eqp.Line);
            Assert.Equal("G_SIO", eqp.Type);
        });
    }

    [Fact]
    public async Task OnGetAsync_フィルタなしの場合全装置が取得できること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new IndexModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.Equal(4, pageModel.Equipments.Count);
    }

    [Fact]
    public async Task OnGetAsync_TypeとLineのリストが正しく取得できること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new IndexModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.Types);
        Assert.NotNull(pageModel.Lines);
        Assert.Equal(2, pageModel.Types.Count);
        Assert.Equal(2, pageModel.Lines.Count);
        Assert.Contains("G_SIO", pageModel.Types);
        Assert.Contains("G_POLY", pageModel.Types);
        Assert.Contains("A", pageModel.Lines);
        Assert.Contains("B", pageModel.Lines);
    }

    [Fact]
    public async Task OnGetAsync_装置がTYPE_NAME順にソートされていること()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var pageModel = new IndexModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        for (int i = 0; i < pageModel.Equipments.Count - 1; i++)
        {
            var current = pageModel.Equipments[i];
            var next = pageModel.Equipments[i + 1];

            // Typeでソートされているかチェック
            Assert.True(
                string.Compare(current.Type, next.Type, StringComparison.Ordinal) <= 0,
                $"Type順序エラー: {current.Type} > {next.Type}");

            // 同じTypeの場合はNameでソートされているかチェック
            if (current.Type == next.Type)
            {
                Assert.True(
                    string.Compare(current.Name, next.Name, StringComparison.Ordinal) <= 0,
                    $"Name順序エラー: {current.Name} > {next.Name}");
            }
        }
    }

    [Fact]
    public async Task OnGetAsync_データベースが空の場合でもエラーが発生しないこと()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CoordinatorDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new CoordinatorDbContext(options);
        var pageModel = new IndexModel(context);

        // Act
        await pageModel.OnGetAsync();

        // Assert
        Assert.NotNull(pageModel.Equipments);
        Assert.Empty(pageModel.Equipments);
        Assert.NotNull(pageModel.Types);
        Assert.Empty(pageModel.Types);
        Assert.NotNull(pageModel.Lines);
        Assert.Empty(pageModel.Lines);
    }
}
