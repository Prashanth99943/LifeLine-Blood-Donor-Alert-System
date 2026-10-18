using System.Security.Claims;
using LifeLine.Data;
using LifeLine.Models;
using LifeLine.Services;
using LifeLine.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Controllers;

[Authorize(Roles = "Donor")]
public class DonorController : Controller
{
    private readonly AppDbContext _db;
    private readonly RequestService _svc;
    public DonorController(AppDbContext db, RequestService svc) { _db = db; _svc = svc; }

    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<DonorLiveVm> BuildLive()
    {
        await _svc.ExpireOldAsync();
        var p = await _db.DonorProfiles.FirstAsync(d => d.UserId == Uid);
        var rs = await _db.DonorResponses
            .Include(r => r.BloodRequest).ThenInclude(b => b.Hospital)
            .Where(r => r.DonorProfileId == p.Id)
            .OrderByDescending(r => r.NotifiedAt).ToListAsync();

        return new DonorLiveVm
        {
            Profile = p,
            Pending = rs.Where(r => r.Status == ResponseStatus.Notified && r.BloodRequest.Status == RequestStatus.Open).ToList(),
            Accepted = rs.Where(r => r.Status == ResponseStatus.Accepted && !r.Donated && r.BloodRequest.Status != RequestStatus.Cancelled).ToList(),
            Donations = rs.Count(r => r.Donated),
            Eligible = BloodRules.IsEligible(p.LastDonationDate),
            NextEligible = p.LastDonationDate?.AddDays(BloodRules.GapDays)
        };
    }

    public async Task<IActionResult> Dashboard() => View(await BuildLive());

    // Called by the browser when SignalR says "something changed": returns only the live HTML fragment
    public async Task<IActionResult> Live() => PartialView("_Live", await BuildLive());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int id, bool accept)
    {
        await _svc.RespondAsync(id, Uid, accept);
        TempData["msg"] = accept ? "Thank you. The hospital has been told you are coming." : "Response recorded.";
        return RedirectToAction("Dashboard");
    }

    public async Task<IActionResult> History()
    {
        var p = await _db.DonorProfiles.FirstAsync(d => d.UserId == Uid);
        var rs = await _db.DonorResponses
            .Include(r => r.BloodRequest).ThenInclude(b => b.Hospital)
            .Where(r => r.DonorProfileId == p.Id)
            .OrderByDescending(r => r.NotifiedAt).ToListAsync();
        return View(rs);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var p = await _db.DonorProfiles.FirstAsync(d => d.UserId == Uid);
        ViewBag.Donor = p;
        return View(new ProfileVm { Phone = p.Phone, City = p.City, IsAvailable = p.IsAvailable });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileVm vm)
    {
        var p = await _db.DonorProfiles.FirstAsync(d => d.UserId == Uid);
        if (!CityData.Cities.ContainsKey(vm.City ?? "")) ModelState.AddModelError("City", "Choose a city from the list.");
        if (!ModelState.IsValid) { ViewBag.Donor = p; return View(vm); }

        var c = CityData.Cities[vm.City];
        p.Phone = vm.Phone; p.City = vm.City; p.Latitude = c.Lat; p.Longitude = c.Lon; p.IsAvailable = vm.IsAvailable;
        await _db.SaveChangesAsync();
        TempData["msg"] = "Profile saved.";
        return RedirectToAction("Profile");
    }
}
