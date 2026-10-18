using System.Security.Claims;
using System.Text.Json;
using LifeLine.Data;
using LifeLine.Models;
using LifeLine.Services;
using LifeLine.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Controllers;

[Authorize(Roles = "Hospital")]
public class HospitalController : Controller
{
    private readonly AppDbContext _db;
    private readonly RequestService _svc;
    public HospitalController(AppDbContext db, RequestService svc) { _db = db; _svc = svc; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private Task<Hospital> Me() => _db.Hospitals.FirstAsync(h => h.UserId == Uid);

    private async Task<HospitalLiveVm> BuildLive()
    {
        await _svc.ExpireOldAsync();
        var h = await Me();
        var reqs = await _db.BloodRequests.Include(b => b.Responses)
            .Where(b => b.HospitalId == h.Id).OrderByDescending(b => b.CreatedAt).ToListAsync();
        var all = reqs.SelectMany(r => r.Responses).ToList();
        var accepted = all.Where(r => r.Status == ResponseStatus.Accepted && r.RespondedAt != null).ToList();

        return new HospitalLiveVm
        {
            Hospital = h,
            Active = reqs.Count(r => r.Status == RequestStatus.Open),
            Fulfilled = reqs.Count(r => r.Status == RequestStatus.Fulfilled),
            Matched = all.Count,
            Responses = all.Count(r => r.Status != ResponseStatus.Notified),
            AvgResponseMin = accepted.Count == 0 ? 0 : Math.Round(accepted.Average(r => (r.RespondedAt.Value - r.NotifiedAt).TotalMinutes), 1),
            Recent = reqs.Take(6).ToList(),
            Feed = await FeedBuilder.BuildAsync(_db, h.Id)
        };
    }

    public async Task<IActionResult> Dashboard() => View(await BuildLive());
    public async Task<IActionResult> Live() => PartialView("_Live", await BuildLive());

    [HttpGet] public IActionResult NewRequest() => View(new NewRequestVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewRequest(NewRequestVm vm)
    {
        var h = await Me();
        await _svc.ExpireOldAsync();

        if (!h.IsVerified) ModelState.AddModelError("", "Your hospital is not verified yet. Ask the admin to verify it.");
        if (!BloodRules.Groups.Contains(vm.BloodGroup ?? "")) ModelState.AddModelError("BloodGroup", "Choose a blood group.");
        else if (await _db.BloodRequests.AnyAsync(b => b.HospitalId == h.Id && b.BloodGroup == vm.BloodGroup && b.Status == RequestStatus.Open))
            ModelState.AddModelError("BloodGroup", "You already have an open request for this blood group.");   // duplicate prevention
        if (!ModelState.IsValid) return View(vm);

        var (req, notified) = await _svc.CreateAsync(h, vm.BloodGroup, vm.UnitsNeeded, vm.Urgency, vm.PatientNote);
        TempData["msg"] = notified == 0
            ? "Request saved, but no eligible donor was found in range."
            : $"Request raised. {notified} matched donor(s) were alerted instantly.";
        return RedirectToAction("Details", new { id = req.Id });
    }

    public async Task<IActionResult> Requests()
    {
        await _svc.ExpireOldAsync();
        var h = await Me();
        var list = await _db.BloodRequests.Include(b => b.Responses)
            .Where(b => b.HospitalId == h.Id).OrderByDescending(b => b.CreatedAt).ToListAsync();
        return View(list);
    }

    private async Task<RequestLiveVm> BuildDetails(int id)
    {
        var h = await Me();
        var req = await _db.BloodRequests.Include(b => b.Hospital)
            .Include(b => b.Responses).ThenInclude(r => r.DonorProfile)
            .FirstOrDefaultAsync(b => b.Id == id && b.HospitalId == h.Id);
        if (req == null) return null;

        var markers = new List<object> { new { lat = req.Hospital.Latitude, lon = req.Hospital.Longitude, label = req.Hospital.Name, kind = "hospital" } };
        markers.AddRange(req.Responses.Select(r => new
        {
            lat = r.DonorProfile.Latitude, lon = r.DonorProfile.Longitude,
            label = $"{r.DonorProfile.FullName}, {r.DonorProfile.BloodGroup}, {r.DistanceKm} km",
            kind = r.Status == ResponseStatus.Accepted ? "accepted" : "donor"
        }));
        return new RequestLiveVm { Req = req, MapJson = JsonSerializer.Serialize(markers), Radius = (int)MatchingService.Policy(req.Urgency).Radius };
    }

    public async Task<IActionResult> Details(int id)
    {
        await _svc.ExpireOldAsync();
        var vm = await BuildDetails(id);
        return vm == null ? NotFound() : View(vm);
    }

    public async Task<IActionResult> DetailsLive(int id)
    {
        var vm = await BuildDetails(id);
        return vm == null ? NotFound() : PartialView("_DetailsLive", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDonated(int responseId, int requestId)
    {
        await _svc.MarkDonatedAsync(responseId, Uid);
        TempData["msg"] = "Donation recorded. The donor now rests for 90 days.";
        return RedirectToAction("Details", new { id = requestId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        await _svc.CancelAsync(id, Uid);
        TempData["msg"] = "Request cancelled.";
        return RedirectToAction("Details", new { id });
    }
}
