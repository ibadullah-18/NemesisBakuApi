using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NemesisBakuApi.Data;
using NemesisBakuApi.DTOs.Showcase;
using NemesisBakuApi.Entities;
using NemesisBakuApi.Helpers;
using NemesisBakuApi.Services.Interfaces;

namespace NemesisBakuApi.Controllers;

[ApiController, Route("api/[controller]"), Authorize(Roles = "SuperAdmin,Admin")]
public class AdminShowcaseGroupsController(
    AppDbContext db, IFileService files, IAuditLogService audit,
    ILogger<AdminShowcaseGroupsController> logger) : ControllerBase
{
    private IQueryable<ShowcaseGroup> Groups => db.ShowcaseGroups
        .Include(x => x.Blocks).ThenInclude(x => x.Products).AsSplitQuery();

    private static object View(ShowcaseGroup group) => new
    {
        group.Id, group.Name, group.DisplayOrder, group.IsActive, group.Version,
        Blocks = group.Blocks.OrderBy(x => x.DisplayOrder).Select(x => new
        {
            x.Id, x.Shape, x.TargetType, x.Slug, x.ExternalUrl, x.ImageUrl, x.MobileImageUrl,
            x.ImageAlt, x.Title, x.Subtitle, x.Description, x.AfterProductsDescription,
            ProductIds = x.Products.OrderBy(p => p.DisplayOrder).Select(p => p.ProductId)
        })
    };

    [HttpGet]
    public async Task<IActionResult> List() => Ok(ApiResponse<object>.Ok(
        (await Groups.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id).ToListAsync()).Select(View)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var group = await Groups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return group == null ? NotFound(ApiResponse<string>.Fail("Qrup tapılmadı."))
            : Ok(ApiResponse<object>.Ok(View(group)));
    }

    [HttpPut("order")]
    public async Task<IActionResult> Reorder([FromBody] List<ShowcaseOrderInput> input)
    {
        var groups = await db.ShowcaseGroups.ToListAsync();
        if (input.Count != groups.Count || input.Select(x => x.Id).Distinct().Count() != input.Count ||
            input.Any(item => !groups.Any(group => group.Id == item.Id && group.Version == item.Version)))
            return Conflict(ApiResponse<string>.Fail("Qrup siyahısı dəyişib. Yeniləyib təkrar sıralayın."));
        for (var i = 0; i < input.Count; i++)
        {
            var group = groups.Single(x => x.Id == input[i].Id);
            group.DisplayOrder = i + 1; group.Version = Guid.NewGuid(); group.UpdatedAt = DateTime.UtcNow;
        }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(ApiResponse<string>.Fail("Qrup siyahısı dəyişib. Yeniləyin.")); }
        await Audit("Reorder", Guid.Empty);
        return await List();
    }

    [HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(70 * 1024 * 1024)]
    public Task<IActionResult> Create([FromForm] ShowcaseGroupInput input) => Save(null, input);

    [HttpPut("{id:guid}"), Consumes("multipart/form-data"), RequestSizeLimit(70 * 1024 * 1024)]
    public Task<IActionResult> Update(Guid id, [FromForm] ShowcaseGroupInput input) => Save(id, input);

    private async Task<IActionResult> Save(Guid? id, ShowcaseGroupInput input)
    {
        var group = id.HasValue ? await Groups.FirstOrDefaultAsync(x => x.Id == id) : new ShowcaseGroup();
        if (group == null) return NotFound(ApiResponse<string>.Fail("Qrup tapılmadı."));
        if (id.HasValue && input.Version != group.Version)
            return Conflict(ApiResponse<string>.Fail("Qrup başqa pəncərədə dəyişdirilib. Yeniləyib təkrar yoxlayın."));
        if (input.Blocks.Count is < 1 or > 3)
            return BadRequest(ApiResponse<string>.Fail("Qrupda 1–3 tanıtım bloku olmalıdır."));
        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(ApiResponse<string>.Fail("Qrupun daxili adını yazın."));

        var existing = group.Blocks.ToDictionary(x => x.Id);
        var seenIds = new HashSet<Guid>();
        var slugs = new HashSet<string>();
        foreach (var block in input.Blocks)
        {
            ShowcaseBlock? old = null;
            if (block.Id.HasValue && (!existing.TryGetValue(block.Id.Value, out old) || !seenIds.Add(block.Id.Value)))
                return BadRequest(ApiResponse<string>.Fail("Blok bu qrupa aid deyil və ya təkrarlanır."));
            if ((block.File == null && string.IsNullOrEmpty(old?.ImageUrl)) ||
                (block.MobileFile == null && string.IsNullOrEmpty(old?.MobileImageUrl)))
                return BadRequest(ApiResponse<string>.Fail("Hər blok üçün kompüter və telefon şəkli tələb olunur."));
            foreach (var file in new[] { block.File, block.MobileFile }.OfType<IFormFile>())
                if (file.Length is <= 0 or > 10 * 1024 * 1024)
                    return BadRequest(ApiResponse<string>.Fail("Hər hazırlanmış şəkil 0–10 MB aralığında olmalıdır."));
            if (block.TargetType == "internal")
            {
                block.Slug = ShowcaseValidation.NormalizeSlug(block.Slug);
                if (block.Slug == null || !slugs.Add(block.Slug))
                    return BadRequest(ApiResponse<string>.Fail("Səhifə ünvanı etibarsızdır, qorunur və ya təkrarlanır. Latın hərfləri, rəqəm və tire istifadə edin."));
            }
            else block.Slug = null;
            block.ExternalUrl = block.TargetType == "external" ? block.ExternalUrl?.Trim() : null;
            if (block.TargetType == "external" && !ShowcaseValidation.IsExternalUrl(block.ExternalUrl))
                return BadRequest(ApiResponse<string>.Fail("Tam http:// və ya https:// keçidi yazın."));
            block.ProductIds = block.ProductIds.Where(x => x != Guid.Empty).Distinct().ToList();
        }
        if (await db.ShowcaseBlocks.AnyAsync(x => x.ShowcaseGroupId != group.Id && x.Slug != null && slugs.Contains(x.Slug)))
            return Conflict(ApiResponse<string>.Fail("Bu səhifə ünvanı artıq istifadə olunur."));
        var productIds = input.Blocks.SelectMany(x => x.ProductIds).Distinct().ToList();
        if (await db.Products.CountAsync(x => productIds.Contains(x.Id)) != productIds.Count)
            return BadRequest(ApiResponse<string>.Fail("Seçilən məhsullardan biri silinib. Seçimi yeniləyin."));

        var uploaded = new List<string>();
        var obsolete = new List<string>();
        try
        {
            foreach (var removed in group.Blocks.Where(x => !seenIds.Contains(x.Id)).ToList())
            {
                obsolete.AddRange(new[] { removed.ImageUrl, removed.MobileImageUrl });
                db.ShowcaseBlocks.Remove(removed);
                group.Blocks.Remove(removed);
            }
            for (var i = 0; i < input.Blocks.Count; i++)
            {
                var source = input.Blocks[i];
                var block = source.Id.HasValue ? existing[source.Id.Value] : new ShowcaseBlock();
                if (!source.Id.HasValue)
                {
                    group.Blocks.Add(block);
                    if (id.HasValue) db.ShowcaseBlocks.Add(block);
                }
                if (source.File != null)
                {
                    var url = await files.UploadImageAsync(source.File, "showcase/desktop");
                    uploaded.Add(url); obsolete.Add(block.ImageUrl); block.ImageUrl = url;
                }
                if (source.MobileFile != null)
                {
                    var url = await files.UploadImageAsync(source.MobileFile, "showcase/mobile");
                    uploaded.Add(url); obsolete.Add(block.MobileImageUrl); block.MobileImageUrl = url;
                }
                block.DisplayOrder = i;
                block.Shape = source.Shape; block.TargetType = source.TargetType;
                block.Slug = source.Slug; block.ExternalUrl = source.ExternalUrl;
                block.ImageAlt = Clean(source.ImageAlt); block.Title = Clean(source.Title);
                block.Subtitle = Clean(source.Subtitle); block.Description = Clean(source.Description);
                block.AfterProductsDescription = Clean(source.AfterProductsDescription);
                block.UpdatedAt = DateTime.UtcNow;
                foreach (var old in block.Products.Where(x => !source.ProductIds.Contains(x.ProductId)).ToList())
                {
                    db.ShowcaseBlockProducts.Remove(old); block.Products.Remove(old);
                }
                for (var p = 0; p < source.ProductIds.Count; p++)
                {
                    var link = block.Products.FirstOrDefault(x => x.ProductId == source.ProductIds[p]);
                    if (link == null) { link = new ShowcaseBlockProduct { ProductId = source.ProductIds[p] }; block.Products.Add(link); }
                    link.DisplayOrder = p;
                }
            }
            group.Name = input.Name.Trim(); group.DisplayOrder = input.DisplayOrder;
            group.IsActive = input.IsActive; group.Version = Guid.NewGuid(); group.UpdatedAt = DateTime.UtcNow;
            if (!id.HasValue) db.ShowcaseGroups.Add(group);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await Cleanup(uploaded);
            return Conflict(ApiResponse<string>.Fail("Qrup dəyişdirilib. Yeniləyib təkrar yoxlayın."));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await Cleanup(uploaded);
            return Conflict(ApiResponse<string>.Fail("Bu səhifə ünvanı artıq istifadə olunur."));
        }
        catch (InvalidOperationException ex)
        {
            await Cleanup(uploaded);
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
        catch { await Cleanup(uploaded); throw; }
        await Cleanup(obsolete.Except(group.Blocks.SelectMany(x => new[] { x.ImageUrl, x.MobileImageUrl })));
        await Audit(id.HasValue ? "Update" : "Create", group.Id);
        return Ok(ApiResponse<object>.Ok(View(group)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid version)
    {
        var group = await Groups.FirstOrDefaultAsync(x => x.Id == id);
        if (group == null) return NotFound(ApiResponse<string>.Fail("Qrup tapılmadı."));
        if (group.Version != version) return Conflict(ApiResponse<string>.Fail("Qrup dəyişdirilib. Siyahını yeniləyin."));
        var images = group.Blocks.SelectMany(x => new[] { x.ImageUrl, x.MobileImageUrl }).ToList();
        db.ShowcaseGroups.Remove(group);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(ApiResponse<string>.Fail("Qrup dəyişdirilib. Siyahını yeniləyin.")); }
        await Cleanup(images);
        await Audit("Delete", id);
        return Ok(ApiResponse<string>.Ok("Qrup silindi."));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task Cleanup(IEnumerable<string> urls)
    {
        foreach (var url in urls.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            try { await files.DeleteImageAsync(url); }
            catch (Exception ex) { logger.LogWarning(ex, "Tanıtım şəklinin təmizlənməsi alınmadı: {ImageUrl}", url); }
    }
    private async Task Audit(string action, Guid id)
    {
        try
        {
            await audit.CreateAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                action, "ShowcaseGroup", id.ToString(), "Tanıtım qrupu", HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
        }
        catch (Exception ex) { logger.LogError(ex, "Tanıtım qrupu saxlanıldı, audit yazılmadı: {Id}", id); }
    }
}
