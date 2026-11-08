namespace Geeratya.FakeJira;

/// <summary>
/// The invented demo dataset (<c>fakejira --demo</c>): Wayfinder, a trip planning app, as one team's Scrum project.
/// Every name, text and number is made up; nothing comes from a real site.
/// </summary>
public static class DemoDataset
{
    public const string ProjectKey = "WAY";

    public static readonly DemoPerson[] People =
    [
        new("demo-priya", "Priya Raman"),
        new("demo-tomasz", "Tomasz Wieczorek"),
        new("demo-hannah", "Hannah Okafor"),
        new("demo-diego", "Diego Marquez"),
        new("demo-lena", "Lena Fischer"),
        new("demo-sam", "Sam Whitaker"),
        new("demo-mei", "Mei Tanaka"),
    ];

    private const int Priya = 0;
    private const int Tomasz = 1;
    private const int Hannah = 2;
    private const int Diego = 3;
    private const int Lena = 4;
    private const int Sam = 5;
    private const int Mei = 6;

    public static List<DemoIssue> Issues()
    {
        var now = DateTimeOffset.UtcNow;
        var issues = new List<DemoIssue>();
        issues.AddRange(Epics());
        issues.AddRange(ClosedSprint());
        issues.AddRange(ActiveSprint());
        issues.AddRange(FutureSprintAndBacklog());

        foreach (var issue in issues)
        {
            issue.Updated = issue.Comments.Count > 0
                ? now.AddHours(-issue.Comments.Min(comment => comment.HoursAgo))
                : now.AddDays(-Math.Max(0.2, issue.CreatedDaysAgo / 4));
        }

        return issues;
    }

    private static DemoIssue Epic(int number, string summary, string priority, int owner, string description)
    {
        return new DemoIssue
        {
            Number = number,
            Type = "Epic",
            Summary = summary,
            Status = "In Progress",
            Priority = priority,
            Assignee = owner,
            Reporter = Mei,
            CreatedDaysAgo = 90,
            Description = DemoAdf.Document(description),
        };
    }

    private static DemoIssue Make(
        int number,
        string type,
        string summary,
        string status,
        string priority,
        int assignee,
        int points,
        int epic,
        int sprint,
        string[] labels,
        string description,
        double createdDaysAgo = 14)
    {
        return new DemoIssue
        {
            Number = number,
            Type = type,
            Summary = summary,
            Status = status,
            Priority = priority,
            Assignee = assignee,
            Reporter = type == "Bug" ? Sam : Mei,
            Points = points == 0 ? null : points,
            Epic = epic,
            Sprint = sprint,
            Labels = [.. labels],
            CreatedDaysAgo = createdDaysAgo,
            Description = DemoAdf.Document(description),
        };
    }

    private static List<DemoIssue> Epics()
    {
        return
        [
            Epic(
                1,
                "Offline maps",
                "High",
                Diego,
                "Travellers lose signal exactly where they need the map. Let people download regions before a trip and keep every itinerary map usable without a connection.\n\n## Success looks like\n\n- 80% of trips with a downloaded region show the map without any network call\n- Download size stays under 150 MB for a typical city region"),
            Epic(
                2,
                "Trip sharing",
                "Medium",
                Hannah,
                "Groups plan together. Companions should join a trip with one link, see the same itinerary and vote on what to do.\n\n## Out of scope for now\n\n- Chat inside the app\n- Splitting expenses (tracked separately)"),
            Epic(
                3,
                "Import bookings from email",
                "High",
                Tomasz,
                "Forward a confirmation email to your Wayfinder address and the booking lands in the right trip. Flights first, then hotels and trains."),
            Epic(
                4,
                "Performance and accessibility",
                "Medium",
                Priya,
                "Make the app fast on the phones our users actually own and usable with a screen reader and large fonts."),
        ];
    }

    private static List<DemoIssue> ClosedSprint()
    {
        return
        [
            Make(5, "Story", "Pick a map region to download by dragging a bounding box", "Done", "Medium", Diego, 5, 1, 14, ["android", "ios", "offline"], "People choose the area to save by dragging a rectangle over the map, with the estimated size shown live.", 40),
            Make(6, "Story", "Create a shared trip from an existing itinerary", "Done", "Medium", Hannah, 5, 2, 14, ["ios", "sharing"], "A trip owner can turn a private itinerary into a shared trip with a single tap.", 40),
            Make(7, "Task", "Set up the inbound email address and webhook for bookings", "Done", "High", Tomasz, 3, 3, 14, ["backend", "email"], "Provision the `trips@in.wayfinder.example` mailbox and forward every message to the parser webhook.", 38),
            Make(8, "Bug", "Map zoom level resets after rotating the phone", "Done", "Medium", Diego, 2, 1, 14, ["android", "regression"], "Rotating the device recreates the map fragment and the camera position is lost.", 35),
            Make(9, "Story", "Cold start under two seconds on mid-range phones", "Done", "High", Priya, 5, 4, 14, ["performance", "android"], "Measured on a 2021 mid-range device the app needs 3.4 s before the first itinerary is visible. Target is 2 s at p75.", 36),
            Make(10, "Task", "Move CI builds to GitHub Actions", "Done", "Low", Diego, 3, 0, 14, ["infra"], "Replace the self-hosted runner with hosted runners and cache the Gradle and Pods directories.", 33),
            Make(11, "Story", "Show trip members with their roles", "Done", "Low", Hannah, 2, 2, 14, ["ios", "sharing"], "List every companion with a role badge (owner, editor, viewer).", 33),
        ];
    }

    private static List<DemoIssue> ActiveSprint()
    {
        var issues = new List<DemoIssue>
        {
            Make(12, "Story", "Download map regions for offline use", "In Progress", "High", Diego, 8, 1, 15, ["android", "offline"],
                "Users can download the region they picked so the itinerary map keeps working without a connection.\n\n## Acceptance criteria\n\n- A download can be started, paused and resumed, also after the app was killed\n- Progress is shown as percent and megabytes\n- Downloads continue on mobile data only after an explicit confirmation\n- A finished region is listed in Settings with its size and date\n\n## Notes\n\nTiles come from the vector tile endpoint, packaged per region by the backend job. Search over downloaded regions is tracked in WAY-30.", 18),
            Make(13, "Story", "Show storage used by offline maps in Settings", "To Do", "Medium", Diego, 3, 1, 15, ["android", "offline"],
                "Add an Offline maps screen under Settings with the total size and one row per region.\n\n## Acceptance criteria\n\n- Total and per-region size use the device language for units\n- Swipe a row to remove a region, with a confirmation", 12),
            Make(14, "Task", "Evict least recently used regions when device storage is low", "To Do", "Medium", Hannah, 5, 1, 15, ["ios", "offline"],
                "When free space drops below 500 MB, offer to remove the regions that were not opened for the longest time. Never offer a region of a trip that starts within 14 days.", 10),
            Make(15, "Bug", "Offline map tiles render blank after an app update", "In Progress", "Highest", Priya, 3, 1, 15, ["regression", "ios", "offline"],
                "After updating from 4.7 to 4.8 the previously downloaded regions show grey tiles until the user deletes and downloads them again.\n\n## Steps to reproduce\n\n1. Install 4.7 and download the Lisbon region\n2. Update to 4.8 from TestFlight\n3. Open any Lisbon itinerary in airplane mode\n\n## Expected\n\nThe saved tiles are shown.\n\n## Actual\n\nGrey tiles and the label \"Map unavailable\".", 6),
            Make(16, "Bug", "Crash when opening a shared trip link with an expired token", "In Review", "High", Hannah, 2, 2, 15, ["android", "crash", "sharing"],
                "Opening an invite link whose token expired crashes the app instead of showing the \"link expired\" screen. Reported by 14 users in the last release, 0.4% of sessions.\n\n## Acceptance criteria\n\n- An expired or revoked token shows the Link expired screen with a way to request a new link\n- No crash for a malformed token either\n- A regression test covers the resolver", 5),
            Make(17, "Story", "Invite travel companions with a link", "Done", "Medium", Hannah, 5, 2, 15, ["ios", "android", "sharing"],
                "The owner generates an invite link that adds the person as editor or viewer. Links expire after 7 days.", 16),
            Make(18, "Story", "Let companions vote on activities", "In Review", "Medium", Priya, 5, 2, 15, ["ios", "sharing"],
                "Each activity in a shared trip gets thumbs up and down. Votes are visible per person and summed on the card.\n\n## Acceptance criteria\n\n- One vote per person and activity, changeable at any time\n- The itinerary can be sorted by score\n- Votes sync to every companion within 5 seconds", 11),
            Make(19, "Task", "Rate limit invite link creation per account", "To Do", "Low", Tomasz, 2, 2, 15, ["backend", "security"],
                "Allow at most 20 invite links per hour per account and answer 429 with a Retry-After header.", 9),
            Make(20, "Story", "Parse flight confirmations from forwarded emails", "In Progress", "High", Tomasz, 8, 3, 15, ["backend", "email"],
                "Extract carrier, flight number, airports, local departure and arrival times and the booking reference from a forwarded confirmation, and attach the flight to the matching trip.\n\n## Acceptance criteria\n\n- Works for plain text and HTML emails of the 12 airlines in the sample set\n- Times keep the airport's local time zone\n- Unknown formats end up in a review queue, never silently dropped\n- Parser runs in under 300 ms per email at p95", 21),
            Make(21, "Task", "Collect anonymised sample confirmation emails for parser tests", "Done", "Medium", Sam, 2, 3, 15, ["email", "testing"],
                "Gather at least 5 emails per airline from teammates, strip names and booking references and commit them under `parser/testdata`.", 20),
            Make(22, "Bug", "Itinerary list janks while scrolling on low-end Android devices", "To Do", "Medium", Diego, 5, 4, 15, ["android", "performance"],
                "On a 2 GB RAM device the itinerary list drops to about 25 fps with more than 40 items. The profiler shows image decoding on the main thread.", 8),
            Make(23, "Task", "Add content descriptions to map markers for TalkBack", "In Progress", "Medium", Hannah, 3, 4, 15, ["accessibility", "android"],
                "Every marker announces its name, category and distance, for example \"Museu do Azulejo, museum, 1.2 km away\".", 9),
            Make(24, "Story", "Respect the system font scale on the itinerary screen", "In Review", "Low", Priya, 2, 4, 15, ["accessibility", "ios"],
                "Text on the itinerary cards is clipped at the largest accessibility font size. Cards should grow instead.", 7),
            Make(25, "Task", "Upgrade the Android Gradle plugin and Kotlin", "Done", "Low", Diego, 2, 0, 15, ["android", "infra"],
                "Move to the latest stable AGP and Kotlin, fix deprecation warnings.", 15),
        };

        AddActiveDetails(issues);

        return issues;
    }

    private static void AddActiveDetails(List<DemoIssue> issues)
    {
        var offline = Find(issues, 12);
        offline.Links.Add(new DemoLink("Blocks", "blocks", "is blocked by", 13));
        offline.Comments.Add(new DemoComment(Mei, 140, "Design is final, Lena's handoff is in the Figma file linked on the epic. Remember the confirmation on mobile data."));
        offline.Comments.Add(new DemoComment(Diego, 52, "Resume after process death works now. Still open: the progress notification shows 99% for a few seconds while tiles are verified."));
        offline.Comments.Add(new DemoComment(Tomasz, 20, "Backend packages Lisbon at 61 MB and Porto at 38 MB. Both pass the size target, the download endpoint supports `Range` requests."));

        var blank = Find(issues, 15);
        blank.Links.Add(new DemoLink("Relates", "relates to", "relates to", 12));
        blank.Comments.Add(new DemoComment(Sam, 70, "Reproduced on iPhone 12 (iOS 17.5) and iPhone SE. Not reproducible on a fresh install of 4.8, only after an update."));
        blank.Comments.Add(new DemoComment(Priya, 26, "Found it: 4.8 moved the tile store from `Caches` to `Application Support` and the migration only runs when the app launches online. Working on a fix plus a migration test."));

        var crash = Find(issues, 16);
        crash.Comments.Add(new DemoComment(Sam, 96, "Stack trace from the Play Console, Pixel 6, Android 14:\n\n```\njava.lang.NullPointerException: Attempt to invoke virtual method 'String ShareToken.getTripId()' on a null object reference\n    at com.wayfinder.share.SharedTripResolver.resolve(SharedTripResolver.kt:58)\n    at com.wayfinder.share.DeepLinkActivity.onCreate(DeepLinkActivity.kt:41)\n    at android.app.Activity.performCreate(Activity.java:8595)\n    at android.app.ActivityThread.performLaunchActivity(ActivityThread.java:3855)\n    at android.os.Looper.loop(Looper.java:288)\n```"));
        crash.Comments.Add(new DemoComment(Hannah, 30, "The API answers 410 for an expired token and our parser maps the empty body to `null`. Returning `LinkExpired` from `resolve` and covering 410, 404 and malformed tokens in tests."));
        crash.Comments.Add(new DemoComment(Priya, 3, "Reviewed. One nit about the copy on the new screen, otherwise good to go."));

        var vote = Find(issues, 18);
        vote.Comments.Add(new DemoComment(Lena, 60, "Thumbs look good on the card. Can we show who voted on long-press instead of a separate screen?"));
        vote.Comments.Add(new DemoComment(Priya, 28, "Done, long-press opens a small sheet with the voters. PR is up, please have a look."));

        var parser = Find(issues, 20);
        parser.Comments.Add(new DemoComment(Tomasz, 110, "First pass handles 7 of 12 airlines. The two low-cost carriers send times without a time zone, I will derive it from the airport code."));
        parser.Comments.Add(new DemoComment(Mei, 41, "Fine to ship with 10 of 12 airlines if the rest land in the review queue. Please keep the fallback visible in the admin view."));

        Find(issues, 22).Comments.Add(new DemoComment(Diego, 35, "Moving image decoding off the main thread gets us to 52 fps on the test device. Will also try a smaller thumbnail size from the backend."));
        Find(issues, 14).Comments.Add(new DemoComment(Mei, 15, "Product decision: ask the user first, never delete without a prompt."));
        Find(issues, 17).Comments.Add(new DemoComment(Hannah, 48, "Shipped to 10% of users yesterday, no errors in the invite funnel."));
    }

    private static DemoIssue Find(List<DemoIssue> issues, int number) => issues.Find(issue => issue.Number == number)!;

    private static List<DemoIssue> FutureSprintAndBacklog()
    {
        return
        [
            Make(26, "Story", "Parse hotel booking emails: check-in and check-out", "To Do", "Medium", -1, 5, 3, 16, ["backend", "email"], "Support the three most common booking sites. Dates are calendar dates without a time zone, the hotel's address decides the local time.", 6),
            Make(27, "Story", "Download offline maps on Wi-Fi only by default", "To Do", "Medium", -1, 3, 1, 16, ["ios", "android", "offline"], "A setting, on by default, that postpones large downloads until the phone is on Wi-Fi.", 6),
            Make(28, "Story", "Read-only public page for a trip", "To Do", "Low", -1, 8, 2, 16, ["web", "sharing"], "Owners can publish a read-only web page of the itinerary, without the private notes.", 5),
            Make(29, "Story", "Import train tickets from PDF attachments", "To Do", "Medium", -1, 8, 3, 0, ["backend", "email"], "Many rail operators send the ticket as a PDF. Extract train number, stations and times from the text layer.", 25),
            Make(30, "Bug", "Offline search ignores accented characters", "To Do", "Medium", -1, 2, 1, 0, ["android", "ios", "offline"], "Searching for \"Sao Paulo\" finds nothing, \"São Paulo\" does. Normalise both the index and the query.", 12),
            Make(31, "Story", "Dark theme for the itinerary and map screens", "To Do", "Low", -1, 5, 4, 0, ["design", "ios", "android"], "Follow the system setting. The map style needs a dark variant.", 30),
            Make(32, "Task", "Set up a crash-free sessions dashboard and alerts", "To Do", "High", -1, 3, 0, 0, ["infra", "quality"], "Alert in the team channel when crash-free sessions drop below 99.5% over one hour.", 19),
            Make(33, "Story", "Export a trip to the calendar as an .ics file", "To Do", "Low", -1, 3, 2, 0, ["ios", "android"], "One event per activity and per booking, with the map link in the description.", 28),
            Make(34, "Task", "Evaluate vector tiles against raster for download size", "To Do", "Medium", -1, 3, 1, 0, ["research", "offline"], "Compare 5 regions at zoom levels 10 to 16: size, render time on low-end devices, style flexibility. Write up the result in one page.", 27),
            Make(35, "Bug", "Trip cover photo is cropped badly on tablets", "To Do", "Low", -1, 1, 2, 0, ["ios", "android", "design"], "Landscape photos lose the top third on tablets in portrait.", 10),
            Make(36, "Task", "Remove the deprecated analytics SDK", "To Do", "Lowest", -1, 2, 0, 0, ["tech-debt"], "The old SDK stops receiving data at the end of the quarter. Replace the remaining three events with the new client.", 45),
        ];
    }
}
