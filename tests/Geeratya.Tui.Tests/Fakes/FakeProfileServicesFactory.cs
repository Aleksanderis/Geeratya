using Geeratya.App;

namespace Geeratya.Tui.Tests.Fakes;

/// <summary>Creates the profile services through a function the test supplies (it may throw).</summary>
public sealed class FakeProfileServicesFactory(Func<ProfileServices> create) : IProfileServicesFactory
{
    public ProfileServices Create()
    {
        return create();
    }
}
