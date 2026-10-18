namespace LifeLine.Services;

/// <summary>The fixed medical rules: who can give to whom, and how often a donor may donate.</summary>
public static class BloodRules
{
    public static readonly string[] Groups = { "O+", "O-", "A+", "A-", "B+", "B-", "AB+", "AB-" };

    /// <summary>Minimum days between two donations.</summary>
    public const int GapDays = 90;

    // Key = patient's group, value = donor groups that can safely give to that patient.
    private static readonly Dictionary<string, string[]> CanReceiveFrom = new()
    {
        ["O-"]  = new[] { "O-" },
        ["O+"]  = new[] { "O+", "O-" },
        ["A-"]  = new[] { "A-", "O-" },
        ["A+"]  = new[] { "A+", "A-", "O+", "O-" },
        ["B-"]  = new[] { "B-", "O-" },
        ["B+"]  = new[] { "B+", "B-", "O+", "O-" },
        ["AB-"] = new[] { "AB-", "A-", "B-", "O-" },
        ["AB+"] = Groups,
    };

    public static bool IsCompatible(string donorGroup, string patientGroup)
        => CanReceiveFrom[patientGroup].Contains(donorGroup);

    public static bool IsEligible(DateTime? lastDonation)
        => lastDonation == null || (DateTime.UtcNow - lastDonation.Value).TotalDays >= GapDays;

    /// <summary>Straight-line distance between two GPS points (Haversine formula).</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // earth radius in km
        double dLat = Rad(lat2 - lat1), dLon = Rad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double Rad(double deg) => deg * Math.PI / 180;
}

/// <summary>City list with coordinates. Using a fixed list means no paid maps/geocoding API is needed.</summary>
public static class CityData
{
    public static readonly Dictionary<string, (double Lat, double Lon)> Cities = new()
    {
        ["Chennai"]       = (13.0827, 80.2707),
        ["Hyderabad"]     = (17.3850, 78.4867),
        ["Bengaluru"]     = (12.9716, 77.5946),
        ["Vijayawada"]    = (16.5062, 80.6480),
        ["Visakhapatnam"] = (17.6868, 83.2185),
        ["Bhimavaram"]    = (16.5449, 81.5212),
        ["Coimbatore"]    = (11.0168, 76.9558),
        ["Madurai"]       = (9.9252, 78.1198),
        ["Mumbai"]        = (19.0760, 72.8777),
        ["Pune"]          = (18.5204, 73.8567),
        ["Delhi"]         = (28.6139, 77.2090),
        ["Kolkata"]       = (22.5726, 88.3639),
    };
}
