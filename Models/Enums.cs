namespace Staybnb.Models;

public enum PropertyType { Apartment, House, Cabin, Villa, Cottage, Guesthouse }

// Pending -> Approved -> CheckedIn -> Completed (rubric flow). Payment state lives on Payment.Status.
public enum BookingStatus { Pending, Approved, Rejected, Cancelled, CheckedIn, Completed }

public enum PaymentStatus { Pending, Paid, Failed, Refunded }

public enum ApplicationStatus { Pending, Approved, Rejected }

public enum CheckInStatus { NotStarted, InProgress, Submitted, Verified, Rejected }

public enum DocumentStatus { Pending, Verified, Rejected }

public enum ActivityType { Authentication, RoleChange, HostApplication, Property, Booking, CheckIn, Payment, System }

public enum NotificationType { Info, Success, Warning, Booking, Message, HostApplication, CheckIn }
