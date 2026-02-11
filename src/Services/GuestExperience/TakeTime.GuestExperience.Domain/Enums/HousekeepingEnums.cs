namespace TakeTime.GuestExperience.Domain.Enums;

public enum HousekeepingTaskType { Cleaning, TurnDown, DeepClean, Inspection, LaundryChange }
public enum HousekeepingStatus { Pending, InProgress, Completed, Verified, Skipped }
public enum RoomServiceStatus { Ordered, Preparing, Ready, Delivering, Delivered, Cancelled }
public enum MaintenanceIssueType { Plumbing, Electrical, HVAC, Furniture, Appliance, Other }
public enum MaintenancePriority { Low, Normal, High, Critical }
public enum MaintenanceStatus { Reported, Assigned, InProgress, Completed, Closed }
