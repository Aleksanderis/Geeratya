using Geeratya.Jira.Transitions.Models;
using Geeratya.Core.Forms;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Forms;

/// <summary>The one field renderer: each kind, the empty word, the caret, the errors, at several widths.</summary>
[Trait("Os", "Independent")]
public class FieldRowsTests
{
    private static readonly FieldOption[] _options = [new("1", "High"), new("2", "Low"), new("3", "A longer option name")];

    public static TheoryData<int> Widths()
    {
        return [1, 2, 4, 8, 24, 80];
    }

    [Theory]
    [MemberData(nameof(Widths))]
    public void GivenAnyKindAndWidth_WhenDrawn_ThenThereIsAtLeastOneRowAndNothingThrows(int width)
    {
        foreach (var field in Fields())
        {
            foreach (var editing in new[] { false, true })
            {
                var look = new FieldLook(width) { Editing = editing, Rows = 2 };

                Assert.NotEmpty(FieldRows.Value(field, look));
                Assert.NotEmpty(FieldRows.Value(field, look with { Layout = FieldLayout.Items }));
                Assert.NotEmpty(FieldRows.Value(field, look with { Boxed = true, Expanded = true }));
            }
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(24)]
    [InlineData(80)]
    public void GivenTextAndChoiceFields_WhenDrawnAtAReasonableWidth_ThenNoRowIsWiderThanTheWidth(int width)
    {
        foreach (var field in Fields().Where(static field => field.Kind != FormFieldKind.User))
        {
            var look = new FieldLook(width) { Editing = true, Rows = 3 };

            foreach (var line in FieldRows.Value(field, look))
            {
                Assert.True(DisplayWidth.Of(line.PlainText) <= width, $"{field.Kind} at {width}: '{line.PlainText}'");
            }
        }
    }

    [Fact]
    public void GivenEmptyFields_WhenNotEdited_ThenTheSharedEmptyWordShowsMuted()
    {
        var text = FormField.Create("a", "A", FormFieldKind.Text, false, []);
        var person = FormField.Create("b", "B", FormFieldKind.User, false, []);
        var labels = FormField.Create("c", "C", FormFieldKind.Labels, false, []);
        var look = new FieldLook(40);

        var textRow = Assert.Single(FieldRows.Value(text, look)).Spans.Single();
        var personRow = Assert.Single(FieldRows.Value(person, look)).Spans.Single();
        var labelsRow = Assert.Single(FieldRows.Value(labels, look)).Spans.Single();

        Assert.Equal((EmptyValues.None, TextRole.Muted), (textRow.Text, textRow.Role));
        Assert.Equal((EmptyValues.Unassigned, TextRole.Muted), (personRow.Text, personRow.Role));
        Assert.Equal((EmptyValues.None, TextRole.Muted), (labelsRow.Text, labelsRow.Role));
    }

    [Fact]
    public void GivenAnEditedText_WhenDrawn_ThenTheCaretSitsAfterItInTheCaretRole()
    {
        var field = FormField.Create("a", "A", FormFieldKind.Text, false, []).WithInitial(FormValue.OfText("abc"));

        var row = Assert.Single(FieldRows.Value(field, new FieldLook(20, TextRole.Body) { Editing = true }));

        Assert.Equal("abc ", row.PlainText);
        Assert.Contains(row.Spans, span => span.Role == TextRole.EditCaret);
        Assert.Contains(row.Spans, span => span.Role == TextRole.Body && span.Text == "abc");
    }

    [Fact]
    public void GivenAnEmptyFieldWithAPlaceholder_WhenEdited_ThenThePlaceholderShowsMutedBeforeTheCaret()
    {
        var field = FormField.Create("a", "A", FormFieldKind.Text, true, []);

        var row = Assert.Single(FieldRows.Value(field, new FieldLook(30) { Editing = true, Placeholder = "What now?" }));

        Assert.Equal("What now? ", row.PlainText);
        Assert.Equal(TextRole.Muted, row.Spans[0].Role);
        Assert.Equal(TextRole.EditCaret, row.Spans[^1].Role);
    }

    [Fact]
    public void GivenAParagraph_WhenNotExpanded_ThenTheFirstRowShowsWithAnEllipsisAndExpandedShowsAllRows()
    {
        var field = FormField.Create("p", "P", FormFieldKind.Paragraph, false, []).WithInitial(FormValue.OfText("one\ntwo\nthree"));

        var collapsed = FieldRows.Value(field, new FieldLook(30));
        var expanded = FieldRows.Value(field, new FieldLook(30) { Expanded = true });

        Assert.Equal(["one…"], collapsed.Select(static line => line.PlainText));
        Assert.Equal(["one", "two", "three"], expanded.Select(static line => line.PlainText));
    }

    [Fact]
    public void GivenAParagraphBeingEdited_WhenTheWindowIsTwoRows_ThenItFollowsTheCaretRow()
    {
        var field = FormField.Create("p", "P", FormFieldKind.Paragraph, false, []).WithInitial(FormValue.OfText("1\n2\n3\n4"));

        var rows = FieldRows.Value(field, new FieldLook(30) { Editing = true, Rows = 2 });

        Assert.Equal(2, rows.Count);
        Assert.StartsWith("3", rows[0].PlainText, StringComparison.Ordinal);
        Assert.StartsWith("4", rows[1].PlainText, StringComparison.Ordinal);
        Assert.Contains(rows[1].Spans, span => span.Role == TextRole.EditCaret);
    }

    [Fact]
    public void GivenLabels_WhenLaidOutInlineOrAsItems_ThenInlineWrapsTokensAndItemsAreOneRowEach()
    {
        var field = FormField.Create("labels", "Labels", FormFieldKind.Labels, false, [])
            .WithInitial(FormValue.OfChosen(["backend", "frontend", "docs"]));

        var inline = FieldRows.Value(field, new FieldLook(20));
        var items = FieldRows.Value(field, new FieldLook(20) { Layout = FieldLayout.Items });

        Assert.Equal(["[backend] [frontend]", "[docs]"], inline.Select(static line => line.PlainText));
        Assert.Equal(3, items.Count);
        Assert.All(items, static line => Assert.Contains(line.Spans, span => span.Role.ToString().StartsWith("Label", StringComparison.Ordinal)));
    }

    [Fact]
    public void GivenAPriorityAndAPerson_WhenDrawn_ThenTheyUseTheSharedValueText()
    {
        var priority = FormField.Create(FormFieldIds.Priority, "Priority", FormFieldKind.Select, false, _options)
            .WithInitial(FormValue.OfChosen(["1"]));
        var person = FormField.Create("assignee", "Assignee", FormFieldKind.User, false, [new FieldOption("u", "Jane Doe")])
            .WithInitial(FormValue.OfChosen(["u"]));

        var priorityRow = Assert.Single(FieldRows.Value(priority, new FieldLook(30)));
        var personRow = Assert.Single(FieldRows.Value(person, new FieldLook(30)));

        Assert.Equal(string.Concat(ValueText.Priority("High", TextRole.Editable).Select(static span => span.Text)), priorityRow.PlainText);
        Assert.Equal(string.Concat(ValueText.Person("Jane Doe", 2, TextRole.Editable).Select(static span => span.Text)), personRow.PlainText);
    }

    [Fact]
    public void GivenALongMessage_WhenTheErrorIsDrawn_ThenItWrapsWithTheMarkAndTheIndentAndIsNeverCutMidSentence()
    {
        var rows = FieldRows.Error("Estimate must be a number such as 0.5 or 3", 24, 4);

        Assert.StartsWith("    ! ", rows[0].PlainText, StringComparison.Ordinal);
        Assert.All(rows.Skip(1), static row => Assert.StartsWith("      ", row.PlainText, StringComparison.Ordinal));
        Assert.All(rows, row => Assert.True(DisplayWidth.Of(row.PlainText) <= 24));
        Assert.Contains("3", rows[^1].PlainText, StringComparison.Ordinal);
        Assert.All(rows, static row => Assert.Contains(row.Spans, span => span.Role == TextRole.Error));
    }

    private static List<FormField> Fields()
    {
        FieldOption[] people = [new("u", "Jane Doe")];

        return
        [
            FormField.Create("t", "T", FormFieldKind.Text, false, []).WithInitial(FormValue.OfText("some text")),
            FormField.Create("n", "N", FormFieldKind.Number, false, []).WithInitial(FormValue.OfText("3.5")),
            FormField.Create("p", "P", FormFieldKind.Paragraph, false, []).WithInitial(FormValue.OfText("a\nb\nc d e")),
            FormField.Create("s", "S", FormFieldKind.Select, false, _options).WithInitial(FormValue.OfChosen(["3"])),
            FormField.Create("m", "M", FormFieldKind.MultiSelect, false, _options)
                .WithInitial(FormValue.OfChosen(["1", "2"])),
            FormField.Create("labels", "L", FormFieldKind.Labels, false, [])
                .WithInitial(FormValue.OfChosen(["backend", "ui"])),
            FormField.Create("u", "U", FormFieldKind.User, false, people).WithInitial(FormValue.OfChosen(["u"])),
            FormField.Create("parent", "Pa", FormFieldKind.Parent, false, [new FieldOption("A-1", "A-1 Epic")])
                .WithInitial(FormValue.OfChosen(["A-1"])),
            FormField.Create("e", "E", FormFieldKind.Text, false, []),
        ];
    }
}
