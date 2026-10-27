using System.Text.Json;
using AntiqueStore.Application.Customization;
using AntiqueStore.Domain.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AntiqueStore.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent seeding.
/// - <see cref="SeedEssentialsAsync"/>: settings, theme, navigation, homepage layout and policy pages (safe for production;
///   only inserts rows that don't exist yet, never overwrites admin changes).
/// - <see cref="SeedExampleCatalogAsync"/>: DEVELOPMENT ONLY example antiques. All example SKUs start with "EXAMPLE-" and use
///   placeholder images under /placeholders/, so they are easy to identify and remove.
/// No admin accounts or credentials are ever seeded; see README "Provisioning an admin".
/// </summary>
public sealed class DatabaseSeeder(AppDbContext db, ILogger<DatabaseSeeder> logger)
{
    private static readonly JsonSerializerOptions Json = SectionConfigValidator.Json;

    public async Task SeedEssentialsAsync(CancellationToken ct)
    {
        if (!await db.CommerceSettings.AnyAsync(ct))
            db.CommerceSettings.Add(new CommerceSettings
            {
                ReservationMinutes = 15, BankTransferPaymentWindowHours = 48, CashOnDeliveryEnabled = true,
                BankTransferEnabled = true, LocalPickupEnabled = false,
                BankTransferInstructions = "Please send a photo or PDF of your transfer receipt from your order page so we can verify it quickly.",
                ReturnsSummary = "Because every piece is unique and described in detail, please read the condition report carefully. See our Return Policy for details."
            });

        foreach (var stage in new[] { ContentStage.Draft, ContentStage.Published })
        {
            if (!await db.StoreSettings.AnyAsync(s => s.Stage == stage, ct)) db.StoreSettings.Add(DefaultStore(stage));
            if (!await db.ThemeSettings.AnyAsync(s => s.Stage == stage, ct)) db.ThemeSettings.Add(new ThemeSettings { Stage = stage });
            if (!await db.NavigationMenus.AnyAsync(s => s.Stage == stage, ct)) db.NavigationMenus.AddRange(DefaultMenus(stage));
            if (!await db.HomepageSections.AnyAsync(s => s.Stage == stage, ct)) db.HomepageSections.AddRange(DefaultSections(stage));
        }

        foreach (var page in DefaultPages())
            if (!await db.ContentPages.AnyAsync(p => p.Slug == page.Slug, ct)) db.ContentPages.Add(page);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Essential seed data verified");
    }

    private static StoreSettings DefaultStore(ContentStage stage) => new()
    {
        Stage = stage,
        StoreName = "Antique Heritage Store",
        Tagline = "Timeless pieces. Beautiful stories.",
        Description = "A curated boutique of antique furniture, vintage décor and rare collectibles, selected piece by piece.",
        LogoUrl = null,
        ContactEmail = null,
        ContactPhone = null,
        AnnouncementEnabled = true,
        AnnouncementText = "Discover timeless antique treasures — each piece one of a kind",
        AnnouncementLink = "/shop",
        FooterDescription = "Carefully selected antiques and vintage treasures that bring character to your home.",
        CopyrightText = "Antique Heritage Store. All rights reserved.",
        SocialLinksJson = "[]",
        NewsletterEnabled = true,
        SeoDefaultTitle = "Antique Heritage Store — Antiques & Vintage Treasures",
        SeoDefaultDescription = "Shop one-of-a-kind antique furniture, vintage clocks, art, lighting and collectibles with detailed condition reports."
    };

    private static IEnumerable<NavigationMenu> DefaultMenus(ContentStage stage)
    {
        NavigationMenu Menu(string location, string title, params NavItem[] items) =>
            new() { Stage = stage, Location = location, Title = title, ItemsJson = JsonSerializer.Serialize(items, Json) };

        yield return Menu(NavigationLocations.Main, "Main menu",
            new NavItem("Home", "/"), new NavItem("Shop", "/shop"), new NavItem("Collections", "/collections"),
            new NavItem("New Arrivals", "/shop?sort=Newest"), new NavItem("Our Story", "/pages/about"), new NavItem("Contact", "/contact"));
        yield return Menu(NavigationLocations.FooterShop, "Shop",
            new NavItem("All antiques", "/shop"), new NavItem("Collections", "/collections"),
            new NavItem("New arrivals", "/shop?sort=Newest"), new NavItem("Recently sold", "/shop?availability=Sold"));
        yield return Menu(NavigationLocations.FooterHelp, "Help",
            new NavItem("Contact us", "/contact"), new NavItem("Shipping policy", "/pages/shipping-policy"),
            new NavItem("Return policy", "/pages/return-policy"), new NavItem("Privacy policy", "/pages/privacy-policy"),
            new NavItem("Terms & conditions", "/pages/terms"));
    }

    private static IEnumerable<HomepageSection> DefaultSections(ContentStage stage)
    {
        var order = 0;
        HomepageSection S(string key, string type, object config) => new()
        {
            Stage = stage, SectionKey = key, Type = type, SortOrder = order++, IsVisible = true,
            ConfigJson = JsonSerializer.Serialize(config, config.GetType(), Json)
        };

        yield return S("hero", SectionTypes.Hero, new HeroConfig(
            "/demo-photos/hero-landscape.jpg", "Timeless Pieces. Beautiful Stories.",
            "Discover carefully selected antiques and vintage treasures that bring character to your home.",
            "Antiques · Vintage · Collectibles",
            new CtaLink("Explore Collection", "/shop"), new CtaLink("Our Story", "/pages/about"), "left", "large", 35));
        yield return S("categories", SectionTypes.FeaturedCategories, new FeaturedCategoriesConfig(
            "Shop by Category", "From statement furniture to small treasures for the shelf.", null, 8));
        yield return S("featured", SectionTypes.FeaturedProducts, new FeaturedProductsConfig(
            "Featured Pieces", "Hand-picked highlights from the collection.", null, 8, new CtaLink("View all", "/shop")));
        yield return S("trust", SectionTypes.TrustFeatures, new TrustFeaturesConfig(null,
        [
            new TrustItem("gem", "Carefully Selected Pieces", "Every item is chosen individually for its character and craftsmanship."),
            new TrustItem("camera", "Detailed Item Photography", "Multiple photographs and an honest condition report for each piece."),
            new TrustItem("shield", "Secure Shopping", "Your account is protected and we never store card details."),
            new TrustItem("truck", "Delivery Information", "Delivery options and fees are shown clearly before you order.")
        ]));
        yield return S("new-arrivals", SectionTypes.NewArrivals, new NewArrivalsConfig(
            "New Arrivals", "Recently added to the boutique.", 8, new CtaLink("Shop new arrivals", "/shop?sort=Newest")));
        yield return S("story", SectionTypes.ImageText, new ImageTextConfig(
            "/demo-photos/french-mantel-clock.jpg", "Our Story", "A home for objects with a past",
            "We believe the best homes are filled with things that have lived a little. Each piece in our boutique is selected for its craftsmanship and character, photographed carefully and described honestly — including its imperfections.\n\nEdit this section in Admin → Homepage Builder to tell your own story.",
            new CtaLink("Read our story", "/pages/about"), "left", "surface"));
        yield return S("rare-finds", SectionTypes.CollectionProducts, new CollectionProductsConfig(
            "Rare Finds", "Limited and unusual pieces you are unlikely to see twice.", null, 4, new CtaLink("See the collection", "/collections/rare-finds")));
        yield return S("eras", SectionTypes.EraShowcase, new EraShowcaseConfig("Shop by Era", "Explore pieces by period and style.",
        [
            new EraItem("Rococo", "/demo-photos/french-commode.jpg", "/shop?era=Rococo"),
            new EraItem("Georgian", "/demo-photos/english-bracket-clock.jpg", "/shop?era=Georgian"),
            new EraItem("Victorian", "/demo-photos/victorian-armchair.jpg", "/shop?era=Victorian"),
            new EraItem("Art Nouveau", "/demo-photos/leaded-glass-table-lamp.jpg", "/shop?era=Art%20Nouveau"),
            new EraItem("Colonial", "/demo-photos/betel-nut-container.jpg", "/shop?era=Colonial")
        ]));
        yield return S("testimonials", SectionTypes.Testimonials, new TestimonialsConfig("Kind Words", "From customers who found something special.", 6));
        yield return S("newsletter", SectionTypes.Newsletter, new NewsletterConfig(
            "Be the first to know", "New pieces arrive in small numbers. Join our list to hear about them first.", "Subscribe"));
    }

    private static IEnumerable<ContentPage> DefaultPages()
    {
        ContentPage Page(string slug, string title, string body) => new()
        {
            Slug = slug, Title = title, Body = body, DraftTitle = title, DraftBody = body, IsPublished = true, IsSystem = true,
            PublishedAt = DateTime.UtcNow
        };
        const string note = "<p><em>This is starter text. Please review and replace it with your store's own policy in Admin → Content Pages before launch.</em></p>";

        yield return Page("about", "Our Story",
            "<h2>A home for objects with a past</h2><p>Antique Heritage Store is a small, independent boutique dedicated to antique and vintage pieces with real character. Every item is chosen individually, photographed carefully and described honestly — including its signs of age.</p><h3>How we describe our pieces</h3><p>Each listing includes a condition report covering wear, scratches, damage, restoration and missing parts. Where we hold documentation about an item's history we say so; where we don't, we never guess.</p>" + note);
        yield return Page("contact", "Contact Us",
            "<p>We're happy to answer questions about any piece — measurements, extra photographs, condition or delivery. Use the form on this page or the contact details below.</p>");
        yield return Page("shipping-policy", "Shipping Policy",
            "<h2>Delivery</h2><p>Delivery fees are calculated at checkout based on your district. Large or fragile items may be available for local pickup only; this is shown on the product page.</p><h2>Delivery times</h2><p>We will contact you to arrange delivery after your order is confirmed. Times vary by location and item size.</p>" + note);
        yield return Page("return-policy", "Return Policy",
            "<h2>Unique, pre-owned items</h2><p>All our items are antique, vintage or pre-owned and are sold as described in their condition reports. Please read each report carefully and contact us with any questions before purchasing.</p><h2>If something isn't right</h2><p>If an item arrives significantly different from its description, contact us within 48 hours of delivery with photographs so we can make it right.</p>" + note);
        yield return Page("privacy-policy", "Privacy Policy",
            "<h2>What we collect</h2><p>We collect the information needed to process your orders: your name, contact details and delivery address. Sign-in is handled by Firebase Authentication; we never see or store your password. We do not store payment card details.</p><h2>How we use it</h2><p>Your information is used only to fulfil orders, respond to enquiries and, if you subscribe, send our newsletter.</p>" + note);
        yield return Page("terms", "Terms & Conditions",
            "<h2>Orders</h2><p>An order is confirmed once we accept it. Prices are shown in Sri Lankan Rupees (LKR). Because most items are one of a kind, availability is confirmed at the moment your order is placed.</p><h2>Payment</h2><p>We accept cash on delivery (where available) and bank transfer. Bank transfer orders are confirmed after we verify your payment.</p>" + note);
    }

    // ---------------- Example catalog (development only) ----------------

    public async Task SeedExampleCatalogAsync(CancellationToken ct)
    {
        if (await db.Products.AnyAsync(ct))
        {
            logger.LogInformation("Products already exist; example catalog not seeded");
            return;
        }

        // Real photographs (CC0, Cleveland Museum of Art Open Access) used as ILLUSTRATIVE example photos.
        // They show museum pieces, not stock you own: replace them with photos of your actual items before selling.
        var categories = new (string Name, string Slug, string Image, string Description)[]
        {
            ("Antique Furniture", "antique-furniture", "victorian-armchair", "Chairs, commodes, servers and tables with generations of character."),
            ("Vintage Clocks", "vintage-clocks", "english-bracket-clock", "Mantel, bracket and table clocks from the 18th and 19th centuries."),
            ("Paintings & Art", "paintings-art", "autumn-mountain-painting", "Original paintings, prints and decorative art."),
            ("Home Decor", "home-decor", "jacquard-coverlet", "Textiles, glass and decorative objects that bring warmth to any room."),
            ("Rare Collectibles", "rare-collectibles", "lacquer-writing-box", "Unusual finds for the discerning collector."),
            ("Vintage Lighting", "vintage-lighting", "leaded-glass-table-lamp", "Lamps and fixtures with period charm."),
            ("Ceramics & Pottery", "ceramics-pottery", "porcelain-bird-flower-vase", "Porcelain, stoneware and earthenware pieces."),
            ("Mirrors & Frames", "mirrors-frames", "dressing-table-mirror", "Carved, gilded and metal frames and mirrors."),
            ("Antique Jewelry", "antique-jewelry", "pendant-brooch", "Antique jewellery and accessories."),
            ("Other Treasures", "other-treasures", "betel-nut-container", "Everything else that caught our eye.")
        };
        var catEntities = categories.Select((c, i) => new Category
        {
            Name = c.Name, Slug = c.Slug, Description = c.Description, ImageUrl = $"/demo-photos/{c.Image}.jpg",
            SortOrder = i, IsVisible = true, IsFeatured = i < 8
        }).ToList();
        db.Categories.AddRange(catEntities);

        var rare = new Collection { Name = "Rare Finds", Slug = "rare-finds", Description = "Limited and unusual pieces you are unlikely to see twice.", SortOrder = 0, IsVisible = true, ImageUrl = "/demo-photos/lacquer-writing-box.jpg" };
        var furniture = new Collection { Name = "Period Furniture", Slug = "period-furniture", Description = "Statement furniture from the 18th and 19th centuries.", SortOrder = 1, IsVisible = true, ImageUrl = "/demo-photos/french-commode.jpg" };
        db.Collections.AddRange(rare, furniture);
        await db.SaveChangesAsync(ct);

        Category Cat(string slug) => catEntities.First(c => c.Slug == slug);
        var now = DateTime.UtcNow;
        var examples = new List<Product>
        {
            Example(1, "Lacquered Chinoiserie Commode", Cat("antique-furniture"), 1250000m, null, ProductCondition.VeryGood, "Rococo", "Lacquered wood, gilt bronze, marble", "french-commode",
                "A serpentine two-drawer commode in black lacquer with gilded chinoiserie scenes, gilt-bronze mounts and a variegated marble top.",
                "Lacquer surfaces retain strong gilded decoration; mounts are bright with light wear to the high points.",
                wear: "Light rubbing to the gilding on the mounts and drawer edges.", scratches: "Fine surface scratches to the lacquer, mainly on the top drawer.",
                dims: "W 130 × D 63 × H 87 cm (approx.)", age: "Mid 18th century style", featured: true, delivery: false, collections: [rare, furniture]),
            Example(2, "Gilt Bracket Clock with Agate Panels", Cat("vintage-clocks"), 485000m, null, ProductCondition.VeryGood, "Georgian", "Gilt metal, agate, enamel", "english-bracket-clock",
                "An elaborate gilt cabinet clock with an enamel dial above doors set with agate panels, opening to a fitted interior.",
                "A showpiece clock; the doors open to reveal agate-faced drawers.", wear: "Gilding softened on the feet and door edges.",
                functional: "Movement not tested; sold as decorative. A clock specialist should service it before use.",
                dims: "W 32 × D 18 × H 52 cm (approx.)", age: "Late 18th century style", featured: true, collections: [rare]),
            Example(3, "Marble and Gilt-Bronze Mantel Clock", Cat("vintage-clocks"), 395000m, 425000m, ProductCondition.Good, "Neoclassical", "White marble, gilt bronze, enamel", "french-mantel-clock",
                "A Neoclassical mantel clock in white and black marble with gilt-bronze figures, lions and an eagle finial.",
                "Enamel dial with Arabic numerals; decorative mounts complete.", wear: "Light discolouration to the marble base.",
                damage: "Small chip to one rear corner of the marble plinth.", functional: "Not running at the time of listing.",
                dims: "W 38 × D 13 × H 55 cm (approx.)", age: "Late 18th century style", featured: true),
            Example(4, "Peacock Leaded-Glass Table Lamp", Cat("vintage-lighting"), 950000m, null, ProductCondition.Excellent, "Art Nouveau", "Leaded glass, patinated bronze", "leaded-glass-table-lamp",
                "A domed leaded-glass shade in a peacock-feather design on a hammered bronze base with feather-shaped feet.",
                "Vibrant glass throughout; the shade lifts off to reveal the original oil font.", wear: "Even patina to the bronze base.",
                functional: "Originally an oil lamp and not electrified.", dims: "Shade Ø 56 cm; H 66 cm (approx.)", age: "Early 20th century style", featured: true),
            Example(5, "Bronze Swivel Reading Lamp", Cat("vintage-lighting"), 310000m, null, ProductCondition.Good, "Art Nouveau", "Bronze, iridescent glass", "bronze-reading-lamp",
                "A desk lamp with an iridescent green glass shade that pivots on a curved bronze arm above a jewelled base.",
                "Shade swivels freely; the base is set with glass cabochons.", wear: "Patina wear on the arm where it is usually handled.",
                restoration: "Rewired at some point (date unknown).", dims: "H 36 cm (approx.)", age: "Circa 1900 style"),
            Example(6, "Blue-and-White Porcelain Jar", Cat("ceramics-pottery"), 275000m, null, ProductCondition.VeryGood, "Joseon", "Porcelain", "porcelain-bird-flower-vase",
                "A shouldered porcelain jar painted in cobalt blue with a bird among flowering branches.",
                "Soft milky glaze with fluid brushwork.", wear: "Minor glaze wear around the foot rim.",
                scratches: "Light surface marks consistent with age.", dims: "H 33 cm (approx.)", age: "Late 19th century style"),
            Example(7, "White Porcelain Potpourri Vase with Cover", Cat("ceramics-pottery"), 165000m, null, ProductCondition.Good, "Rococo", "Soft-paste porcelain", "porcelain-potpourri-vase",
                "A pierced white porcelain potpourri vase and cover, modelled with applied blossoms and a small figure at the base.",
                "Crisp moulding; cover fits well.", damage: "Tiny losses to two applied petals.",
                dims: "H 22 cm (approx.)", age: "Mid 18th century style"),
            Example(8, "Art Nouveau Bronze Dressing-Table Mirror", Cat("mirrors-frames"), 145000m, null, ProductCondition.Good, "Art Nouveau", "Bronze, enamel, mirror glass", "dressing-table-mirror",
                "A round easel mirror in a bronze frame with an enamelled border, supported on sinuous feet.",
                "Original mirror plate.", wear: "Light silvering loss near the edge of the mirror plate.",
                dims: "Ø 30 cm; H 34 cm (approx.)", age: "Circa 1900 style"),
            Example(9, "Gold Lacquer Writing Box", Cat("rare-collectibles"), 420000m, null, ProductCondition.Fair, "Edo Period", "Lacquered wood, gold", "lacquer-writing-box",
                "A square black lacquer writing box decorated in gold with pine, camellia and bamboo, fitted inside with an inkstone and water dropper.",
                "Complete with its inkstone; gold decoration remains rich.", wear: "Rubbing to the lacquer along the lid edges.",
                damage: "Fine age cracks in the lacquer on the base.", dims: "W 22 × D 24 × H 5 cm (approx.)", age: "Antique (age unverified)", featured: true, collections: [rare]),
            Example(10, "Oil Painting — Autumn in the Mountains", Cat("paintings-art"), 680000m, null, ProductCondition.Good, "Victorian", "Oil on canvas, giltwood frame", "autumn-mountain-painting",
                "A luminous autumn landscape of distant mountains above golden woodland, in a deep gilt frame.",
                "Rich colour; presented in a period-style gilt frame.", wear: "Fine craquelure across the sky consistent with age.",
                damage: "Minor chips to the frame corners.", dims: "Canvas 76 × 51 cm (approx.)", age: "19th century style"),
            Example(11, "Marble-Top Mahogany Server", Cat("antique-furniture"), 540000m, null, ProductCondition.Fair, "Victorian", "Mahogany, marble", "mahogany-sideboard",
                "A substantial mahogany server with a white marble top, frieze drawer and carved supports joined by a stretcher.",
                "Solid construction; marble top is original to the piece.", wear: "Faded finish to the top rails.",
                scratches: "Scratches and marks to the base consistent with use.", damage: "A small repaired break to one back corner of the marble.",
                dims: "W 120 × D 55 × H 92 cm (approx.)", age: "Mid 19th century style", delivery: false, collections: [furniture]),
            Example(12, "Pendant Brooch with Pink Stones", Cat("antique-jewelry"), 225000m, null, ProductCondition.Excellent, "Victorian", "Gold, silver, pink stones (untested)", "pendant-brooch",
                "A pierced openwork brooch set with an oval pink stone, suspending a detachable pear-shaped pink drop.",
                "Stones are untested and sold as described; metal is unmarked.", wear: "Light surface wear to the setting.",
                dims: "H 7 cm including drop (approx.)", age: "Late 19th / early 20th century style"),
            Example(13, "Hand-Woven Blue and White Coverlet", Cat("home-decor"), 98000m, null, ProductCondition.Good, "19th Century", "Wool, cotton", "jacquard-coverlet",
                "A double-weave coverlet in indigo and cream with a repeating floral medallion pattern and patterned borders.",
                "Bold, even colour across the field.", wear: "Slight fading along one fold line.",
                damage: "Two small professionally darned areas near the border.", dims: "200 × 190 cm (approx.)", age: "19th century style"),
            Example(14, "Aqua Blown-Glass Tumbler", Cat("home-decor"), 18500m, null, ProductCondition.VeryGood, "19th Century", "Blown glass", "pressed-glass-tumbler",
                "A pale aqua tumbler in hand-blown glass with gentle optic ribbing and a pontil mark beneath.",
                "Pleasing seed bubbles typical of early glass.", scratches: "Light wear scratches to the base.",
                dims: "Ø 8 × H 9 cm (approx.)", age: "First half of the 19th century style"),
            Example(15, "Beaded Brass Betel Container", Cat("other-treasures"), 72000m, null, ProductCondition.Good, "Colonial", "Brass, glass beads", "betel-nut-container",
                "A rectangular brass container with a hinged lid and fitted interior, carried on a colourful glass-bead strap.",
                "Bead strap complete and supple.", wear: "Dark patina and small dents to the brass.",
                missing: "A few beads missing near one end of the strap.", dims: "W 18 × D 8 × H 7 cm (approx.)", age: "19th century style"),
            Example(16, "Rococo Silver-Gilt Tea Caddy", Cat("rare-collectibles"), 365000m, null, ProductCondition.VeryGood, "Georgian", "Silver-gilt", "silver-tea-caddy",
                "A chased silver-gilt tea caddy with rococo scrolls, shells and flowers and a bud finial on the cover.",
                "Crisp chasing; cover fits closely.", wear: "Gilding thinned on the high points.",
                dims: "H 14 cm (approx.)", age: "Mid 18th century style", collections: [rare]),
            Example(17, "Gilded Armchair in Green Velvet", Cat("antique-furniture"), 330000m, null, ProductCondition.Restored, "Victorian", "Giltwood, marquetry, velvet", "victorian-armchair",
                "A gilded armchair with inlaid rails and turned legs, upholstered in green velvet.",
                "Comfortable and sturdy; a decorative accent chair.", restoration: "Re-upholstered in new velvet.",
                wear: "Gilding worn on the arm terminals.", dims: "W 64 × D 66 × H 86 cm (approx.)", age: "Late 19th century style", featured: true, collections: [furniture])
        };

        foreach (var p in examples) p.PublishedAt = now.AddDays(-examples.IndexOf(p));
        db.Products.AddRange(examples);
        await db.SaveChangesAsync(ct);

        foreach (var p in examples.Where(p => p.QuantityAvailable > 0))
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = p.Id, Type = InventoryMovementType.InitialStock, AvailableChange = p.QuantityAvailable,
                Reason = "Example seed data", CreatedAt = now
            });

        // Example delivery rates so checkout works in development. Rate names say "example" so they're never mistaken for real quotes.
        if (!await db.ShippingZones.AnyAsync(ct))
        {
            db.ShippingZones.Add(new ShippingZone
            {
                Name = "Colombo & Gampaha", SortOrder = 0, CashOnDeliveryAvailable = true,
                Districts = [new() { District = "Colombo" }, new() { District = "Gampaha" }],
                Rates = [new() { Name = "Standard delivery (example rate)", Fee = 1500m }]
            });
            db.ShippingZones.Add(new ShippingZone
            {
                Name = "Rest of Sri Lanka", SortOrder = 1, CashOnDeliveryAvailable = false,
                Districts = Domain.Rules.SriLanka.Districts.Where(d => d is not ("Colombo" or "Gampaha")).Select(d => new ShippingZoneDistrict { District = d }).ToList(),
                Rates = [new() { Name = "Standard delivery (example rate)", Fee = 3500m }]
            });
        }

        // Point the "Rare Finds" homepage section at the example collection.
        foreach (var section in await db.HomepageSections.Where(s => s.Type == SectionTypes.CollectionProducts).ToListAsync(ct))
        {
            var config = JsonSerializer.Deserialize<CollectionProductsConfig>(section.ConfigJson, Json)!;
            if (config.CollectionId is null)
                section.ConfigJson = JsonSerializer.Serialize(config with { CollectionId = rare.Id }, Json);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} EXAMPLE products (development data)", examples.Count);
    }

    private static Product Example(int n, string name, Category category, decimal price, decimal? compareAt, ProductCondition condition,
        string era, string material, string photo, string shortDescription, string notes,
        string? wear = null, string? scratches = null, string? damage = null, string? restoration = null, string? missing = null,
        string? functional = null, string? dims = null, string? age = null, bool featured = false, bool delivery = true,
        Collection[]? collections = null, int quantity = 1)
    {
        var slug = AntiqueStore.Application.Common.Text.Slugify(name);
        return new Product
        {
            Name = name, Slug = slug, Sku = $"EXAMPLE-{n:000}", CategoryId = category.Id, Price = price, CompareAtPrice = compareAt,
            ShortDescription = shortDescription,
            Description = $"<p>{shortDescription}</p><p>{notes}</p><p><em>Example listing created by the development seed. Photo: Cleveland Museum of Art (CC0), shown for illustration only. Replace it with your own products and photos.</em></p>",
            Condition = condition, ConditionNotes = notes, SignsOfWear = wear, Scratches = scratches, Damage = damage,
            RestorationHistory = restoration, MissingParts = missing, FunctionalStatus = functional, Material = material,
            Dimensions = dims, Era = era, ApproximateAge = age, QuantityAvailable = quantity, MaxPerOrder = 1,
            Status = ProductStatus.Published, IsFeatured = featured, IsDeliveryAvailable = delivery, ShowWhenSold = true,
            Images = DemoImages(photo, name),
            Collections = (collections ?? []).Select(c => new ProductCollection { CollectionId = c.Id }).ToList()
        };
    }
    /// <summary>Pixel sizes of the bundled example photos (frontend/public/demo-photos).</summary>
    private static readonly Dictionary<string, (int W, int H)> DemoPhotoSizes = new()
    {
        ["autumn-mountain-painting"] = (1500, 971),
        ["autumn-mountain-painting-2"] = (1500, 1089),
        ["betel-nut-container"] = (1500, 1166),
        ["betel-nut-container-2"] = (1500, 1196),
        ["bronze-reading-lamp"] = (1240, 1500),
        ["dressing-table-mirror"] = (1500, 1307),
        ["dressing-table-mirror-2"] = (1500, 1318),
        ["english-bracket-clock"] = (1349, 1500),
        ["english-bracket-clock-2"] = (1456, 1500),
        ["french-commode"] = (1500, 1268),
        ["french-commode-2"] = (1500, 1193),
        ["french-mantel-clock"] = (1123, 1500),
        ["french-mantel-clock-2"] = (705, 1500),
        ["jacquard-coverlet"] = (1500, 1426),
        ["jacquard-coverlet-2"] = (1500, 1495),
        ["lacquer-writing-box"] = (1500, 1234),
        ["lacquer-writing-box-2"] = (1500, 809),
        ["leaded-glass-table-lamp"] = (1146, 1500),
        ["leaded-glass-table-lamp-2"] = (1275, 1500),
        ["mahogany-sideboard"] = (1500, 960),
        ["pendant-brooch"] = (1109, 1500),
        ["pendant-brooch-2"] = (1183, 1500),
        ["porcelain-bird-flower-vase"] = (1216, 1500),
        ["porcelain-bird-flower-vase-2"] = (1220, 1500),
        ["porcelain-potpourri-vase"] = (1500, 1378),
        ["porcelain-potpourri-vase-2"] = (1500, 1375),
        ["pressed-glass-tumbler"] = (1416, 1500),
        ["silver-tea-caddy"] = (1500, 1376),
        ["silver-tea-caddy-2"] = (1159, 1500),
        ["victorian-armchair"] = (1184, 1500)
    };

    private static List<ProductImage> DemoImages(string photo, string name)
    {
        var images = new List<ProductImage>();
        foreach (var (file, alt, order) in new[] { (photo, name, 0), ($"{photo}-2", $"{name} — another view", 1) })
        {
            if (!DemoPhotoSizes.TryGetValue(file, out var size)) continue;
            images.Add(new ProductImage { Url = $"/demo-photos/{file}.jpg", AltText = $"{alt} (example photo)", SortOrder = order, Width = size.W, Height = size.H });
        }
        return images;
    }
}
