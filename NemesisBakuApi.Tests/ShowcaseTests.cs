using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NemesisBakuApi.Controllers;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Showcase;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;
using Xunit;

namespace NemesisBakuApi.Tests;

public class ShowcaseTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly AppDbContext db;
    private readonly FakeFiles files = new();
    private readonly AdminShowcaseGroupsController admin;
    private readonly ShowcaseController storefront;

    public ShowcaseTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        admin = new(db, files, new FakeAudit(), NullLogger<AdminShowcaseGroupsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())]))
            } }
        };
        storefront = new(db);
    }

    [Theory]
    [InlineData("/birbankodeniskecidi", "birbankodeniskecidi")]
    [InlineData("  /Summer-2026  ", "summer-2026")]
    [InlineData("/Admin", null)]
    [InlineData("/products", null)]
    [InlineData("/infoAddress", null)]
    [InlineData("//evil.test", null)]
    [InlineData("foo/bar", null)]
    [InlineData("hello?x=y", null)]
    [InlineData("<script>", null)]
    public void Slugs_are_normalized_and_reserved_routes_rejected(string input, string? expected) =>
        Assert.Equal(expected, ShowcaseValidation.NormalizeSlug(input));

    [Theory]
    [InlineData("https://example.com/path?q=1", true)]
    [InlineData("http://example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,test", false)]
    [InlineData("//example.com", false)]
    [InlineData("https://user:password@example.com", false)]
    [InlineData("https://example.com/\nfoo", false)]
    public void Only_safe_absolute_web_links_are_accepted(string url, bool valid) =>
        Assert.Equal(valid, ShowcaseValidation.IsExternalUrl(url));

    [Fact]
    public async Task Create_edit_add_remove_reorder_and_delete_group()
    {
        var input = Input();
        Assert.IsType<OkObjectResult>(await admin.Create(input));
        var group = await db.ShowcaseGroups.Include(x => x.Blocks).SingleAsync();
        var first = group.Blocks.Single();
        var update = Input();
        update.Version = group.Version;
        update.Blocks = [Block("second"), new() { Id = first.Id, Shape = "portrait", TargetType = "internal", Slug = first.Slug }];
        Assert.IsType<OkObjectResult>(await admin.Update(group.Id, update));
        db.ChangeTracker.Clear();
        group = await db.ShowcaseGroups.Include(x => x.Blocks).SingleAsync();
        Assert.Equal(2, group.Blocks.Count);
        Assert.Equal("second", group.Blocks.OrderBy(x => x.DisplayOrder).First().Slug);
        Assert.IsType<OkObjectResult>(await storefront.Page("second", default));
        update.Version = group.Version;
        update.Blocks = [new() { Id = first.Id, Shape = "square", TargetType = "none" }];
        Assert.IsType<OkObjectResult>(await admin.Update(group.Id, update));
        Assert.IsType<NotFoundObjectResult>(await storefront.Page("second", default));
        Assert.IsType<NotFoundObjectResult>(await storefront.Page("first", default));
        Assert.Equal(2, files.Deleted.Count);
        group = await db.ShowcaseGroups.SingleAsync();
        Assert.IsType<OkObjectResult>(await admin.Delete(group.Id, group.Version));
        Assert.Empty(await db.ShowcaseBlocks.ToListAsync());
        Assert.Equal(4, files.Deleted.Count);
    }

    [Fact]
    public async Task Upload_failure_cleans_new_files_and_preserves_saved_images()
    {
        await admin.Create(Input());
        var group = await db.ShowcaseGroups.Include(x => x.Blocks).SingleAsync();
        var savedImage = group.Blocks.Single().ImageUrl;
        files.FailAt = files.Count + 2;
        var update = Input(); update.Version = group.Version; update.Blocks[0].Id = group.Blocks.Single().Id;
        Assert.IsType<BadRequestObjectResult>(await admin.Update(group.Id, update));
        db.ChangeTracker.Clear();
        Assert.Equal(savedImage, (await db.ShowcaseBlocks.SingleAsync()).ImageUrl);
        Assert.Single(files.Deleted);
        Assert.DoesNotContain(savedImage, files.Deleted);
    }

    [Fact]
    public async Task Duplicate_slug_and_stale_version_do_not_upload_or_overwrite()
    {
        await admin.Create(Input());
        var group = await db.ShowcaseGroups.SingleAsync();
        Assert.IsType<ConflictObjectResult>(await admin.Create(Input()));
        var update = Input(); update.Version = Guid.NewGuid();
        Assert.IsType<ConflictObjectResult>(await admin.Update(group.Id, update));
        Assert.Equal(2, files.Count);
        Assert.Single(await db.ShowcaseGroups.ToListAsync());
    }

    [Fact]
    public async Task Missing_mobile_image_and_unknown_products_rejected_before_upload()
    {
        var input = Input(); input.Blocks[0].MobileFile = null;
        Assert.IsType<BadRequestObjectResult>(await admin.Create(input));
        input = Input(); input.Blocks[0].ProductIds = [Guid.NewGuid()];
        Assert.IsType<BadRequestObjectResult>(await admin.Create(input));
        Assert.Equal(0, files.Count);
    }

    [Fact]
    public async Task Hidden_groups_and_pages_not_public_and_optional_content_is_not_invented()
    {
        var input = Input(); input.IsActive = false;
        await admin.Create(input);
        Assert.IsType<NotFoundObjectResult>(await storefront.Page("first", default));
        Assert.Equal(0, Data(await storefront.Active(default)).GetArrayLength());
        var group = await db.ShowcaseGroups.Include(x => x.Blocks).SingleAsync();
        input.Version = group.Version; input.IsActive = true; input.Blocks[0].Id = group.Blocks.Single().Id;
        input.Blocks[0].File = null; input.Blocks[0].MobileFile = null;
        await admin.Update(group.Id, input);
        var page = Data(await storefront.Page("first", default));
        Assert.Equal(JsonValueKind.Null, page.GetProperty("Title").ValueKind);
        Assert.Equal(JsonValueKind.Null, page.GetProperty("Description").ValueKind);
        Assert.Equal(0, page.GetProperty("Products").GetArrayLength());
        Assert.Equal(1, Data(await storefront.Active(default)).GetArrayLength());
    }

    [Fact]
    public async Task Reorder_preserves_whole_groups_and_rejects_stale_list()
    {
        await admin.Create(Input());
        var second = Input(); second.Name = "Second"; second.Blocks = [Block("second"), Block("third")];
        await admin.Create(second);
        var groups = await db.ShowcaseGroups.OrderBy(x => x.CreatedAt).ToListAsync();
        var order = groups.AsEnumerable().Reverse().Select(x => new ShowcaseOrderInput { Id = x.Id, Version = x.Version }).ToList();
        Assert.IsType<OkObjectResult>(await admin.Reorder(order));
        var active = Data(await storefront.Active(default));
        Assert.Equal(2, active[0].GetProperty("Blocks").GetArrayLength());
        Assert.IsType<ConflictObjectResult>(await admin.Reorder(order));
    }

    [Fact]
    public async Task Selected_products_keep_order_and_inactive_products_are_hidden()
    {
        var brand = new Brand { Name = "Test" }; var category = new Category { Name = "Test" };
        var one = new Product { Name = "One", ProductCode = "ONE", Brand = brand, Category = category, Price = 10 };
        var two = new Product { Name = "Two", ProductCode = "TWO", Brand = brand, Category = category, Price = 20 };
        db.Products.AddRange(one, two); await db.SaveChangesAsync();
        var input = Input(); input.Blocks[0].ProductIds = [two.Id, one.Id];
        await admin.Create(input);
        var products = Data(await storefront.Page("first", default)).GetProperty("Products");
        Assert.Equal("Two", products[0].GetProperty("Name").GetString());
        two.IsActive = false; await db.SaveChangesAsync();
        products = Data(await storefront.Page("first", default)).GetProperty("Products");
        Assert.Equal(1, products.GetArrayLength());
        Assert.Equal("One", products[0].GetProperty("Name").GetString());
    }

    private static JsonElement Data(IActionResult result) => JsonSerializer.SerializeToElement(
        Assert.IsType<ApiResponse<object>>(Assert.IsType<OkObjectResult>(result).Value).Data);
    private static ShowcaseGroupInput Input() => new() { Name = "Test", IsActive = true, Blocks = [Block("first")] };
    private static ShowcaseBlockInput Block(string slug) => new()
    {
        TargetType = "internal", Slug = slug,
        File = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "desktop.jpg"),
        MobileFile = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "mobileFile", "mobile.jpg")
    };
    public void Dispose() { db.Dispose(); connection.Dispose(); }
    private sealed class FakeFiles : IFileService
    {
        public int Count { get; private set; }
        public int FailAt { get; set; } = -1;
        public List<string> Deleted { get; } = [];
        public Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            if (++Count == FailAt) throw new InvalidOperationException("Simulated upload failure");
            return Task.FromResult($"https://images.example/{Count}.jpg");
        }
        public Task DeleteImageAsync(string url) { Deleted.Add(url); return Task.CompletedTask; }
    }
    private sealed class FakeAudit : IAuditLogService
    {
        public Task CreateAsync(Guid? userId, string action, string entityName, string? entityId,
            string? description, string? ipAddress, string? userAgent) => Task.CompletedTask;
    }
}
