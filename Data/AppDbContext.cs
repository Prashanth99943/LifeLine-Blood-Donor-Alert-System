using LifeLine.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LifeLine.Data;

// IdentityDbContext already contains the login tables (users, roles).
// We add our four tables on top.
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DonorProfile> DonorProfiles => Set<DonorProfile>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();
    public DbSet<DonorResponse> DonorResponses => Set<DonorResponse>();
}
