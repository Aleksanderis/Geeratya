using Geeratya.Jira.Boards;
using Geeratya.Jira.Comments;
using Geeratya.Jira.Create;
using Geeratya.Jira.Edit;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Links;
using Geeratya.Jira.Rank;
using Geeratya.Jira.RemoteLinks;
using Geeratya.Jira.Fields;
using Geeratya.Jira.Filters;
using Geeratya.Jira.Gateway;
using Geeratya.Jira.Search;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Transitions;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.App;

/// <summary>
/// The Jira clients of the active site, built from its transport. Clients (search, board, filter, issue) are added
/// here as the Jira layer gains them; no DI container. An instance belongs to one connection and is never reused
/// after a profile switch.
/// </summary>
public sealed class JiraServices
{
    private JiraServices(
        JiraTransport transport,
        BoardKindResolver boardKinds,
        SiteFields fields,
        DoneCutoffReader cutoffs,
        int generation)
    {
        DoneCutoffs = cutoffs;
        Transport = transport;
        Generation = generation;
        BoardKinds = boardKinds;
        Fields = fields;
        Search = new JiraSearchClient(transport);
        Users = new JiraUserClient(transport);
        Issues = new IssueClient(transport);
        Comments = new CommentClient(transport);
        RemoteLinks = new RemoteLinkClient(transport);
        Boards = new BoardClient(transport);
        Filters = new FilterClient(transport);
        Edits = new EditClient(transport);
        Creates = new IssueCreateClient(transport);
        Links = new LinkClient(transport);
        Suggestions = new SuggestionClient(transport);
        Transitions = new TransitionClient(transport);
        Ranks = new RankClient(transport);
        Sprints = new SprintClient(transport);
        SprintWrites = new SprintWriteClient(transport);
        SprintMoves = new SprintMoveClient(transport);
    }

    /// <summary>Jira's own Done cutoff per Kanban board (the gateway; optional, kept for the connection).</summary>
    public DoneCutoffReader DoneCutoffs { get; }

    /// <summary>Scrum or Kanban per board; answers are kept per profile, so a board is probed once.</summary>
    public BoardKindResolver BoardKinds { get; }

    /// <summary>Sprints of a board and the issues of a sprint (reads).</summary>
    public SprintClient Sprints { get; }

    /// <summary>Creates, edits, starts and completes sprints (writes: only called from a confirmed command).</summary>
    public SprintWriteClient SprintWrites { get; }

    /// <summary>Moves issues into a sprint or the backlog (writes: only called from a confirmed command).</summary>
    public SprintMoveClient SprintMoves { get; }

    /// <summary>The Sprint and estimate field ids of this site, read once per connection.</summary>
    public SiteFields Fields { get; }

    /// <summary>Create metadata and the create call (the write is only called from the write handler).</summary>
    public IssueCreateClient Creates { get; }

    /// <summary>Link types, and creating and removing issue links (writes: only called from the write handler).</summary>
    public LinkClient Links { get; }
    /// <summary>Lookups behind pickers: people, labels, components (reads).</summary>
    public SuggestionClient Suggestions { get; }

    /// <summary>Lists the transitions of an issue and performs one (the write only from the write handler).</summary>
    public TransitionClient Transitions { get; }

    /// <summary>Edits fields and assigns issues (writes: only called from the write handler).</summary>
    public EditClient Edits { get; }

    /// <summary>Ranks issues (a write: only called from the write handler).</summary>
    public RankClient Ranks { get; }

    /// <summary>Boards, board configuration, board issues and backlog (agile API).</summary>
    public BoardClient Boards { get; }

    /// <summary>Saved filters: the caller's own and favourites.</summary>
    public FilterClient Filters { get; }

    /// <summary>The current account (<c>/myself</c>), also the credential check.</summary>
    public JiraUserClient Users { get; }

    /// <summary>Reads one issue for the detail screen.</summary>
    public IssueClient Issues { get; }

    /// <summary>Reads the comments of one issue.</summary>
    public CommentClient Comments { get; }

    /// <summary>Reads the remote links of one issue.</summary>
    public RemoteLinkClient RemoteLinks { get; }

    public JiraTransport Transport { get; }

    /// <summary>JQL search over this site's transport.</summary>
    public JiraSearchClient Search { get; }

    /// <summary>The <see cref="SiteConnections.Generation"/> these services were built under.</summary>
    public int Generation { get; }

    /// <summary>Services for the connected profile, or null while no transport exists (not connected yet or failed).</summary>
    public static JiraServices? Create(SiteConnections connections)
    {
        var generation = connections.Generation;
        var parts = connections.Snapshot();

        // Generation is read first: if a switch lands in between, the stamp is older than the transport and the
        // result is dropped as stale, never the other way round.
        if (parts is not { Transport: { } transport, Kinds: { } kinds, Fields: { } fields, Cutoffs: { } cutoffs })
        {
            return null;
        }

        return new JiraServices(transport, kinds, fields, cutoffs, generation);
    }
}
