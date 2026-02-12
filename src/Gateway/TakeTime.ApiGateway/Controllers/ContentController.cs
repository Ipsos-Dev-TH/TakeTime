using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TakeTime.ApiGateway.Controllers;

/// <summary>
/// Simple CMS controller for managing hotel website content.
/// Provides endpoints for page content, gallery images, and announcements.
/// Uses in-memory storage with thread-safe concurrent dictionaries.
/// In production, this should be replaced with a proper persistence layer.
/// </summary>
[ApiController]
[Route("api/v1/content")]
[Produces("application/json")]
public class ContentController : ControllerBase
{
    private readonly ILogger<ContentController> _logger;

    // In-memory storage for content pages, gallery, and announcements.
    // Thread-safe for concurrent access. In production, replace with
    // a database or file-based storage.
    private static readonly ConcurrentDictionary<string, PageContentDto> Pages = new(
        new Dictionary<string, PageContentDto>
        {
            ["about"] = new PageContentDto
            {
                Slug = "about",
                Title = "About Us",
                Content = "Welcome to TakeTime Resort. We are a boutique resort located in Bang Phra, Chonburi, Thailand. Our resort offers a peaceful retreat with modern amenities, surrounded by nature.",
                MetaDescription = "Learn about TakeTime Resort - a boutique resort in Bang Phra, Chonburi.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            ["contact"] = new PageContentDto
            {
                Slug = "contact",
                Title = "Contact Us",
                Content = "Get in touch with us. We are happy to assist you with reservations, inquiries, and special requests.",
                MetaDescription = "Contact TakeTime Resort for reservations and inquiries.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            ["facilities"] = new PageContentDto
            {
                Slug = "facilities",
                Title = "Facilities",
                Content = "Our resort features a swimming pool, restaurant, spa, fitness center, and conference room. All designed for your comfort and convenience.",
                MetaDescription = "Explore the facilities at TakeTime Resort.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            },
            ["gallery"] = new PageContentDto
            {
                Slug = "gallery",
                Title = "Photo Gallery",
                Content = "Browse our photo gallery to see our rooms, facilities, and the beautiful surroundings of our resort.",
                MetaDescription = "View photos of TakeTime Resort.",
                IsPublished = true,
                UpdatedAt = DateTime.UtcNow
            }
        });

    private static readonly ConcurrentBag<GalleryImageDto> GalleryImages = new(
    [
        new GalleryImageDto { Id = Guid.NewGuid(), Url = "/images/gallery/resort-exterior.jpg", Title = "Resort Exterior", Caption = "Welcome to TakeTime Resort", Category = "General", SortOrder = 1, CreatedAt = DateTime.UtcNow },
        new GalleryImageDto { Id = Guid.NewGuid(), Url = "/images/gallery/pool.jpg", Title = "Swimming Pool", Caption = "Outdoor pool with garden view", Category = "Facilities", SortOrder = 2, CreatedAt = DateTime.UtcNow },
        new GalleryImageDto { Id = Guid.NewGuid(), Url = "/images/gallery/deluxe-room.jpg", Title = "Deluxe Room", Caption = "Spacious deluxe room with modern amenities", Category = "Rooms", SortOrder = 3, CreatedAt = DateTime.UtcNow },
        new GalleryImageDto { Id = Guid.NewGuid(), Url = "/images/gallery/restaurant.jpg", Title = "Restaurant", Caption = "Thai and international cuisine", Category = "Dining", SortOrder = 4, CreatedAt = DateTime.UtcNow }
    ]);

    private static readonly ConcurrentBag<AnnouncementDto> Announcements = new(
    [
        new AnnouncementDto
        {
            Id = Guid.NewGuid(),
            Title = "Welcome to TakeTime Resort",
            Content = "We are excited to welcome you. Check out our special opening rates!",
            Type = "Info",
            IsActive = true,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(60),
            CreatedAt = DateTime.UtcNow
        }
    ]);

    public ContentController(ILogger<ContentController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ─── Pages ──────────────────────────────────────────────────────

    /// <summary>
    /// Get page content by slug (e.g., about, gallery, contact, facilities).
    /// Public endpoint for website content.
    /// </summary>
    /// <param name="slug">The page slug identifier.</param>
    /// <returns>The page content.</returns>
    [HttpGet("pages/{slug}")]
    [ProducesResponseType(typeof(PageContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetPage(string slug)
    {
        _logger.LogDebug("Fetching content page: {Slug}", slug);

        var normalizedSlug = slug.ToLowerInvariant().Trim();

        if (!Pages.TryGetValue(normalizedSlug, out var page))
        {
            return NotFound(new { message = $"Page '{slug}' not found." });
        }

        if (!page.IsPublished)
        {
            return NotFound(new { message = $"Page '{slug}' is not published." });
        }

        return Ok(page);
    }

    /// <summary>
    /// Update page content by slug. Requires authentication.
    /// Creates the page if it does not exist.
    /// </summary>
    /// <param name="slug">The page slug identifier.</param>
    /// <param name="dto">Updated page content.</param>
    /// <returns>The updated page content.</returns>
    [HttpPut("pages/{slug}")]
    [Authorize]
    [ProducesResponseType(typeof(PageContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult UpdatePage(string slug, [FromBody] UpdatePageContentDto dto)
    {
        var normalizedSlug = slug.ToLowerInvariant().Trim();

        _logger.LogInformation("Updating content page: {Slug}", normalizedSlug);

        var page = new PageContentDto
        {
            Slug = normalizedSlug,
            Title = dto.Title ?? normalizedSlug,
            Content = dto.Content ?? string.Empty,
            MetaDescription = dto.MetaDescription,
            IsPublished = dto.IsPublished ?? true,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = User.Identity?.Name
        };

        Pages.AddOrUpdate(normalizedSlug, page, (_, _) => page);

        _logger.LogInformation("Content page '{Slug}' updated by {User}", normalizedSlug, page.UpdatedBy);

        return Ok(page);
    }

    // ─── Gallery ────────────────────────────────────────────────────

    /// <summary>
    /// Get gallery images. Public endpoint for the website gallery.
    /// </summary>
    /// <param name="category">Optional filter by image category.</param>
    /// <returns>A list of gallery images.</returns>
    [HttpGet("gallery")]
    [ProducesResponseType(typeof(List<GalleryImageDto>), StatusCodes.Status200OK)]
    public IActionResult GetGallery([FromQuery] string? category)
    {
        _logger.LogDebug("Fetching gallery images, category: {Category}", category);

        var images = GalleryImages.AsEnumerable();

        if (!string.IsNullOrEmpty(category))
        {
            images = images.Where(i =>
                i.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true);
        }

        return Ok(images.OrderBy(i => i.SortOrder).ToList());
    }

    /// <summary>
    /// Add a gallery image. Requires authentication.
    /// Accepts image metadata (URL-based); actual file upload should use
    /// a separate file storage service.
    /// </summary>
    /// <param name="dto">Gallery image metadata.</param>
    /// <returns>The created gallery image entry.</returns>
    [HttpPost("gallery")]
    [Authorize]
    [ProducesResponseType(typeof(GalleryImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult AddGalleryImage([FromBody] CreateGalleryImageDto dto)
    {
        _logger.LogInformation("Adding gallery image: {Title}", dto.Title);

        var image = new GalleryImageDto
        {
            Id = Guid.NewGuid(),
            Url = dto.Url,
            Title = dto.Title,
            Caption = dto.Caption,
            Category = dto.Category,
            SortOrder = dto.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        GalleryImages.Add(image);

        _logger.LogInformation("Gallery image '{Title}' added with ID {ImageId}", dto.Title, image.Id);

        return CreatedAtAction(nameof(GetGallery), null, image);
    }

    // ─── Announcements ──────────────────────────────────────────────

    /// <summary>
    /// Get active announcements. Returns only currently active announcements
    /// within their start and end date range.
    /// </summary>
    /// <returns>A list of active announcements.</returns>
    [HttpGet("announcements")]
    [ProducesResponseType(typeof(List<AnnouncementDto>), StatusCodes.Status200OK)]
    public IActionResult GetAnnouncements()
    {
        _logger.LogDebug("Fetching active announcements");

        var now = DateTime.UtcNow;

        var activeAnnouncements = Announcements
            .Where(a => a.IsActive
                && (!a.StartDate.HasValue || a.StartDate.Value <= now)
                && (!a.EndDate.HasValue || a.EndDate.Value >= now))
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        return Ok(activeAnnouncements);
    }

    /// <summary>
    /// Create a new announcement. Requires authentication.
    /// </summary>
    /// <param name="dto">Announcement details.</param>
    /// <returns>The created announcement.</returns>
    [HttpPost("announcements")]
    [Authorize]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult CreateAnnouncement([FromBody] CreateAnnouncementDto dto)
    {
        _logger.LogInformation("Creating announcement: {Title}", dto.Title);

        var announcement = new AnnouncementDto
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Content = dto.Content,
            Type = dto.Type ?? "Info",
            IsActive = dto.IsActive ?? true,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name
        };

        Announcements.Add(announcement);

        _logger.LogInformation("Announcement '{Title}' created with ID {AnnouncementId}",
            dto.Title, announcement.Id);

        return CreatedAtAction(nameof(GetAnnouncements), null, announcement);
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────

/// <summary>
/// Page content DTO for the CMS.
/// </summary>
public sealed class PageContentDto
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? MetaDescription { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// DTO for updating page content.
/// </summary>
public sealed class UpdatePageContentDto
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? MetaDescription { get; set; }
    public bool? IsPublished { get; set; }
}

/// <summary>
/// Gallery image DTO.
/// </summary>
public sealed class GalleryImageDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public string? Category { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a gallery image entry.
/// </summary>
public sealed class CreateGalleryImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public string? Category { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// Announcement DTO.
/// </summary>
public sealed class AnnouncementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
    public bool IsActive { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for creating an announcement.
/// </summary>
public sealed class CreateAnnouncementDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Type { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
