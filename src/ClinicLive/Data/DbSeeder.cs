using ClinicLive.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicLive.Data;

/// <summary>
/// Applies migrations and seeds staff logins (always) plus demo data (Development only).
/// Idempotent — safe to run on every startup.
/// </summary>
public static class DbSeeder
{
    public const string ReceptionRole = "Reception";
    public const string PractitionerRole = "Practitioner";

    public static async Task SeedAsync(IServiceProvider services, bool includeDemoData)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { ReceptionRole, PractitionerRole })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        // .test is a reserved TLD (RFC 2606) — these addresses can never be real.
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(users, "reception@cliniclive.test", ReceptionRole);
        await EnsureUserAsync(users, "practitioner@cliniclive.test", PractitionerRole);

        if (includeDemoData && !await db.Patients.AnyAsync())
        {
            // Fictional people from around the world; +00 is an unassigned ITU country
            // code, so these phone numbers cannot exist anywhere.
            var today9 = DateTime.UtcNow.Date.AddHours(9);
            var demo = new (string Name, string Phone, int SlotOffset)[]
            {
                ("Maria Garcia", "+00-0000-0001", 0),
                ("David Chen", "+00-0000-0002", 1),
                ("Emma Wilson", "+00-0000-0003", 2),
                ("Omar Haddad", "+00-0000-0004", 4),
            };

            foreach (var (name, phone, offset) in demo)
            {
                db.Patients.Add(new Patient
                {
                    FullName = name,
                    Phone = phone,
                    Appointments =
                    [
                        new Appointment
                        {
                            StartsAt = today9.AddMinutes(offset * 15),
                            ConfirmationCode = $"DEMO{offset}{offset}",
                        },
                    ],
                });
            }

            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, string email, string role)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await users.CreateAsync(user, "Clinic!Live1");
        }

        if (!await users.IsInRoleAsync(user, role))
        {
            await users.AddToRoleAsync(user, role);
        }
    }
}
