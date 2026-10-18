using LifeLine.Data;
using LifeLine.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Services;

public record MatchResult(DonorProfile Donor, double Score, double DistanceKm);

/// <summary>
/// The heart of the project. Three steps:
///   1. FILTER  - drop donors who are unavailable, incompatible, resting (&lt; 90 days) or too far.
///   2. SCORE   - exact blood group 50 (compatible 30) + proximity up to 40 + "well rested" 10.
///   3. RANK    - highest score first; only the top N donors are alerted.
/// </summary>
public class MatchingService
{
    private readonly AppDbContext _db;
    public MatchingService(AppDbContext db) => _db = db;

    /// <summary>The more urgent the request, the wider we search and the more donors we alert.</summary>
    public static (double Radius, int Top, int ExpiryHours) Policy(Urgency u) => u switch
    {
        Urgency.Critical => (300, 20, 6),
        Urgency.Urgent   => (100, 10, 24),
        _                => (30, 5, 72),
    };

    public async Task<List<MatchResult>> RankDonorsAsync(BloodRequest req, Hospital hospital)
    {
        var (radius, top, _) = Policy(req.Urgency);
        var donors = await _db.DonorProfiles.Where(d => d.IsAvailable).ToListAsync();
        var results = new List<MatchResult>();

        foreach (var d in donors)
        {
            if (!BloodRules.IsCompatible(d.BloodGroup, req.BloodGroup)) continue;   // rule 1: blood group
            if (!BloodRules.IsEligible(d.LastDonationDate)) continue;               // rule 2: 90-day gap

            double km = BloodRules.DistanceKm(hospital.Latitude, hospital.Longitude, d.Latitude, d.Longitude);
            if (km > radius) continue;                                              // rule 3: distance

            double groupPoints = d.BloodGroup == req.BloodGroup ? 50 : 30;
            double distancePoints = 40 * (1 - km / radius);
            bool rested = d.LastDonationDate == null || (DateTime.UtcNow - d.LastDonationDate.Value).TotalDays > 180;
            double score = groupPoints + distancePoints + (rested ? 10 : 0);

            results.Add(new MatchResult(d, Math.Round(score, 1), Math.Round(km, 1)));
        }

        return results.OrderByDescending(r => r.Score).Take(top).ToList();
    }
}
