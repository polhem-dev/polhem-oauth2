namespace Polhem.OAuth2.UnitTests
{
    /// <summary>
    /// Bounds how long a test waits for work outside the test method, such as a fake browser or a listener, so that a
    /// regression fails the test instead of stopping the test run.
    /// </summary>
    internal static class TestTaskExtensions
    {
        private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

        public static async Task WithTimeout(this Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(s_timeout)) != task)
                throw new TimeoutException("The awaited operation did not complete in time.");
            await task;
        }

        public static async Task<T> WithTimeout<T>(this Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(s_timeout)) != task)
                throw new TimeoutException("The awaited operation did not complete in time.");
            return await task;
        }
    }
}
