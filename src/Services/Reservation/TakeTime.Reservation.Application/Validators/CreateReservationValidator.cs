using FluentValidation;
using TakeTime.Reservation.Application.Commands;
using TakeTime.Reservation.Application.DTOs;

namespace TakeTime.Reservation.Application.Validators;

/// <summary>
/// FluentValidation validator for <see cref="CreateReservationCommand"/>.
/// Ensures all required fields are present and business rules are satisfied
/// before the reservation is created.
/// </summary>
public sealed class CreateReservationValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(200).WithMessage("Customer name must not exceed 200 characters.");

        RuleFor(x => x.CustomerEmail)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.CustomerEmail))
            .WithMessage("A valid email address is required.");

        RuleFor(x => x.CustomerPhone)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .Matches(@"^[\+]?[\d\s\-\(\)]+$").When(x => !string.IsNullOrEmpty(x.CustomerPhone))
            .WithMessage("Phone number format is invalid.");

        RuleFor(x => x.CheckInDate)
            .NotEmpty().WithMessage("Check-in date is required.")
            .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage("Check-in date cannot be in the past.");

        RuleFor(x => x.CheckOutDate)
            .NotEmpty().WithMessage("Check-out date is required.")
            .GreaterThan(x => x.CheckInDate)
            .WithMessage("Check-out date must be after check-in date.");

        RuleFor(x => x)
            .Must(x => (x.CheckOutDate - x.CheckInDate).Days <= 365)
            .WithMessage("Reservation duration cannot exceed 365 days.");

        RuleFor(x => x.NumberOfGuests)
            .GreaterThan(0).WithMessage("Number of guests must be at least 1.")
            .LessThanOrEqualTo(100).WithMessage("Number of guests cannot exceed 100.");

        RuleFor(x => x.NumberOfAdults)
            .GreaterThanOrEqualTo(0).WithMessage("Number of adults cannot be negative.");

        RuleFor(x => x.NumberOfChildren)
            .GreaterThanOrEqualTo(0).WithMessage("Number of children cannot be negative.");

        RuleFor(x => x)
            .Must(x => x.NumberOfAdults + x.NumberOfChildren <= x.NumberOfGuests)
            .When(x => x.NumberOfAdults > 0 || x.NumberOfChildren > 0)
            .WithMessage("Sum of adults and children cannot exceed total number of guests.");

        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Reservation source is required.")
            .MaximumLength(50).WithMessage("Source must not exceed 50 characters.");

        RuleFor(x => x.SpecialRequests)
            .MaximumLength(2000).WithMessage("Special requests must not exceed 2000 characters.");

        RuleFor(x => x.InternalNotes)
            .MaximumLength(2000).WithMessage("Internal notes must not exceed 2000 characters.");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).When(x => x.DiscountAmount.HasValue)
            .WithMessage("Discount amount cannot be negative.");

        RuleFor(x => x.DiscountReason)
            .NotEmpty().When(x => x.DiscountAmount.HasValue && x.DiscountAmount > 0)
            .WithMessage("Discount reason is required when a discount is applied.")
            .MaximumLength(500).WithMessage("Discount reason must not exceed 500 characters.");

        RuleFor(x => x.Accommodations)
            .NotEmpty().WithMessage("At least one accommodation is required.");

        RuleForEach(x => x.Accommodations)
            .SetValidator(new CreateReservationAccommodationValidator());

        RuleForEach(x => x.Items)
            .SetValidator(new CreateReservationItemValidator());
    }
}

/// <summary>
/// Validator for accommodation assignments within a reservation creation request.
/// </summary>
public sealed class CreateReservationAccommodationValidator : AbstractValidator<CreateReservationAccommodationDto>
{
    public CreateReservationAccommodationValidator()
    {
        RuleFor(x => x.AccommodationId)
            .NotEmpty().WithMessage("Accommodation ID is required.");

        RuleFor(x => x.Occupancy)
            .GreaterThan(0).When(x => x.Occupancy > 0)
            .WithMessage("Occupancy must be at least 1 if specified.");

        RuleFor(x => x.RateOverride)
            .GreaterThanOrEqualTo(0).When(x => x.RateOverride.HasValue)
            .WithMessage("Rate override cannot be negative.");

        RuleFor(x => x.CheckOutDate)
            .GreaterThan(x => x.CheckInDate)
            .When(x => x.CheckInDate.HasValue && x.CheckOutDate.HasValue)
            .WithMessage("Accommodation check-out date must be after check-in date.");
    }
}

/// <summary>
/// Validator for items within a reservation creation request.
/// </summary>
public sealed class CreateReservationItemValidator : AbstractValidator<CreateReservationItemDto>
{
    public CreateReservationItemValidator()
    {
        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("Item name is required.")
            .MaximumLength(200).WithMessage("Item name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.ItemType)
            .NotEmpty().WithMessage("Item type is required.")
            .MaximumLength(50).WithMessage("Item type must not exceed 50 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be at least 1.");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.");
    }
}
