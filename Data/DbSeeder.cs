using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Data;

/// <summary>
/// Seeds a representative dataset mirroring the frontend's dummy data (src/lib/data,
/// src/features/*/data.ts) — same tenant/facility/user names, categories, statuses, and prices.
///
/// Scope note (see BACKEND_IMPLEMENTATION_PLAN.md §10): the frontend's real labeling-export mock
/// data has 91 recipes / ~153 ingredients. Reproducing every row here by hand would be a large,
/// error-prone transcription exercise for a one-time seed; instead this seeds a representative
/// subset across every category/table (~12 recipes, ~25 ingredients) so every page has real data
/// to render, and documents the full CSV import path (`POST /ingredients/import`, planned) as how
/// the complete 153-row set gets loaded for a real environment. Idempotent — skipped if any tenant
/// already exists.
/// </summary>
public static class DbSeeder
{
    private const string DevPassword = "Passwort123!";

    /// <summary>Deterministic GUID derived from a readable mnemonic key (e.g. "U1", "IN12") so
    /// re-running the seeder against the same empty database always produces the same IDs.</summary>
    private static Guid G(string key)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("DailyGourmet-Seed-" + key));
        return new Guid(hash);
    }

    public static async Task SeedAsync(DailyGourmetDbContext db)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync())
            return;

        var hasher = new PasswordHasher<User>();
        var now = new DateTime(2026, 8, 6, 8, 0, 0, DateTimeKind.Utc);

        // ---- Tenant ----
        var tenantId = G("T1");
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = "Daily Gourmet GmbH",
            Status = TenantStatus.AKTIV,
            MainContactName = "Miriam Hoffmann",
            MainContactEmail = "info@daily-gourmet.de",
            CreatedAt = now,
        };
        db.Tenants.Add(tenant);
        db.TenantProfiles.Add(new TenantProfile
        {
            TenantId = tenantId,
            VatId = "DE 123 456 789",
            Street = "Eiland 2",
            PostalCode = "42103",
            City = "Wuppertal",
            Phone = "(0)202 – 479 47 001",
            Email = "info@daily-gourmet.de",
            Timezone = "Europe/Berlin",
            Currency = "EUR",
        });
        db.TenantSettings.Add(new TenantSettings { TenantId = tenantId });
        db.TenantNotificationSettings.AddRange(
            new TenantNotificationSetting { Id = G("T1N1"), TenantId = tenantId, EventKey = "MealPlanPublished", Enabled = true, CreatedAt = now },
            new TenantNotificationSetting { Id = G("T1N2"), TenantId = tenantId, EventKey = "DeadlineApproaching", Enabled = true, CreatedAt = now },
            new TenantNotificationSetting { Id = G("T1N3"), TenantId = tenantId, EventKey = "OrderChangedAfterSubmit", Enabled = true, CreatedAt = now },
            new TenantNotificationSetting { Id = G("T1N4"), TenantId = tenantId, EventKey = "ProductionPlanChanged", Enabled = false, CreatedAt = now });

        // ---- Feature flags (global) ----
        var flags = new[]
        {
            (Key: "kundenportal", Name: "Kundenportal", Enabled: true),
            (Key: "naehrwert-api", Name: "Nährwert-API (Zutaten)", Enabled: true),
            (Key: "einkaufslisten", Name: "Einkaufslisten", Enabled: true),
            (Key: "bedarfsprognose", Name: "Bedarfsprognose (Basis)", Enabled: false),
            (Key: "white-label", Name: "White-Label-Branding", Enabled: false),
            (Key: "mehrsprachigkeit", Name: "Mehrsprachigkeit", Enabled: false),
        };
        foreach (var f in flags)
        {
            var id = G("FF-" + f.Key);
            db.FeatureFlags.Add(new FeatureFlag { Id = id, Key = f.Key, Name = f.Name, DefaultEnabled = f.Enabled, CreatedAt = now });
            db.TenantFeatureFlags.Add(new TenantFeatureFlag { TenantId = tenantId, FeatureFlagId = id, Enabled = f.Enabled });
        }

        // ---- Lookups ----
        var recipeCategoryNames = new[] { "Beilage", "Belegtes Brötchen", "Dessert", "Dip", "Hauptgericht", "Pasta", "Salat/ Rohkost", "Sauce", "Sonstiges", "Suppe" };
        var recipeCategories = recipeCategoryNames.Select((n, i) => new RecipeCategory { Id = G($"RC{i + 1}"), Name = n, CreatedAt = now }).ToList();
        db.RecipeCategories.AddRange(recipeCategories);

        var ingredientCategoryNames = new[] { "Gemüse", "Obst", "Fleisch & Geflügel", "Fisch", "Molkereiprodukte", "Eier & Frischware", "Trockenware", "Konserven", "Hülsenfrüchte", "Gewürze & Saucen", "Sonstiges" };
        var ingredientCategories = ingredientCategoryNames.Select((n, i) => new IngredientCategory { Id = G($"IC{i + 1}"), Name = n, CreatedAt = now }).ToList();
        db.IngredientCategories.AddRange(ingredientCategories);

        var allergenNames = new[] { "Gluten", "Krebstiere", "Eier", "Fisch", "Erdnüsse", "Soja", "Milch", "Schalenfrüchte", "Sellerie", "Senf", "Sesam", "Sulfite", "Lupinen", "Weichtiere" };
        var allergens = allergenNames.Select((n, i) => new Allergen { Id = G($"AL{i + 1}"), Name = n, CreatedAt = now }).ToList();
        db.Allergens.AddRange(allergens);

        var targetGroupNames = new[] { "Kita", "Schule", "Betriebsgastronomie", "Seniorenverpflegung", "Reha-/Klinikverpflegung" };
        var targetGroups = targetGroupNames.Select((n, i) => new TargetAudienceGroup { Id = G($"TG{i + 1}"), Name = n, CreatedAt = now }).ToList();
        db.TargetAudienceGroups.AddRange(targetGroups);

        var storage = new[] { "Kühlhaus 1", "Kühlhaus 2", "Kühlhaus 3", "Trockenlager", "Gewürzregal", "Hauptlager" }
            .Select((n, i) => new StorageLocation { Id = G($"SL{i + 1}"), TenantId = tenantId, Name = n, CreatedAt = now }).ToList();
        db.StorageLocations.AddRange(storage);

        // ---- Location & Facilities ----
        var locationId = G("L1");
        db.Locations.Add(new Location
        {
            Id = locationId,
            TenantId = tenantId,
            Name = "Daily Gourmet",
            Address = "Eiland 2, 42103 Wuppertal",
            ContactPerson = "Petra Salomon",
            CapacityPortions = 3400,
            Status = LocationStatus.AKTIV,
            CreatedAt = now,
        });

        var facilityData = new[]
        {
            (Id: G("F1"), Name: "Musterschule Nord", Cust: "DG-1001", Price: 5.20m, Days: "Mo,Di,Mi,Do,Fr", Status: FacilityStatus.AKTIV),
            (Id: G("F2"), Name: "Kita Sonnenblume", Cust: "DG-1002", Price: 4.60m, Days: "Mo,Di,Mi,Do,Fr", Status: FacilityStatus.AKTIV),
            (Id: G("F3"), Name: "Betriebsrestaurant Rheinblick", Cust: "DG-1003", Price: 6.50m, Days: "Mo,Di,Mi,Do,Fr", Status: FacilityStatus.AKTIV),
            (Id: G("F4"), Name: "Seniorenzentrum Am Park", Cust: "DG-1004", Price: 5.90m, Days: "Mo,Di,Mi,Do,Fr,Sa,So", Status: FacilityStatus.INAKTIV),
        };
        foreach (var f in facilityData)
        {
            db.Facilities.Add(new Facility
            {
                Id = f.Id,
                TenantId = tenantId,
                LocationId = locationId,
                Name = f.Name,
                CustomerNumber = f.Cust,
                Address = $"{f.Name}, 42103 Wuppertal",
                ContactPerson = "Ansprechpartner " + f.Name,
                Email = $"kontakt@{f.Cust.ToLowerInvariant()}.example.de",
                Phone = "0202 4794700",
                ActiveWeekdays = f.Days,
                PortionPrice = f.Price,
                Status = f.Status,
                Notes = f.Status == FacilityStatus.INAKTIV ? "Vertrag pausiert bis September." : null,
                CreatedAt = now,
            });
        }

        // ---- Users (one per role + a facility-linked pair) ----
        var users = new (Guid Id, string Name, string Email, Role Role, UserStatus Status, Guid? FacilityId)[]
        {
            (G("U1"), "Berk-Can Aydin", "berkcan@gentle-webdesign.com", Role.SUPER_ADMIN, UserStatus.AKTIV, null),
            (G("U2"), "Miriam Hoffmann", "miriam.hoffmann@daily-gourmet.de", Role.TENANT_OWNER, UserStatus.AKTIV, null),
            (G("U3"), "Jonas Weber", "jonas.weber@daily-gourmet.de", Role.TENANT_ADMIN, UserStatus.AKTIV, null),
            (G("U4"), "Petra Salomon", "petra.salomon@daily-gourmet.de", Role.KITCHEN_MANAGER, UserStatus.AKTIV, null),
            (G("U5"), "Ali Yildiz", "ali.yildiz@daily-gourmet.de", Role.KITCHEN_STAFF, UserStatus.AKTIV, null),
            (G("U6"), "Claudia Winter", "claudia.winter@musterschule-nord.example.de", Role.FACILITY_ADMIN, UserStatus.AKTIV, facilityData[0].Id),
            (G("U7"), "Sven Fischer", "sven.fischer@musterschule-nord.example.de", Role.FACILITY_USER, UserStatus.EINGELADEN, facilityData[0].Id),
            (G("U8"), "Lena Roth", "lena.roth@daily-gourmet.de", Role.READ_ONLY, UserStatus.DEAKTIVIERT, null),
            (G("U9"), "Markus Becker", "markus.becker@daily-gourmet.de", Role.DRIVER, UserStatus.AKTIV, null),
        };
        foreach (var u in users)
        {
            var user = new User
            {
                Id = u.Id,
                TenantId = u.Role == Role.SUPER_ADMIN ? null : tenantId,
                FacilityId = u.FacilityId,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role,
                Status = u.Status,
                LastLoginAt = u.Status == UserStatus.AKTIV ? now : null,
                FailedLoginCount = 0,
                CreatedAt = now,
            };
            user.PasswordHash = hasher.HashPassword(user, DevPassword);
            db.Users.Add(user);
        }
        db.Drivers.Add(new Driver
        {
            Id = G("DRV1"),
            TenantId = tenantId,
            UserId = G("U9"),
            Phone = "0176 55501201",
            VehicleDescription = "Mercedes Sprinter Kühl",
            LicensePlate = "D-DG 201",
            CreatedAt = now,
        });

        // ---- Suppliers ----
        var suppliers = new[] { "Frischdienst Rheinland", "Bäckerei Korn & Krume", "Metzgerei Wagner", "Bio-Kontor West" }
            .Select((n, i) => new Supplier { Id = G($"SUP{i + 1}"), TenantId = tenantId, Name = n, CreatedAt = now }).ToList();
        db.Suppliers.AddRange(suppliers);

        // ---- Ingredients (representative subset, ~25 rows across categories) ----
        Guid Cat(string name) => ingredientCategories.First(c => c.Name == name).Id;
        var ingredientDefs = new (string Name, string Article, string Category, Unit BaseUnit, string PurchaseUnit, decimal ConvFactor, decimal Price, int? AllergenIdx, string Storage, double Kcal, double Protein, double Fat, double Carbs, double Sugar, double Salt)[]
        {
            ("Vollkornnudeln", "ART-0001", "Trockenware", Unit.kg, "Sack (5 kg)", 5m, 8.90m, 0, "Trockenware", 351, 13, 2.5, 66, 3, 0.02),
            ("Basmatireis", "ART-0002", "Trockenware", Unit.kg, "Sack (10 kg)", 10m, 16.50m, 0, "Trockenware", 349, 7.5, 0.6, 78, 0.1, 0.01),
            ("Tomaten", "ART-0003", "Gemüse", Unit.kg, "Kiste (10 kg)", 10m, 14.00m, 0, "Kühlhaus 1", 18, 0.9, 0.2, 3.9, 2.6, 0.01),
            ("Zwiebeln", "ART-0004", "Gemüse", Unit.kg, "Sack (25 kg)", 25m, 18.00m, 1, "Trockenlager", 40, 1.1, 0.1, 9.3, 4.2, 0.01),
            ("Möhren", "ART-0005", "Gemüse", Unit.kg, "Kiste (10 kg)", 10m, 9.50m, 0, "Kühlhaus 1", 41, 0.9, 0.2, 9.6, 4.7, 0.06),
            ("Hähnchenbrust", "ART-0006", "Fleisch & Geflügel", Unit.kg, "Karton (10 kg)", 10m, 62.00m, 2, "Kühlhaus 3", 165, 31, 3.6, 0, 0, 0.07),
            ("Rinderhack", "ART-0007", "Fleisch & Geflügel", Unit.kg, "Karton (10 kg)", 10m, 78.00m, 2, "Kühlhaus 3", 254, 17.2, 20, 0, 0, 0.09),
            ("Lachsfilet", "ART-0008", "Fisch", Unit.kg, "Karton (5 kg)", 5m, 95.00m, null, "Kühlhaus 3", 208, 20, 13, 0, 0, 0.08),
            ("Vollmilch 3,5%", "ART-0009", "Molkereiprodukte", Unit.l, "Kanister (10 l)", 10m, 11.90m, null, "Kühlhaus 2", 64, 3.4, 3.6, 4.8, 4.8, 0.13),
            ("Butter", "ART-0010", "Molkereiprodukte", Unit.kg, "Block (5 kg)", 5m, 32.50m, null, "Kühlhaus 2", 741, 0.7, 82, 0.5, 0.5, 0.02),
            ("Gouda", "ART-0011", "Molkereiprodukte", Unit.kg, "Laib (5 kg)", 5m, 41.00m, null, "Kühlhaus 2", 356, 25, 28, 0.1, 0.1, 1.7),
            ("Hühnereier", "ART-0012", "Eier & Frischware", Unit.Stueck, "Karton (360 Stück)", 360m, 79.20m, null, "Kühlhaus 2", 143, 12.6, 9.5, 0.7, 0.7, 0.13),
            ("Vollkornmehl", "ART-0013", "Trockenware", Unit.kg, "Sack (25 kg)", 25m, 22.50m, null, "Trockenlager", 340, 13.2, 2.5, 61, 1.5, 0.01),
            ("Kidneybohnen (Konserve)", "ART-0014", "Konserven", Unit.kg, "Dose (2,5 kg)", 2.5m, 6.10m, 3, "Trockenlager", 127, 8.7, 0.5, 22.8, 0.3, 0.53),
            ("Kichererbsen (Konserve)", "ART-0015", "Hülsenfrüchte", Unit.kg, "Dose (2,5 kg)", 2.5m, 5.80m, 3, "Trockenlager", 139, 8.9, 2.6, 20.1, 0.3, 0.5),
            ("Olivenöl", "ART-0016", "Gewürze & Saucen", Unit.l, "Kanister (5 l)", 5m, 45.00m, null, "Trockenlager", 884, 0, 100, 0, 0, 0),
            ("Jodsalz", "ART-0017", "Gewürze & Saucen", Unit.kg, "Sack (25 kg)", 25m, 9.00m, null, "Gewürzregal", 0, 0, 0, 0, 0, 99.9),
            ("Sonnenblumenöl", "ART-0018", "Gewürze & Saucen", Unit.l, "Kanister (10 l)", 10m, 22.00m, null, "Trockenlager", 884, 0, 100, 0, 0, 0),
            ("Äpfel", "ART-0019", "Obst", Unit.kg, "Kiste (12 kg)", 12m, 15.60m, 4, "Kühlhaus 1", 52, 0.3, 0.2, 14, 10.4, 0),
            ("Bananen", "ART-0020", "Obst", Unit.kg, "Kiste (18 kg)", 18m, 21.60m, null, "Kühlhaus 1", 89, 1.1, 0.3, 23, 12, 0),
            ("Weizenbrötchen", "ART-0021", "Trockenware", Unit.Stueck, "Karton (50 Stück)", 50m, 12.50m, 0, "Trockenlager", 275, 9, 1.7, 53, 4, 1.2),
            ("Sahne", "ART-0022", "Molkereiprodukte", Unit.l, "Kanister (5 l)", 5m, 14.50m, null, "Kühlhaus 2", 292, 2.2, 30, 3.3, 3.3, 0.05),
            ("Erbsen tiefgefroren", "ART-0023", "Gemüse", Unit.kg, "Beutel (2,5 kg)", 2.5m, 5.20m, null, "Kühlhaus 1", 81, 5.4, 0.5, 12.6, 4.9, 0.01),
            ("Paprika rot", "ART-0024", "Gemüse", Unit.kg, "Kiste (5 kg)", 5m, 12.90m, null, "Kühlhaus 1", 31, 1, 0.3, 6, 4.2, 0),
            ("Joghurt natur", "ART-0025", "Molkereiprodukte", Unit.kg, "Eimer (10 kg)", 10m, 15.90m, null, "Kühlhaus 2", 61, 3.5, 3.5, 4.7, 4.7, 0.1),
        };
        var allergenCycle = new[] { "Gluten", "Milch", "Eier", "Senf", "Sellerie" };
        var ingredients = new List<Ingredient>();
        for (int idx = 0; idx < ingredientDefs.Length; idx++)
        {
            var d = ingredientDefs[idx];
            var ing = new Ingredient
            {
                Id = G($"IN{idx + 1}"),
                TenantId = tenantId,
                CategoryId = Cat(d.Category),
                SupplierId = suppliers[idx % suppliers.Count].Id,
                Name = d.Name,
                ArticleNumber = d.Article,
                BaseUnit = d.BaseUnit,
                PurchaseUnit = d.PurchaseUnit,
                ConversionFactor = d.ConvFactor,
                PurchasePrice = d.Price,
                Vegetarian = d.Category is not ("Fleisch & Geflügel" or "Fisch"),
                Vegan = d.Category is not ("Fleisch & Geflügel" or "Fisch" or "Molkereiprodukte" or "Eier & Frischware"),
                Bio = idx % 5 == 0,
                Regional = idx % 3 == 0,
                Active = true,
                Nutrition = new IngredientNutrition
                {
                    Kcal = (decimal)d.Kcal,
                    ProteinG = (decimal)d.Protein,
                    FatG = (decimal)d.Fat,
                    CarbsG = (decimal)d.Carbs,
                    SugarG = (decimal)d.Sugar,
                    SaltG = (decimal)d.Salt,
                    Source = NutritionSource.Manuell,
                },
                CreatedAt = now,
            };
            if (d.AllergenIdx is int allergenIdx)
                db.IngredientAllergens.Add(new IngredientAllergen { IngredientId = ing.Id, AllergenId = allergens.First(a => a.Name == allergenCycle[allergenIdx]).Id });
            ingredients.Add(ing);
        }
        db.Ingredients.AddRange(ingredients);

        Guid Ing(string articleNumber) => ingredients.First(i => i.ArticleNumber == articleNumber).Id;
        Guid RCat(string name) => recipeCategories.First(c => c.Name == name).Id;

        // ---- Recipes (representative subset) ----
        var recipeDefs = new (string Name, string Category, int Portions, Difficulty Diff, bool Veg, bool Vegan, (string Article, decimal Qty, Unit Unit)[] Zutaten, string[] Steps)[]
        {
            ("Vollkorn-Penne mit Tomatensauce", "Pasta", 100, Difficulty.Einfach, true, true,
                new[] { ("ART-0001", 8m, Unit.kg), ("ART-0003", 6m, Unit.kg), ("ART-0004", 1.5m, Unit.kg), ("ART-0016", 0.5m, Unit.l) },
                new[] { "Nudeln in Salzwasser kochen.", "Zwiebeln anschwitzen, Tomaten zugeben und einkochen.", "Nudeln unterheben und abschmecken." }),
            ("Hähnchengeschnetzeltes mit Reis", "Hauptgericht", 100, Difficulty.Mittel, false, false,
                new[] { ("ART-0006", 12m, Unit.kg), ("ART-0002", 8m, Unit.kg), ("ART-0024", 2m, Unit.kg), ("ART-0009", 2m, Unit.l) },
                new[] { "Hähnchenbrust anbraten.", "Paprika zugeben und ablöschen.", "Mit Reis servieren." }),
            ("Gemüsesuppe mit Möhren und Erbsen", "Suppe", 100, Difficulty.Einfach, true, true,
                new[] { ("ART-0005", 4m, Unit.kg), ("ART-0023", 3m, Unit.kg), ("ART-0004", 1m, Unit.kg) },
                new[] { "Gemüse würfeln und andünsten.", "Mit Brühe aufgießen und köcheln lassen." }),
            ("Rinderhack-Bolognese", "Sauce", 100, Difficulty.Mittel, false, false,
                new[] { ("ART-0007", 10m, Unit.kg), ("ART-0003", 5m, Unit.kg), ("ART-0004", 1.5m, Unit.kg) },
                new[] { "Hack anbraten.", "Tomaten und Zwiebeln zugeben, lange köcheln lassen." }),
            ("Lachs auf Gemüsebett", "Hauptgericht", 100, Difficulty.Anspruchsvoll, false, false,
                new[] { ("ART-0008", 12m, Unit.kg), ("ART-0005", 4m, Unit.kg), ("ART-0024", 2m, Unit.kg) },
                new[] { "Gemüse in die Form geben.", "Lachs darauflegen und garen." }),
            ("Käsebrötchen belegt", "Belegtes Brötchen", 100, Difficulty.Einfach, true, false,
                new[] { ("ART-0021", 100m, Unit.Stueck), ("ART-0011", 3m, Unit.kg), ("ART-0010", 1m, Unit.kg) },
                new[] { "Brötchen aufschneiden und buttern.", "Mit Käse belegen." }),
            ("Kichererbsen-Salat", "Salat/ Rohkost", 100, Difficulty.Einfach, true, true,
                new[] { ("ART-0015", 6m, Unit.kg), ("ART-0024", 2m, Unit.kg), ("ART-0016", 0.3m, Unit.l) },
                new[] { "Kichererbsen abtropfen lassen.", "Mit Paprika und Öl vermengen." }),
            ("Apfelmus", "Dessert", 100, Difficulty.Einfach, true, true,
                new[] { ("ART-0019", 10m, Unit.kg) },
                new[] { "Äpfel schälen und würfeln.", "Weich kochen und pürieren." }),
            ("Joghurt-Dip mit Kräutern", "Dip", 100, Difficulty.Einfach, true, false,
                new[] { ("ART-0025", 5m, Unit.kg) },
                new[] { "Joghurt glattrühren und würzen." }),
            ("Kidneybohnen-Chili (vegetarisch)", "Hauptgericht", 100, Difficulty.Mittel, true, true,
                new[] { ("ART-0014", 6m, Unit.kg), ("ART-0003", 5m, Unit.kg), ("ART-0004", 1.5m, Unit.kg) },
                new[] { "Zwiebeln anschwitzen.", "Bohnen und Tomaten zugeben, köcheln lassen." }),
            ("Rührei mit Schnittlauch", "Hauptgericht", 100, Difficulty.Einfach, true, false,
                new[] { ("ART-0012", 300m, Unit.Stueck), ("ART-0022", 2m, Unit.l) },
                new[] { "Eier verquirlen.", "In der Pfanne unter Rühren stocken lassen." }),
            ("Bananen-Joghurt-Dessert", "Dessert", 100, Difficulty.Einfach, true, false,
                new[] { ("ART-0020", 8m, Unit.kg), ("ART-0025", 6m, Unit.kg) },
                new[] { "Bananen in Scheiben schneiden.", "Mit Joghurt schichten." }),
        };
        var recipes = new List<Recipe>();
        for (int idx = 0; idx < recipeDefs.Length; idx++)
        {
            var d = recipeDefs[idx];
            var recipe = new Recipe
            {
                Id = G($"RE{idx + 1}"),
                TenantId = tenantId,
                CategoryId = RCat(d.Category),
                CreatedByUserId = G("U2"),
                Name = d.Name,
                Description = $"{d.Name} — Standardrezeptur für {d.Portions} Portionen.",
                StandardPortions = d.Portions,
                PortionWeightG = 250,
                PrepTimeMinutes = 30 + idx * 5,
                Difficulty = d.Diff,
                Vegetarian = d.Veg,
                Vegan = d.Vegan,
                Active = true,
                Version = 1,
                CreatedAt = now,
            };
            foreach (var (step, i) in d.Steps.Select((s, i) => (s, i)))
                db.RecipePrepSteps.Add(new RecipePrepStep { Id = G($"RE{idx + 1}S{i + 1}"), RecipeId = recipe.Id, StepNumber = i + 1, Text = step, CreatedAt = now });
            foreach (var (article, qty, unit) in d.Zutaten)
                db.RecipeIngredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, IngredientId = Ing(article), Quantity = qty, Unit = unit, CreatedAt = now });
            recipes.Add(recipe);
        }
        db.Recipes.AddRange(recipes);

        // ---- Meal plans (current week 32 + prior week 31, both published) ----
        var weekdays = new[] { "Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag" };
        MealPlan BuildMealPlan(Guid id, int week, int year, DateOnly monday, MealPlanStatus status, Guid[] recipeIds)
        {
            var plan = new MealPlan { Id = id, TenantId = tenantId, CalendarWeek = week, Year = year, Status = status, CreatedAt = now };
            db.MealPlans.Add(plan);
            db.MealPlanLocations.Add(new MealPlanLocation { MealPlanId = id, LocationId = locationId });
            foreach (var f in facilityData.Where(f => f.Status == FacilityStatus.AKTIV))
                db.MealPlanFacilities.Add(new MealPlanFacility { MealPlanId = id, FacilityId = f.Id });

            for (int i = 0; i < 5; i++)
            {
                var day = new MealPlanDay { Id = Guid.NewGuid(), MealPlanId = id, Weekday = weekdays[i], Date = monday.AddDays(i), CreatedAt = now };
                db.MealPlanDays.Add(day);
                foreach (var rid in new[] { recipeIds[i % recipeIds.Length], recipeIds[(i + 1) % recipeIds.Length] })
                    db.MealPlanItems.Add(new MealPlanItem { Id = Guid.NewGuid(), MealPlanDayId = day.Id, RecipeId = rid, CreatedAt = now });
            }
            return plan;
        }

        var week32RecipeIds = recipes.Select(r => r.Id).Take(6).ToArray();
        var week31RecipeIds = recipes.Select(r => r.Id).Skip(4).Take(6).ToArray();
        var mealPlan32Id = G("MP32");
        var mealPlan31Id = G("MP31");
        BuildMealPlan(mealPlan32Id, 32, 2026, new DateOnly(2026, 8, 3), MealPlanStatus.PUBLISHED, week32RecipeIds);
        BuildMealPlan(mealPlan31Id, 31, 2026, new DateOnly(2026, 7, 27), MealPlanStatus.CLOSED, week31RecipeIds);

        // ---- Orders ----
        (Guid Id, Guid FacilityId, Guid MealPlanId, OrderStatus Status, DateOnly WeekStart, Guid[] RecipeIds)[] orderDefs =
        {
            (G("O1"), facilityData[0].Id, mealPlan32Id, OrderStatus.CONFIRMED, new DateOnly(2026, 8, 3), week32RecipeIds),
            (G("O2"), facilityData[1].Id, mealPlan32Id, OrderStatus.SUBMITTED, new DateOnly(2026, 8, 3), week32RecipeIds),
            (G("O3"), facilityData[2].Id, mealPlan32Id, OrderStatus.DRAFT, new DateOnly(2026, 8, 3), week32RecipeIds),
            (G("O4"), facilityData[0].Id, mealPlan31Id, OrderStatus.LOCKED, new DateOnly(2026, 7, 27), week31RecipeIds),
            (G("O5"), facilityData[1].Id, mealPlan31Id, OrderStatus.LOCKED, new DateOnly(2026, 7, 27), week31RecipeIds),
            (G("O6"), facilityData[2].Id, mealPlan31Id, OrderStatus.LOCKED, new DateOnly(2026, 7, 27), week31RecipeIds),
        };
        foreach (var o in orderDefs)
        {
            var deadline = o.WeekStart.AddDays(-1).ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc);
            var order = new Order
            {
                Id = o.Id,
                TenantId = tenantId,
                FacilityId = o.FacilityId,
                MealPlanId = o.MealPlanId,
                Status = o.Status,
                SubmittedAt = o.Status is OrderStatus.DRAFT ? null : deadline.AddHours(-2),
                DeadlineAtUtc = deadline,
                CreatedAt = now,
            };
            db.Orders.Add(order);
            for (int i = 0; i < 5; i++)
                db.OrderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Date = o.WeekStart.AddDays(i),
                    RecipeId = o.RecipeIds[i % o.RecipeIds.Length],
                    Portions = o.Status == OrderStatus.DRAFT ? 0 : 40 + i * 5,
                    CreatedAt = now,
                });
        }

        // ---- Production plan for today (week 32, Thursday) ----
        var productionPlanId = G("PP1");
        db.ProductionPlans.Add(new ProductionPlan { Id = productionPlanId, TenantId = tenantId, LocationId = locationId, Date = new DateOnly(2026, 8, 6), CreatedAt = now });
        foreach (var (rid, i) in week32RecipeIds.Take(4).Select((r, i) => (r, i)))
            db.ProductionPlanItems.Add(new ProductionPlanItem
            {
                Id = Guid.NewGuid(),
                ProductionPlanId = productionPlanId,
                RecipeId = rid,
                OrderedQuantity = 120 + i * 10,
                AdjustmentQuantity = i == 0 ? 12 : 0,
                AdjustmentReason = i == 0 ? "Sicherheitsmenge" : null,
                Status = i < 2 ? ProductionItemStatus.PREPARING : ProductionItemStatus.PLANNED,
                WorkStatus = i < 2 ? WorkStatus.ZUBEREITUNG : WorkStatus.OFFEN,
                CreatedAt = now,
            });

        // ---- Procurement list ----
        var procurementListId = G("PL1");
        db.ProcurementLists.Add(new ProcurementList { Id = procurementListId, TenantId = tenantId, LocationId = locationId, Label = "Bedarf KW 32 — Zentralküche", CalendarWeek = 32, Status = ProcurementListStatus.DRAFT, CreatedAt = now });
        foreach (var article in new[] { "ART-0001", "ART-0003", "ART-0004", "ART-0006" })
        {
            var ing = ingredients.First(i => i.ArticleNumber == article);
            db.ProcurementListItems.Add(new ProcurementListItem { Id = Guid.NewGuid(), ProcurementListId = procurementListId, IngredientId = ing.Id, Unit = ing.BaseUnit, TotalQuantityBase = 45, PurchaseQuantity = 5, CreatedAt = now });
        }

        // ---- Route ----
        var routeId = G("RT1");
        db.Routes.Add(new DeliveryRoute { Id = routeId, TenantId = tenantId, DriverId = G("DRV1"), LocationId = locationId, Name = "Route Nord", Date = new DateOnly(2026, 8, 6), PlannedDepartureTime = new TimeSpan(10, 15, 0), Status = RouteStatus.GEPLANT, CreatedAt = now });
        for (int i = 0; i < 2; i++)
        {
            var stop = new RouteStop
            {
                Id = Guid.NewGuid(),
                RouteId = routeId,
                FacilityId = facilityData[i].Id,
                SequenceNumber = i + 1,
                PlannedArrivalTime = new TimeSpan(10 + i, 45, 0),
                ContactName = "Ansprechpartner " + facilityData[i].Name,
                ContactPhone = "0202 4794700",
                Status = RouteStopStatus.OFFEN,
                CreatedAt = now,
            };
            db.RouteStops.Add(stop);
            db.RouteStopItems.Add(new RouteStopItem
            {
                Id = Guid.NewGuid(),
                RouteStopId = stop.Id,
                RecipeId = week32RecipeIds[0],
                Portions = 40,
                ContainerDescription = "3 × GN 1/1",
                TemperatureRequirement = "mind. 65 °C",
                CreatedAt = now,
            });
        }

        // ---- Support ticket ----
        db.SupportTickets.Add(new SupportTicket
        {
            Id = G("ST1"),
            TenantId = tenantId,
            CreatedByUserId = G("U2"),
            TicketNumber = "SUP-1042",
            Category = SupportCategory.FRAGE,
            Priority = SupportPriority.NORMAL,
            Title = "Frage zur Portionsberechnung",
            Message = "Wie wird die Portionsanzahl bei Zusatzmengen berechnet?",
            PageUrl = "/admin/production",
            Status = SupportStatus.OFFEN,
            CreatedAt = now,
        });

        // ---- Notifications ----
        db.Notifications.AddRange(
            new Notification { Id = G("N1"), TenantId = tenantId, Title = "Speiseplan veröffentlicht", Text = "KW 32 wurde veröffentlicht.", IsRead = false, CreatedAt = now },
            new Notification { Id = G("N2"), TenantId = tenantId, Title = "Frist läuft ab", Text = "Kita Sonnenblume hat noch keine Bestellung für KW 33.", IsRead = false, CreatedAt = now },
            new Notification { Id = G("N3"), TenantId = tenantId, Title = "Bestellung geändert", Text = "Musterschule Nord hat die Bestellung angepasst.", IsRead = true, CreatedAt = now });

        // ---- Audit log sample ----
        db.AuditLogs.Add(new AuditLog
        {
            Id = G("AU1"),
            TenantId = tenantId,
            UserId = G("U2"),
            Action = "Bestellung nach Frist geändert",
            Entity = "Order",
            EntityId = orderDefs[3].Id.ToString(),
            Reason = "Telefonische Korrektur der Schule (Ausflug 5b)",
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync();
    }
}
