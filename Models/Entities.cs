namespace LifeLine.Models;

public enum Urgency { Normal = 0, Urgent = 1, Critical = 2 }
public enum RequestStatus { Open = 0, Fulfilled = 1, Expired = 2, Cancelled = 3 }
public enum ResponseStatus { Notified = 0, Accepted = 1, Declined = 2 }

/// <summary>A registered blood donor (one row per donor login).</summary>
public class DonorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; }            // link to the login account
    public string FullName { get; set; }
    public string Phone { get; set; }
    public string BloodGroup { get; set; }        // O+, O-, A+ ...
    public string City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? LastDonationDate { get; set; }
    public bool IsAvailable { get; set; } = true;
}

/// <summary>A hospital / blood bank. Must be verified by the admin before it can raise requests.</summary>
public class Hospital
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public string City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsVerified { get; set; }
}

/// <summary>An emergency request raised by a hospital.</summary>
public class BloodRequest
{
    public int Id { get; set; }
    public int HospitalId { get; set; }
    public Hospital Hospital { get; set; }
    public string BloodGroup { get; set; }
    public int UnitsNeeded { get; set; }
    public Urgency Urgency { get; set; }
    public RequestStatus Status { get; set; }
    public string PatientNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public List<DonorResponse> Responses { get; set; } = new();

    public int AcceptedCount => Responses.Count(r => r.Status == ResponseStatus.Accepted);
}

/// <summary>One row per (request, donor that was alerted). Stores the match score and the donor's answer.</summary>
public class DonorResponse
{
    public int Id { get; set; }
    public int BloodRequestId { get; set; }
    public BloodRequest BloodRequest { get; set; }
    public int DonorProfileId { get; set; }
    public DonorProfile DonorProfile { get; set; }
    public ResponseStatus Status { get; set; }
    public double Score { get; set; }
    public double DistanceKm { get; set; }
    public DateTime NotifiedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool Donated { get; set; }             // hospital confirms the donation really happened
}
