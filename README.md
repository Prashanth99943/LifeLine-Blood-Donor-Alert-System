# LifeLine: Real-Time Blood Donor Alert and Emergency Coordination System

ASP.NET Core MVC (.NET 9), Entity Framework Core, SQL Server, SignalR, Bootstrap 5, Leaflet maps, Chart.js.

## Run it on your laptop
1. Install the .NET 9 SDK (`dotnet --version` should print 9.x).
2. Open a terminal in this folder and run `dotnet run`.
3. Open the address it prints (usually http://localhost:5000).

The first run creates the database `LifeLineDb` on SQL Server LocalDB and fills it with demo data.
To use a full SQL Server instead, change `ConnectionStrings:Default` in `appsettings.json`.
No SQL Server at all? Run with `DatabaseProvider=Sqlite` (see Hosting below).

## Demo accounts
| Role | Email | Password |
|---|---|---|
| Admin | admin@lifeline.com | Admin@123 |
| Hospital, Chennai | hospital1@lifeline.com | Pass@123 |
| Hospital, Hyderabad | hospital2@lifeline.com | Pass@123 |
| Hospital, unverified | hospital4@lifeline.com | Pass@123 |
| Donor O+, Chennai | donor1@lifeline.com | Pass@123 |
| Donor O-, Chennai | donor4@lifeline.com | Pass@123 |

donor1 to donor24 exist (see Admin > Donors). Remove the "Demo accounts" panel from `Views/Account/Login.cshtml` before sharing a public link.

## Live demo script (3 minutes)
1. Window A (normal): sign in as **hospital1**. Window B (incognito, or another browser/phone): sign in as **donor1**. Two windows are needed because one browser shares one login cookie.
2. In A: New request, blood group **O+**, 1 unit, **Normal**. Click Send.
3. In B, with no refresh: a toast and a beep appear, and an alert card shows up on the dashboard.
4. In B: click Accept. In A the progress bar fills, the request flips to Fulfilled and the activity feed updates.
5. In A: open the request, click Mark donated. In B the dashboard now says "Resting" (90-day rule).
6. Raise a **Critical** O+ request: the search radius widens (300 km) and more donors are alerted. Show the map.
7. Sign in as **admin** (third window): live counters, charts, map, and Hospitals > Verify hospital for the pending one.

## How the code is organised (explain it in this order)
| Folder | What it does |
|---|---|
| `Models/Entities.cs` | The 4 tables: DonorProfile, Hospital, BloodRequest, DonorResponse (plus the login tables from Identity). |
| `Services/BloodRules.cs` | Medical rules: blood-group compatibility, 90-day gap, Haversine distance. |
| `Services/MatchingService.cs` | **Filter, score, rank.** The core algorithm. |
| `Services/RequestService.cs` | Create request, donor responds, mark donated, cancel, expire. Each step pushes a SignalR message. |
| `Hubs/AlertHub.cs` | The SignalR hub. Admins join the "Admins" group; everyone else is addressed by user id. |
| `Controllers/` | Donor, Hospital, Admin, Account, Home. Each is protected by `[Authorize(Roles = ...)]`. |
| `Views/` | Razor pages. Files starting with `_Live` are the small fragments that refresh themselves. |
| `wwwroot/js/site.js` | Connects to SignalR, shows toasts, swaps in fresh fragments, draws maps. |

## The matching algorithm
1. **Filter**: drop donors who have alerts off, an incompatible blood group, a donation in the last 90 days, or are outside the radius.
2. **Score** (max 100): exact blood group 50 (compatible group 30) + proximity up to 40 (`40 * (1 - distance / radius)`) + 10 if never donated or last donation over 180 days ago.
3. **Rank**: highest first, alert the top N.

| Urgency | Radius | Donors alerted | Expires |
|---|---|---|---|
| Normal | 30 km | 5 | 72 h |
| Urgent | 100 km | 10 | 24 h |
| Critical | 300 km | 20 | 6 h |

## How "real time" works
- Every signed-in browser opens one SignalR connection to `/hubs/alerts` (the green "Live" dot in the top bar).
- When something changes, the server calls `Clients.User(id).SendAsync(...)`.
- The browser shows a toast, then fetches the small `_Live` fragment of the current page and swaps it in. No page reload, no polling.

## Hosting for free (so many people can open it)
GitHub Pages cannot run ASP.NET (static files only). Use one of these:

**A. Tunnel from your laptop (most reliable for a viva).** Run `dotnet run --urls http://localhost:5000`, then `cloudflared tunnel --url http://localhost:5000` (no account needed) or use Visual Studio Dev Tunnels. You get a public https link; share it and anyone can open it while your laptop is on. SignalR works through it.

**B. GitHub Codespaces.** Push this folder to a GitHub repo, open a Codespace, then:
```
export DatabaseProvider=Sqlite
export ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
dotnet run --urls http://0.0.0.0:5000
```
In the Ports tab set port 5000 to **Public** and share that URL. Free hours per month are limited, so stop the Codespace when done. If `dotnet --version` is below 9, install it with `curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0`.

**C. A free ASP.NET host such as MonsterASP.NET.** It advertises a free plan with SQL Server and SignalR support and no credit card. Publish from Visual Studio, and set the connection string they give you as `ConnectionStrings__Default`. Free hosts change their limits, so check the current plan first.

Many users at once: everyone opens the same link and signs in with their own account. One browser = one login, so use incognito windows or different devices for different roles.

## Likely viva questions
- **Why SignalR and not refreshing every few seconds?** The server pushes only when something changes, so alerts arrive instantly and there is no wasted traffic.
- **Why cookie login and not JWT?** It is one MVC application, so the browser cookie is simpler and safer. JWT is the right choice when a separate mobile app calls an API (listed as future scope).
- **How is the 90-day rule enforced?** `BloodRules.IsEligible` is checked in the filter step, and `LastDonationDate` is only updated when the hospital clicks "Mark donated".
- **How is donor privacy protected?** A hospital sees a donor's phone number only after that donor accepts.
- **What stops duplicate requests?** A hospital cannot open a second Open request for the same blood group.
- **Known limit:** if two donors accept at the same instant, the request can briefly be over-filled by one. Production fix: a concurrency token on the request row.
- **Why `EnsureCreated` and not migrations?** Simplest for a demo database. Migrations are the next step for production.
- **Future scope:** SMS and email alerts, JWT API for a mobile app, PDF reports, AI-assisted prioritisation, driving distance instead of straight-line distance.
