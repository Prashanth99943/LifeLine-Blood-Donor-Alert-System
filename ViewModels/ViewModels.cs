using System.ComponentModel.DataAnnotations;
using LifeLine.Models;

namespace LifeLine.ViewModels;

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; }
    [Required, DataType(DataType.Password)] public string Password { get; set; }
}

public class DonorRegisterVm
{
    [Required, Display(Name = "Full name")] public string FullName { get; set; }
    [Required, EmailAddress] public string Email { get; set; }
    [Required, MinLength(6), DataType(DataType.Password)] public string Password { get; set; }
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a 10-digit phone number")] public string Phone { get; set; }
    [Required, Display(Name = "Blood group")] public string BloodGroup { get; set; }
    [Required] public string City { get; set; }
}

public class HospitalRegisterVm
{
    [Required, Display(Name = "Hospital name")] public string Name { get; set; }
    [Required, EmailAddress] public string Email { get; set; }
    [Required, MinLength(6), DataType(DataType.Password)] public string Password { get; set; }
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a 10-digit phone number")] public string Phone { get; set; }
    [Required] public string City { get; set; }
}

public class NewRequestVm
{
    [Required, Display(Name = "Blood group needed")] public string BloodGroup { get; set; }
    [Range(1, 20), Display(Name = "Units needed")] public int UnitsNeeded { get; set; } = 1;
    public Urgency Urgency { get; set; }
    [StringLength(200), Display(Name = "Note for donors (optional)")] public string PatientNote { get; set; }
}

public class ProfileVm
{
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a 10-digit phone number")] public string Phone { get; set; }
    [Required] public string City { get; set; }
    public bool IsAvailable { get; set; }
}

// ---- Models for the live (auto-refreshing) parts of each dashboard ----
public class DonorLiveVm
{
    public DonorProfile Profile { get; set; }
    public List<DonorResponse> Pending { get; set; }
    public List<DonorResponse> Accepted { get; set; }
    public int Donations { get; set; }
    public bool Eligible { get; set; }
    public DateTime? NextEligible { get; set; }
}

public class HospitalLiveVm
{
    public Hospital Hospital { get; set; }
    public int Active { get; set; }
    public int Fulfilled { get; set; }
    public int Matched { get; set; }
    public int Responses { get; set; }
    public double AvgResponseMin { get; set; }
    public List<BloodRequest> Recent { get; set; }
    public List<(DateTime Time, string Text)> Feed { get; set; }
}

public class RequestLiveVm
{
    public BloodRequest Req { get; set; }
    public string MapJson { get; set; }
    public int Radius { get; set; }
}

public class AdminLiveVm
{
    public int Donors { get; set; }
    public int EligibleNow { get; set; }
    public int HospitalsVerified { get; set; }
    public int HospitalsPending { get; set; }
    public int Active { get; set; }
    public int Fulfilled { get; set; }
    public int Total { get; set; }
    public double AcceptRate { get; set; }
    public List<(DateTime Time, string Text)> Feed { get; set; }
}
