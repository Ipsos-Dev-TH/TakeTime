using TakeTime.Core.Domain.Base;
using TakeTime.CRM.Domain.Enums;

namespace TakeTime.CRM.Domain.Entities;

/// <summary>
/// Represents a guest review and rating for a stay.
/// </summary>
public class GuestReview : Entity
{
    private readonly List<ReviewCategory> _categories = [];

    /// <summary>
    /// Identifier of the customer who wrote the review.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Identifier of the associated reservation.
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Overall rating (1 to 5).
    /// </summary>
    public int Rating { get; private set; }

    /// <summary>
    /// Review text/comment.
    /// </summary>
    public string? Comment { get; private set; }

    /// <summary>
    /// Categories the guest rated.
    /// </summary>
    public IReadOnlyCollection<ReviewCategory> Categories => _categories.AsReadOnly();

    /// <summary>
    /// Staff response to the review.
    /// </summary>
    public string? Response { get; private set; }

    /// <summary>
    /// Identifier of the staff member who responded.
    /// </summary>
    public string? RespondedBy { get; private set; }

    /// <summary>
    /// Timestamp when the response was posted.
    /// </summary>
    public DateTime? RespondedAt { get; private set; }

    /// <summary>
    /// Moderation status of the review.
    /// </summary>
    public ReviewStatus Status { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private GuestReview() { }

    /// <summary>
    /// Creates a new guest review.
    /// </summary>
    public GuestReview(
        Guid customerId,
        Guid reservationId,
        int rating,
        string? comment = null,
        IEnumerable<ReviewCategory>? categories = null)
    {
        if (rating < 1 || rating > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        CustomerId = customerId;
        ReservationId = reservationId;
        Rating = rating;
        Comment = comment;
        Status = ReviewStatus.Pending;

        if (categories is not null)
        {
            _categories.AddRange(categories);
        }
    }

    /// <summary>
    /// Adds a staff response to the review.
    /// </summary>
    public void AddResponse(string response, string respondedBy)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Response is required.", nameof(response));

        if (string.IsNullOrWhiteSpace(respondedBy))
            throw new ArgumentException("Responded by user is required.", nameof(respondedBy));

        Response = response;
        RespondedBy = respondedBy;
        RespondedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Approves the review for public display.
    /// </summary>
    public void Approve()
    {
        Status = ReviewStatus.Approved;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Hides the review from public display.
    /// </summary>
    public void Hide()
    {
        Status = ReviewStatus.Hidden;
        UpdatedAt = DateTime.UtcNow;
    }
}
