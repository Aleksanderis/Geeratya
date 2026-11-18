using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Jira.Create.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class FormFooterTests
{
    private const int NarrowWidth = 60;

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenACreateFormWithFieldsOnlyTheBrowserEdits_WhenTheFooterIsNarrow_ThenTheWriteAndTheWayOutStay()
    {
        var target = new CreateFormTarget("GCM", null, false, new CreateIssueType("10", "Epic", "", 0, false));
        var fields = new[]
        {
            FormField.Create(FormFieldIds.Summary, "Summary", FormFieldKind.Text, true, []),
            FormField.Create("attachment", "Attachment", FormFieldKind.Unsupported, false, []),
        };

        var form = new FormState(target, fields, 0, false, 1, null, null, "https://work.atlassian.net/browse/GCM")
        {
            Navigable = true,
        };

        var text = FormFooter.Line(form, _keymap, NarrowWidth).PlainText;

        Assert.Contains("Ctrl-s create", text, StringComparison.Ordinal);
        Assert.Contains("Esc ", text, StringComparison.Ordinal);
    }
}
