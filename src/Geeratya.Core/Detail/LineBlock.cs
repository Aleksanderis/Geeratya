namespace Geeratya.Core.Detail;

/// <summary>Document lines that form one cursor stop: <paramref name="Start"/> up to, not including, <paramref name="End"/>.</summary>
public sealed record LineBlock(int Start, int End);
