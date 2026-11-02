using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Geeratya.Core.Adf;
using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.Tests.Adf;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Cache;

public sealed class IssueCacheTests : IDisposable
{
    private static readonly DateTimeOffset _saved = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly IssueStatus _status = new("3", "In Progress", "indeterminate");
    private static readonly IssueTypeSummary _type = new("10", "Story", 0, false);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"geeratya-issue-cache-{Guid.NewGuid():N}");

    [Fact]
    public void GivenIssueWithRichDescription_WhenReadByANewCache_ThenTheWholeFileRoundTripsAndRendersTheSame()
    {
        var detail = Detail("GTM-5", AdfTestSupport.FixtureDescription(AdfTestSupport.RichDescription));
        var folder = Path.Combine(_root, "p");
        using (var writer = new SiteCache(folder))
        {
            writer.PutIssue(new CachedIssue(detail, _saved));
            writer.Flush();
        }

        using var reader = new SiteCache(folder);
        var read = reader.GetIssue("GTM-5");

        Assert.NotNull(read);
        Assert.Equal(_saved, read.SavedAt);
        Assert.Equal(Json(new CachedIssue(detail, _saved)), Json(read));
        Assert.Equal(
            AdfTestSupport.PlainLines(AdfRenderer.Render(detail.Description!, 40)),
            AdfTestSupport.PlainLines(AdfRenderer.Render(read.Detail.Description!, 40)));
    }

    [Fact]
    public void GivenDocumentWithEveryNodeAndMarkKind_WhenMappedToDtoAndBack_ThenEveryAttributeSurvives()
    {
        AdfMark[] marks =
        [
            new AdfStrongMark(), new AdfEmphasisMark(), new AdfStrikeMark(), new AdfCodeMark(),
            new AdfUnderlineMark(), new AdfLinkMark("https://example.test/x"), new AdfTextColorMark("#ff0000"),
            new AdfSubSupMark("sup"), new AdfUnknownMark("fancy"),
        ];
        AdfNode[] content =
        [
            new AdfHeading(3, [new AdfText("title", marks)]),
            new AdfBulletList([new AdfListItem([new AdfParagraph([new AdfHardBreak()])])]),
            new AdfOrderedList(7, [new AdfListItem([])]),
            new AdfTaskList([new AdfTaskItem(AdfTaskState.Done, []), new AdfTaskItem(AdfTaskState.Todo, [])]),
            new AdfCodeBlock("csharp", [new AdfText("code", [])]),
            new AdfBlockquote([new AdfRule()]),
            new AdfPanel("warning", []),
            new AdfTable([new AdfTableRow([new AdfTableHeader(2, []), new AdfTableCell(3, [])])]),
            new AdfTable(
            [
                new AdfTableRow(
                [
                    new AdfTableHeader(2, []) { RowSpan = 3, ColumnWidths = [100, 150] },
                    new AdfTableCell(1, [new AdfNestedExpand("Inner", [new AdfParagraph([new AdfText("n", [])])])])
                    {
                        RowSpan = 2,
                    },
                ]),
            ]),
            new AdfDecisionList([new AdfDecisionItem("DECIDED", [new AdfText("choice", [])])]),
            new AdfParagraph([new AdfMediaInline("m-2", "file", "coll", null, 10, 20)]),
            new AdfMediaSingle(null, [new AdfMedia("m-3", "file", null, null, null, null), new AdfCaption([new AdfText("cap", [])])]),
            new AdfExpand("More", []),
            new AdfMediaSingle("wide", [new AdfMedia("m-1", "file", "coll", "alt text", 640, 480)]),
            new AdfMediaGroup([new AdfMedia(null, null, null, null, null, null)]),
            new AdfInlineCard("https://example.test/card"),
            new AdfEmoji(":rocket:", "R"),
            new AdfDate(1760000000000),
            new AdfStatus("DONE", "green"),
            new AdfMention("acc-9", "@Name"),
            new AdfUnknownNode("layoutSection", "own text", [new AdfText("inner", [])]),
        ];
        var document = new AdfDocument(1, content, true);

        var read = AdfCacheMapper.FromDto(AdfCacheMapper.ToDto(document));

        Assert.Equal(Dump(document), Dump(read));
    }

    [Fact]
    public void GivenIssueWithEveryField_WhenReadBack_ThenEveryFieldIsEqual()
    {
        var detail = Detail("GCM-2", null);
        var folder = Path.Combine(_root, "p");
        using (var writer = new SiteCache(folder))
        {
            writer.PutIssue(new CachedIssue(detail, _saved));
            writer.Flush();
        }

        using var reader = new SiteCache(folder);
        var read = reader.GetIssue("GCM-2")!.Detail;

        Assert.Equal(detail.Reporter, read.Reporter);
        Assert.Equal(detail.Assignee, read.Assignee);
        Assert.Equal(detail.Labels, read.Labels);
        Assert.Equal(detail.Created, read.Created);
        Assert.Equal(detail.Parent, read.Parent);
        Assert.Equal(detail.Subtasks, read.Subtasks);
        Assert.Equal(detail.Links, read.Links);
        Assert.Equal(detail.Attachments, read.Attachments);
        Assert.Null(read.Description);
        Assert.Equal(IssueLinkDirection.Outward, read.Links[0].Direction);
    }

    [Fact]
    public void GivenDescriptionNestedAtTheParserLimit_WhenWrittenAndRead_ThenItSurvives()
    {
        var detail = Detail("GTM-9", Nested(AdfParser.MaxDepth - 2));
        var folder = Path.Combine(_root, "p");
        using (var writer = new SiteCache(folder))
        {
            writer.PutIssue(new CachedIssue(detail, _saved));
            writer.Flush();
        }

        using var reader = new SiteCache(folder);
        var read = reader.GetIssue("GTM-9");

        Assert.NotNull(read);
        Assert.Equal(Depth(detail.Description!.Content[0]), Depth(read.Detail.Description!.Content[0]));
    }

    [Fact]
    public void GivenPutIssueWithoutFlush_WhenLookingAtDiskAndMemory_ThenOnlyTheMemoryHasIt()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder);

        cache.PutIssue(new CachedIssue(Detail("A-1", null), _saved));

        Assert.False(Directory.Exists(folder));
        Assert.NotNull(cache.GetIssue("A-1"));
    }

    [Fact]
    public void GivenLowerCaseKey_WhenGettingIssue_ThenItIsFound()
    {
        var folder = Path.Combine(_root, "p");
        using (var writer = new SiteCache(folder))
        {
            writer.PutIssue(new CachedIssue(Detail("GCM-2", null), _saved));
            writer.Flush();
        }

        using var reader = new SiteCache(folder);

        Assert.NotNull(reader.GetIssue("gcm-2"));
    }

    [Fact]
    public void GivenUnknownIssue_WhenGetting_ThenItIsAMiss()
    {
        using var cache = new SiteCache(Path.Combine(_root, "p"));

        Assert.Null(cache.GetIssue("A-404"));
    }

    [Fact]
    public void GivenFileOfAnotherSchema_WhenReading_ThenItIsAMissAndTheFileIsDeleted()
    {
        var folder = Path.Combine(_root, "p");
        WriteAndRewrite(folder, "A-1", text => text.Replace("\"schema\":1", "\"schema\":99"));
        using var reader = new SiteCache(folder);

        Assert.Null(reader.GetIssue("A-1"));
        Assert.False(File.Exists(Path.Combine(folder, SiteCache.IssueFileName("A-1"))));
    }

    [Fact]
    public void GivenCorruptFile_WhenReading_ThenItIsAMissAndTheFileIsDeleted()
    {
        var folder = Path.Combine(_root, "p");
        WriteAndRewrite(folder, "A-1", text => text[..(text.Length / 2)]);
        using var reader = new SiteCache(folder);

        Assert.Null(reader.GetIssue("A-1"));
        Assert.False(File.Exists(Path.Combine(folder, SiteCache.IssueFileName("A-1"))));
    }

    [Fact]
    public void GivenFileWithNullWhereAValueIsRequired_WhenReading_ThenItIsAMiss()
    {
        var folder = Path.Combine(_root, "p");
        WriteAndRewrite(folder, "A-1", text => text.Replace("\"status\":{", "\"status\":null,\"unused\":{"));
        using var reader = new SiteCache(folder);

        Assert.Null(reader.GetIssue("A-1"));
    }

    [Fact]
    public void GivenFileOfAnotherIssue_WhenReadingByKey_ThenItIsAMiss()
    {
        var folder = Path.Combine(_root, "p");
        WriteAndRewrite(folder, "A-1", text => text.Replace("\"key\":\"A-1\"", "\"key\":\"A-2\""));
        using var reader = new SiteCache(folder);

        Assert.Null(reader.GetIssue("A-1"));
    }

    [Fact]
    public void GivenMoreThanTheLimit_WhenPutting_ThenTheLeastRecentlyUsedLeavesTheMemory()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder);

        for (var number = 1; number <= SiteCache.MaxIssues; number++)
        {
            cache.PutIssue(new CachedIssue(Detail($"A-{number}", null), _saved));
        }

        Assert.NotNull(cache.GetIssue("A-1"));
        cache.PutIssue(new CachedIssue(Detail("A-201", null), _saved));
        cache.Flush();
        Directory.Delete(folder, true);

        Assert.NotNull(cache.GetIssue("A-1"));
        Assert.Null(cache.GetIssue("A-2"));
        Assert.NotNull(cache.GetIssue("A-201"));
        Assert.NotNull(cache.GetIssue("A-200"));
    }

    [Fact]
    public void GivenDiskCap_WhenIssuesAndViewsExceedIt_ThenTheOldestFilesGoRegardlessOfKind()
    {
        var folder = Path.Combine(_root, "p");
        using (var seed = new SiteCache(folder))
        {
            seed.Put(new CacheKey(ViewSourceKind.Jql, "old"), new CachedView([], null, _saved));
            seed.PutIssue(new CachedIssue(Detail("A-1", null), _saved));
            seed.Flush();
        }

        var oldView = Path.Combine(folder, SiteCache.ViewFileName(new CacheKey(ViewSourceKind.Jql, "old")));
        var oldIssue = Path.Combine(folder, SiteCache.IssueFileName("A-1"));
        File.SetLastWriteTimeUtc(oldView, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(oldIssue, new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        var cap = new FileInfo(oldView).Length + new FileInfo(oldIssue).Length;

        using var cache = new SiteCache(folder, maxDiskBytes: cap);
        cache.PutIssue(new CachedIssue(Detail("A-2", null), _saved));
        cache.Flush();

        Assert.False(File.Exists(oldView));
        Assert.True(File.Exists(Path.Combine(folder, SiteCache.IssueFileName("A-2"))));
    }

    [Fact]
    public void GivenDisposedCache_WhenPuttingIssue_ThenNothingIsWritten()
    {
        var folder = Path.Combine(_root, "p");
        var cache = new SiteCache(folder);

        cache.Dispose();
        cache.PutIssue(new CachedIssue(Detail("A-1", null), _saved));
        cache.Flush();

        Assert.False(Directory.Exists(folder));
        Assert.Null(cache.GetIssue("A-1"));
    }

    [Fact]
    public void GivenProfileWithCachedIssues_WhenTheProfileIsDeleted_ThenTheIssueFilesGoWithTheFolder()
    {
        var profile = new Profile("a", "https://a.atlassian.net", "me@a.test");
        using var caches = new SiteCaches(_root);
        var cache = caches.For(profile);

        cache.PutIssue(new CachedIssue(Detail("A-1", null), _saved));
        cache.Flush();

        Assert.True(File.Exists(Path.Combine(caches.FolderOf(profile), SiteCache.IssueFileName("A-1"))));
        Assert.True(caches.Delete(profile));
        Assert.False(Directory.Exists(caches.FolderOf(profile)));
    }

    [Fact]
    public void GivenFlushedIssue_WhenLookingAtTheFile_ThenTheNameHidesTheKeyAndTheContentHoldsNoCredentials()
    {
        var folder = Path.Combine(_root, "p");
        using var cache = new SiteCache(folder);

        cache.PutIssue(new CachedIssue(Detail("SECRET-1", null), _saved));
        cache.Flush();

        var file = Assert.Single(Directory.GetFiles(folder));
        var text = File.ReadAllText(file);

        Assert.DoesNotContain("SECRET", Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", text, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private static string Dump(object? value)
    {
        var builder = new StringBuilder();

        DumpTo(builder, value);

        return builder.ToString();
    }

    // Reflection is fine in tests; the records hold lists that compare by reference, so walk them by value.
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Test helper, never trimmed.")]
    private static void DumpTo(StringBuilder builder, object? value)
    {
        if (value is null or string || value.GetType().IsPrimitive || value.GetType().IsEnum)
        {
            builder.Append(value).Append(';');

            return;
        }

        if (value is IEnumerable items)
        {
            builder.Append('[');

            foreach (var item in items)
            {
                DumpTo(builder, item);
            }

            builder.Append(']');

            return;
        }

        builder.Append(value.GetType().Name).Append('{');

        var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties.Where(IsData))
        {
            builder.Append(property.Name).Append('=');
            DumpTo(builder, property.GetValue(value));
        }

        builder.Append('}');
    }

    private static bool IsData(PropertyInfo property)
    {
        return property.Name != "EqualityContract";
    }

    private static string Json(CachedIssue issue)
    {
        return JsonSerializer.Serialize(IssueCacheMapper.ToFile(issue), CacheJsonContext.Default.CachedIssueFile);
    }

    private static AdfDocument Nested(int levels)
    {
        AdfNode node = new AdfParagraph([new AdfText("deep", [new AdfStrongMark()])]);

        for (var level = 0; level < levels; level++)
        {
            node = new AdfBlockquote([node]);
        }

        return new AdfDocument(1, [node], false);
    }

    private static int Depth(AdfNode node)
    {
        return node is AdfContainerNode container && container.Children.Count > 0
            ? 1 + Depth(container.Children[0])
            : 1;
    }

    private static IssueDetail Detail(string key, AdfDocument? description)
    {
        var person = new IssueAssignee("acc-1", "Test User 1");
        var stub = new IssueStub("A-100", "Epic", _status, new IssueTypeSummary("11", "Epic", 1, false));

        return new IssueDetail(
            "10001",
            key,
            $"Summary of {key}",
            _status,
            _type,
            "High",
            person,
            new IssueAssignee("acc-2", "Test User 2"),
            ["bug", "ui"],
            _saved.AddDays(-3),
            _saved.AddDays(-1),
            key.Split('-')[0],
            description,
            stub,
            [stub with { Key = "A-101" }],
            [new IssueLink("1", "Blocks", IssueLinkDirection.Outward, "blocks", stub)],
            [new IssueAttachment("9", "näyte.png", "image/png", 1234, _saved, person, "https://example.test/9")]);
    }

    private static void WriteAndRewrite(string folder, string key, Func<string, string> change)
    {
        using (var writer = new SiteCache(folder))
        {
            writer.PutIssue(new CachedIssue(Detail(key, null), _saved));
            writer.Flush();
        }

        var path = Path.Combine(folder, SiteCache.IssueFileName(key));

        File.WriteAllText(path, change(File.ReadAllText(path)));
    }
}
