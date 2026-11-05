using Geeratya.Core.State;

namespace Geeratya.Core.Detail;

/// <summary>
/// The pointer dragged over the main column of the opened issue (press, release): the text between the two lines is
/// selected and copied, the mouse twin of <c>v</c>, the motions and <c>y</c>.
/// </summary>
/// <param name="FromColumn">The screen column where the button went down.</param>
/// <param name="FromRow">The body row where the button went down.</param>
/// <param name="ToColumn">The screen column where it was released.</param>
/// <param name="ToRow">The body row where it was released.</param>
/// <param name="Width">The screen width the body was drawn for.</param>
public sealed record DetailDragged(int FromColumn, int FromRow, int ToColumn, int ToRow, int Width) : Command;
