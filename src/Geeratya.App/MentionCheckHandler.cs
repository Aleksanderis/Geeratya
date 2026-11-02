using Geeratya.Core.Comments;
using Geeratya.Core.State;

namespace Geeratya.App;

/// <summary>
/// Runs <see cref="CheckMentionsEffect"/>: for each person a comment already mentions, asks Jira for the people who
/// can see the issue under that name (<c>viewissue/search</c>, the lookup the editor's popup uses) and reports the
/// ones whose account did not come back. Reads only. A person is flagged only on a clean answer that is not
/// truncated (a full page may hide the account); a failed lookup flags nobody and posts nothing, because the check
/// is a hint and must never get in the way of the edit.
/// </summary>
public sealed class MentionCheckHandler
{
    /// <summary>Jira's answer is capped at 20 users; a full page proves nothing about the 21st.</summary>
    private const int FullPage = 20;

    private readonly NetworkRunner _runner;

    public MentionCheckHandler(NetworkRunner runner)
    {
        _runner = runner;
    }

    public Task StartAsync(CheckMentionsEffect effect)
    {
        return _runner.Start(effect.Tab.Value, (services, token) => RunAsync(services, effect, token));
    }

    private static async Task<Command?> RunAsync(JiraServices services, CheckMentionsEffect effect, CancellationToken token)
    {
        var unseen = new List<MentionedPerson>();

        foreach (var person in effect.People)
        {
            var found = await services.Suggestions.SearchMentionableUsersAsync(effect.Key, person.Name, token);

            if (!found.IsSuccess)
            {
                return null;
            }

            if (found.Value.Count < FullPage && found.Value.All(user => user.Id != person.Id))
            {
                unseen.Add(person);
            }
        }

        return new MentionsChecked(effect.Tab, effect.Key, effect.CommentId, unseen);
    }
}
