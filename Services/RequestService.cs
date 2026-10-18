using LifeLine.Data;
using LifeLine.Hubs;
using LifeLine.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Services;

/// <summary>Everything that changes a request: create, answer, confirm donation, cancel, expire. Each change is pushed live over SignalR.</summary>
public class RequestService
{
    private readonly AppDbContext _db;
    private readonly MatchingService _match;
    private readonly IHubContext<AlertHub> _hub;

    public RequestService(AppDbContext db, MatchingService match, IHubContext<AlertHub> hub)
    {
        _db = db; _match = match; _hub = hub;
    }

    // 1. Hospital raises a request -> rank donors -> save -> push an alert to each chosen donor
    public async Task<(BloodRequest Request, int Notified)> CreateAsync(Hospital h, string group, int units, Urgency urgency, string note)
    {
        var policy = MatchingService.Policy(urgency);
        var req = new BloodRequest
        {
            HospitalId = h.Id, BloodGroup = group, UnitsNeeded = units, Urgency = urgency, PatientNote = note,
            Status = RequestStatus.Open, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(policy.ExpiryHours)
        };

        var matches = await _match.RankDonorsAsync(req, h);
        foreach (var m in matches)
        {
            req.Responses.Add(new DonorResponse
            {
                DonorProfileId = m.Donor.Id, Score = m.Score, DistanceKm = m.DistanceKm,
                Status = ResponseStatus.Notified, NotifiedAt = DateTime.UtcNow
            });
        }
        _db.BloodRequests.Add(req);
        await _db.SaveChangesAsync();

        foreach (var m in matches)
            await _hub.Clients.User(m.Donor.UserId).SendAsync("NewAlert",
                new { title = $"{urgency} request: {group} blood needed", body = $"{h.Name}, {m.DistanceKm} km from you" });

        await _hub.Clients.Group("Admins").SendAsync("RequestUpdated",
            new { title = "New emergency request", body = $"{h.Name} needs {units} unit(s) of {group}" });

        return (req, matches.Count);
    }

    // 2. Donor accepts or declines -> update counts -> tell the hospital and admins instantly
    public async Task RespondAsync(int responseId, string donorUserId, bool accept)
    {
        var r = await _db.DonorResponses
            .Include(x => x.DonorProfile)
            .Include(x => x.BloodRequest).ThenInclude(b => b.Hospital)
            .Include(x => x.BloodRequest).ThenInclude(b => b.Responses)
            .FirstOrDefaultAsync(x => x.Id == responseId && x.DonorProfile.UserId == donorUserId);

        if (r == null || r.Status != ResponseStatus.Notified || r.BloodRequest.Status != RequestStatus.Open) return;

        r.Status = accept ? ResponseStatus.Accepted : ResponseStatus.Declined;
        r.RespondedAt = DateTime.UtcNow;

        var req = r.BloodRequest;
        int accepted = req.AcceptedCount;                       // r is the same tracked object, so it is already counted
        if (accepted >= req.UnitsNeeded) req.Status = RequestStatus.Fulfilled;
        await _db.SaveChangesAsync();

        var msg = new
        {
            title = accept ? "Donor accepted" : "Donor declined",
            body = $"{r.DonorProfile.FullName} ({r.DonorProfile.BloodGroup}) {(accept ? "accepted" : "declined")}: {accepted} of {req.UnitsNeeded} units covered"
        };
        await _hub.Clients.User(req.Hospital.UserId).SendAsync("RequestUpdated", msg);
        await _hub.Clients.Group("Admins").SendAsync("RequestUpdated", msg);

        if (req.Status == RequestStatus.Fulfilled)
            await NotifyPendingAsync(req, "Request fulfilled", $"{req.Hospital.Name} now has enough donors. Thank you for standing by.");
    }

    // 3. Hospital confirms the donation happened -> donor's 90-day rest period starts
    public async Task MarkDonatedAsync(int responseId, string hospitalUserId)
    {
        var r = await _db.DonorResponses
            .Include(x => x.DonorProfile)
            .Include(x => x.BloodRequest).ThenInclude(b => b.Hospital)
            .FirstOrDefaultAsync(x => x.Id == responseId);

        if (r == null || r.BloodRequest.Hospital.UserId != hospitalUserId) return;
        if (r.Status != ResponseStatus.Accepted || r.Donated) return;

        r.Donated = true;
        r.DonorProfile.LastDonationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hub.Clients.User(r.DonorProfile.UserId).SendAsync("RequestUpdated",
            new { title = "Donation recorded", body = $"Thank you. You can donate again after {DateTime.UtcNow.AddDays(BloodRules.GapDays):dd MMM yyyy}." });
        await _hub.Clients.Group("Admins").SendAsync("RequestUpdated",
            new { title = "Donation completed", body = $"{r.DonorProfile.FullName} donated at {r.BloodRequest.Hospital.Name}" });
    }

    public async Task CancelAsync(int requestId, string hospitalUserId)
    {
        var req = await _db.BloodRequests.Include(b => b.Hospital)
            .FirstOrDefaultAsync(b => b.Id == requestId && b.Hospital.UserId == hospitalUserId);
        if (req == null || req.Status != RequestStatus.Open) return;

        req.Status = RequestStatus.Cancelled;
        await _db.SaveChangesAsync();
        await NotifyPendingAsync(req, "Request cancelled", $"{req.Hospital.Name} cancelled its {req.UnitsNeeded} x {req.BloodGroup} request.");
    }

    // Marks open requests whose time is up as Expired (called whenever a dashboard loads)
    public async Task ExpireOldAsync()
    {
        var now = DateTime.UtcNow;
        var old = await _db.BloodRequests.Where(b => b.Status == RequestStatus.Open && b.ExpiresAt < now).ToListAsync();
        foreach (var b in old) b.Status = RequestStatus.Expired;
        if (old.Count > 0) await _db.SaveChangesAsync();
    }

    private async Task NotifyPendingAsync(BloodRequest req, string title, string body)
    {
        var userIds = await _db.DonorResponses
            .Where(x => x.BloodRequestId == req.Id && x.Status == ResponseStatus.Notified)
            .Select(x => x.DonorProfile.UserId).ToListAsync();
        foreach (var uid in userIds)
            await _hub.Clients.User(uid).SendAsync("RequestUpdated", new { title, body });
    }
}
