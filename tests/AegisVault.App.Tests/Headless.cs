using Avalonia.Headless;

namespace AegisVault.App.Tests;

/// <summary>
/// Shared headless Avalonia session. UI-touching test bodies must run through
/// <see cref="Run(Action)"/> / <see cref="RunAsync{T}(Func{Task{T}})"/>.
/// </summary>
internal static class Headless
{
    private static readonly HeadlessUnitTestSession Session =
        HeadlessUnitTestSession.StartNew(typeof(global::AegisVault.App.App));

    public static Task Run(Action action)
        => Session.Dispatch(action, CancellationToken.None);

    public static Task<T> RunAsync<T>(Func<Task<T>> func)
        => Session.Dispatch(func, CancellationToken.None);
}
