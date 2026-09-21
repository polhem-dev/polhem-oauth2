namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// A clock that a test moves forward, so that lifetimes are tested without waiting.
    /// </summary>
    internal sealed class AspNetCoreTestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan time) => _now += time;
    }
}
