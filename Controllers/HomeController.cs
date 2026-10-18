using LifeLine.Data;
using LifeLine.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    // Logged-in users go straight to their own dashboard; visitors see the landing page.
    public async Task<IActionResult> Index()
    {
        if (User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole("Hospital")) return RedirectToAction("Dashboard", "Hospital");
            return RedirectToAction("Dashboard", "Donor");
        }
        ViewBag.Donors = await _db.DonorProfiles.CountAsync();
        ViewBag.Hospitals = await _db.Hospitals.CountAsync(h => h.IsVerified);
        ViewBag.Fulfilled = await _db.BloodRequests.CountAsync(b => b.Status == RequestStatus.Fulfilled);
        return View();
    }
}
