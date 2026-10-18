using LifeLine.Data;
using LifeLine.Models;
using LifeLine.Services;
using LifeLine.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LifeLine.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _sign;
    private readonly UserManager<IdentityUser> _users;
    private readonly AppDbContext _db;

    public AccountController(SignInManager<IdentityUser> sign, UserManager<IdentityUser> users, AppDbContext db)
    { _sign = sign; _users = users; _db = db; }

    [HttpGet] public IActionResult Login() => View(new LoginVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _sign.PasswordSignInAsync(vm.Email, vm.Password, isPersistent: true, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Email or password is incorrect.");
            return View(vm);
        }
        return RedirectToAction("Index", "Home");       // Home sends each role to its own dashboard
    }

    [HttpGet] public IActionResult RegisterDonor() => View(new DonorRegisterVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterDonor(DonorRegisterVm vm)
    {
        if (!BloodRules.Groups.Contains(vm.BloodGroup ?? "")) ModelState.AddModelError("BloodGroup", "Choose a blood group.");
        if (!CityData.Cities.ContainsKey(vm.City ?? "")) ModelState.AddModelError("City", "Choose a city from the list.");
        if (!ModelState.IsValid) return View(vm);

        var user = new IdentityUser { UserName = vm.Email, Email = vm.Email, EmailConfirmed = true };
        var created = await _users.CreateAsync(user, vm.Password);
        if (!created.Succeeded)
        {
            foreach (var e in created.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        await _users.AddToRoleAsync(user, "Donor");

        var c = CityData.Cities[vm.City];
        _db.DonorProfiles.Add(new DonorProfile
        {
            UserId = user.Id, FullName = vm.FullName, Phone = vm.Phone, BloodGroup = vm.BloodGroup,
            City = vm.City, Latitude = c.Lat, Longitude = c.Lon, IsAvailable = true
        });
        await _db.SaveChangesAsync();

        await _sign.SignInAsync(user, isPersistent: true);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet] public IActionResult RegisterHospital() => View(new HospitalRegisterVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterHospital(HospitalRegisterVm vm)
    {
        if (!CityData.Cities.ContainsKey(vm.City ?? "")) ModelState.AddModelError("City", "Choose a city from the list.");
        if (!ModelState.IsValid) return View(vm);

        var user = new IdentityUser { UserName = vm.Email, Email = vm.Email, EmailConfirmed = true };
        var created = await _users.CreateAsync(user, vm.Password);
        if (!created.Succeeded)
        {
            foreach (var e in created.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        await _users.AddToRoleAsync(user, "Hospital");

        var c = CityData.Cities[vm.City];
        _db.Hospitals.Add(new Hospital
        {
            UserId = user.Id, Name = vm.Name, Phone = vm.Phone, City = vm.City,
            Latitude = c.Lat, Longitude = c.Lon, IsVerified = false          // admin must verify
        });
        await _db.SaveChangesAsync();

        await _sign.SignInAsync(user, isPersistent: true);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _sign.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }
}
