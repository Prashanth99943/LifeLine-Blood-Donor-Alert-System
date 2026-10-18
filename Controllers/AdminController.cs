using System.Text.Json;
using LifeLine.Data;
using LifeLine.Hubs;
using LifeLine.Models;
using LifeLine.Services;
using LifeLine.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly RequestService _svc;
    private readonly IHubContext<AlertHub> _hub;
    public AdminController(AppDbContext db, RequestService svc, IHubContext<AlertHub> hub) { _db = db; _svc = svc; _hub = hub; }

    private async Task<AdminLiveVm> BuildLive()
    {
        await _svc.ExpireOldAsync();
        var donors = await _db.DonorProfiles.ToListAsync();
        var hospitals = await _db.Hospitals.ToListAsync();
        var reqs = await _db.BloodRequests.Include(b => b.Responses).ToListAsync();
        var answered = reqs.SelectMany(r => r.Responses).Where(r => r.Status != ResponseStatus.Notified).ToList();

        return new AdminLiveVm
        {
            Donors = donors.Count,
            EligibleNow = donors.Count(d => d.IsAvailable && BloodRules.IsEligible(d.LastDonationDate)),
            HospitalsVerified = hospitals.Count(h => h.IsVerified),
            HospitalsPending = hospitals.Count(h => !h.IsVerified),
            Active = reqs.Count(r => r.Status == RequestStatus.Open),
            Fulfilled = reqs.Count(r => r.Status == RequestStatus.Fulfilled),
            Total = reqs.Count,
            AcceptRate = answered.Count == 0 ? 0 : Math.Round(100.0 * answered.Count(r => r.Status == ResponseStatus.Accepted) / answered.Count),
            Feed = await FeedBuilder.BuildAsync(_db, null)
        };
    }

    public async Task<IActionResult> Dashboard()
    {
        var vm = await BuildLive();
        var reqs = await _db.BloodRequests.ToListAsync();

        var byGroup = BloodRules.Groups.Select(g => reqs.Count(r => r.BloodGroup == g)).ToArray();
        ViewBag.GroupJson = JsonSerializer.Serialize(new { labels = BloodRules.Groups, data = byGroup });

        var statuses = Enum.GetValues<RequestStatus>();
        ViewBag.StatusJson = JsonSerializer.Serialize(new
        {
            labels = statuses.Select(s => s.ToString()).ToArray(),
            data = statuses.Select(s => reqs.Count(r => r.Status == s)).ToArray()
        });

        var hs = await _db.Hospitals.ToListAsync();
        var ds = await _db.DonorProfiles.ToListAsync();
        var markers = hs.Select(h => (object)new { lat = h.Latitude, lon = h.Longitude, label = h.Name, kind = "hospital" })
            .Concat(ds.Select(d => (object)new { lat = d.Latitude, lon = d.Longitude, label = $"{d.FullName} ({d.BloodGroup})", kind = "donor" }))
            .ToList();
        ViewBag.MapJson = JsonSerializer.Serialize(markers);

        return View(vm);
    }

    public async Task<IActionResult> Live() => PartialView("_Live", await BuildLive());

    public async Task<IActionResult> Hospitals() => View(await _db.Hospitals.OrderBy(h => h.IsVerified).ThenBy(h => h.Name).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyHospital(int id)
    {
        var h = await _db.Hospitals.FindAsync(id);
        if (h != null && !h.IsVerified)
        {
            h.IsVerified = true;
            await _db.SaveChangesAsync();
            await _hub.Clients.User(h.UserId).SendAsync("RequestUpdated",
                new { title = "Hospital verified", body = "You can now raise emergency requests." });
            TempData["msg"] = $"{h.Name} is now verified.";
        }
        return RedirectToAction("Hospitals");
    }

    public async Task<IActionResult> Donors() => View(await _db.DonorProfiles.OrderBy(d => d.City).ThenBy(d => d.FullName).ToListAsync());

    public async Task<IActionResult> Requests()
    {
        await _svc.ExpireOldAsync();
        var list = await _db.BloodRequests.Include(b => b.Hospital).Include(b => b.Responses)
            .OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(list);
    }
}
