using LifeLine.Models;
using LifeLine.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Data;

/// <summary>Creates the database on first run and fills it with demo accounts, donors and past requests.</summary>
public static class SeedData
{
    public static async Task InitAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;                       // already seeded

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        foreach (var r in new[] { "Admin", "Hospital", "Donor" }) await roles.CreateAsync(new IdentityRole(r));

        async Task<string> Make(string email, string role, string password)
        {
            var u = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            await users.CreateAsync(u, password);
            await users.AddToRoleAsync(u, role);
            return u.Id;
        }

        await Make("admin@lifeline.com", "Admin", "Admin@123");

        // ---- hospitals (the 4th one is NOT verified, so the admin can demo the "Verify" button)
        var hospitalRows = new (string Name, string City, bool Verified)[]
        {
            ("City General Hospital", "Chennai", true),
            ("Deccan Care Institute", "Hyderabad", true),
            ("Metro Heart Centre", "Bengaluru", true),
            ("Krishna Lifeline Clinic", "Vijayawada", false),
        };
        var hospitals = new List<Hospital>();
        for (int i = 0; i < hospitalRows.Length; i++)
        {
            var row = hospitalRows[i];
            var uid = await Make($"hospital{i + 1}@lifeline.com", "Hospital", "Pass@123");
            var c = CityData.Cities[row.City];
            hospitals.Add(new Hospital
            {
                UserId = uid, Name = row.Name, City = row.City, Phone = $"9000000{i + 1:000}",
                Latitude = c.Lat, Longitude = c.Lon, IsVerified = row.Verified
            });
        }

        // ---- donors. DaysAgo = days since last donation (-1 = never donated). Under 90 = still resting.
        var donorRows = new (string Name, string Group, string City, int DaysAgo)[]
        {
            ("Arjun Kumar","O+","Chennai",-1),       ("Karthik Raj","A+","Chennai",200),   ("Vignesh S","B+","Chennai",25),
            ("Suresh Babu","O-","Chennai",150),      ("Priya Nair","AB+","Chennai",-1),    ("Divya R","A-","Chennai",120),
            ("Rahul Reddy","O+","Hyderabad",-1),     ("Srinivas M","B+","Hyderabad",300),  ("Naveen P","A+","Hyderabad",40),
            ("Praveen K","O+","Hyderabad",95),       ("Kiran D","AB-","Hyderabad",-1),     ("Aditya Rao","O+","Bengaluru",130),
            ("Harish N","A+","Bengaluru",-1),        ("Mohan T","B-","Bengaluru",210),     ("Ganesh B","O+","Bengaluru",20),
            ("Venkat Rao","O+","Vijayawada",-1),     ("Ramesh C","A+","Vijayawada",100),   ("Satish V","B+","Vijayawada",400),
            ("Anil Kumar","O-","Vijayawada",-1),     ("Ravi Teja","O+","Visakhapatnam",180),("Charan G","A+","Visakhapatnam",-1),
            ("Pavan S","B+","Visakhapatnam",60),     ("Senthil A","O+","Coimbatore",-1),   ("Lakshmi M","AB+","Coimbatore",250),
        };
        var donors = new List<DonorProfile>();
        for (int i = 0; i < donorRows.Length; i++)
        {
            var d = donorRows[i];
            var uid = await Make($"donor{i + 1}@lifeline.com", "Donor", "Pass@123");
            var c = CityData.Cities[d.City];
            donors.Add(new DonorProfile
            {
                UserId = uid, FullName = d.Name, Phone = $"98765{i + 1:00000}", BloodGroup = d.Group, City = d.City,
                Latitude = c.Lat + ((i % 5) - 2) * 0.012,           // small offset so markers do not overlap on the map
                Longitude = c.Lon + ((i % 3) - 1) * 0.015,
                LastDonationDate = d.DaysAgo < 0 ? null : DateTime.UtcNow.AddDays(-d.DaysAgo),
                IsAvailable = true
            });
        }
        db.Hospitals.AddRange(hospitals);
        db.DonorProfiles.AddRange(donors);
        await db.SaveChangesAsync();

        // ---- a few finished requests so the charts and counters are not empty on day one
        var past = new (int H, string Group, int Units, RequestStatus St, int Days, int[] Donors)[]
        {
            (1, "O+", 1, RequestStatus.Fulfilled, 6, new[] { 7 }),
            (0, "A+", 2, RequestStatus.Fulfilled, 5, new[] { 2, 13 }),
            (2, "B+", 1, RequestStatus.Fulfilled, 4, new[] { 8 }),
            (1, "AB-", 1, RequestStatus.Cancelled, 3, Array.Empty<int>()),
            (0, "O-", 1, RequestStatus.Expired, 2, Array.Empty<int>()),
            (2, "O+", 2, RequestStatus.Fulfilled, 1, new[] { 12, 15 }),
        };
        foreach (var p in past)
        {
            var h = hospitals[p.H];
            var created = DateTime.UtcNow.AddDays(-p.Days);
            var req = new BloodRequest
            {
                HospitalId = h.Id, BloodGroup = p.Group, UnitsNeeded = p.Units,
                Urgency = p.Units > 1 ? Urgency.Urgent : Urgency.Normal, Status = p.St,
                PatientNote = "Sample record", CreatedAt = created, ExpiresAt = created.AddHours(48)
            };
            int k = 0;
            foreach (var n in p.Donors)
            {
                var d = donors[n - 1];
                req.Responses.Add(new DonorResponse
                {
                    DonorProfileId = d.Id, Status = ResponseStatus.Accepted, Donated = true, Score = 80,
                    DistanceKm = Math.Round(BloodRules.DistanceKm(h.Latitude, h.Longitude, d.Latitude, d.Longitude), 1),
                    NotifiedAt = created, RespondedAt = created.AddMinutes(6 + 7 * k++)
                });
            }
            db.BloodRequests.Add(req);
        }
        await db.SaveChangesAsync();
    }
}
