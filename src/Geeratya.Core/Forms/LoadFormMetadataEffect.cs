using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Forms;

/// <summary>
/// Fetch the field metadata of a form (editmeta, createmeta or the transition fields), when the form opens and never
/// at startup. Answered with <see cref="FormMetadataLoaded"/> or <see cref="FormMetadataFailed"/>.
/// </summary>
public sealed record LoadFormMetadataEffect(TabId Tab, long RequestId, FormTarget Target) : Effect;
