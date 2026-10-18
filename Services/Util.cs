using LifeLine.Data;
using LifeLine.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Services;

public static class Fmt
{
    /// <summary>"3 min ago" style text from a UTC time.</summary>
    public static string Ago(DateTime utc)
    {
        var s = DateTime.UtcNow - utc;
        if (s.TotalSeconds < 60) return "just now";
        if (s.TotalMinutes < 60) return $"{(int)s.TotalMinutes} min ago";
        if (s.TotalHours < 24) return $"{(int)s.TotalHours} h ago";
        return $"{(int)s.TotalDays} d ago";
    }

    /// <summary>Lower-case enum name, used for CSS classes like t-critical.</summary>
    public static string Cls(object value) => value.ToString().ToLower();
}

/// <summary>Builds the "live activity" list shown on the hospital and admin dashboards.</summary>
public static class FeedBuilder
{
    public static async Task<List<(DateTime Time, string Text)>> BuildAsync(AppDbContext db, int? hospitalId)
    {
        var reqQ = db.BloodRequests.Include(b => b.Hospital).AsQueryable();
        var resQ = db.DonorResponses
            .Include(r => r.DonorProfile)
            .Include(r => r.BloodRequest).ThenInclude(b => b.Hospital)
            .Where(r => r.RespondedAt != null).AsQueryable();

        if (hospitalId != null)
        {
            reqQ = reqQ.Where(b => b.HospitalId == hospitalId);
            resQ = resQ.Where(r => r.BloodRequest.HospitalId == hospitalId);
        }

        var reqs = await reqQ.OrderByDescending(b => b.CreatedAt).Take(6).ToListAsync();
        var ress = await resQ.OrderByDescending(r => r.RespondedAt).Take(6).ToListAsync();

        return reqs.Select(b => (Time: b.CreatedAt, Text: $"{b.Hospital.Name} requested {b.UnitsNeeded} x {b.BloodGroup} ({b.Urgency})"))
            .Concat(ress.Select(r => (Time: r.RespondedAt.Value,
                Text: $"{r.DonorProfile.FullName} ({r.DonorProfile.BloodGroup}) {(r.Status == ResponseStatus.Accepted ? "accepted" : "declined")} the request from {r.BloodRequest.Hospital.Name}")))
            .OrderByDescending(x => x.Time).Take(8).ToList();
    }
}
