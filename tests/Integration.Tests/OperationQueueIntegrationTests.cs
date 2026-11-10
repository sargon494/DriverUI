using Agent.Service.Operations;
using FluentAssertions;

namespace Integration.Tests;

/// <summary>
/// Cola de operaciones real: comprueba exclusión mutua y que un fallo no bloquea la cola.
/// </summary>
[Trait("Category", "Integration")]
public class OperationQueueIntegrationTests
{
    [Fact]
    public async Task Should_Run_Operations_One_At_A_Time()
    {
        using var queue = new OperationQueue();
        var release = new TaskCompletionSource();
        var secondRuns = 0;

        var first = queue.EnqueueAsync(async ct =>
        {
            await release.Task.WaitAsync(ct);
            return 1;
        }, CancellationToken.None);

        await WaitUntilAsync(() => queue.State.OperationInProgress);

        var second = queue.EnqueueAsync(ct =>
        {
            Interlocked.Increment(ref secondRuns);
            return Task.FromResult(2);
        }, CancellationToken.None);

        await Task.Delay(50);
        Volatile.Read(ref secondRuns).Should().Be(0, "la segunda operación debe esperar a la primera");

        release.SetResult();

        (await first).Should().Be(1);
        (await second).Should().Be(2);
        Volatile.Read(ref secondRuns).Should().Be(1);
        queue.State.OperationInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Keep_Queue_Usable_After_An_Operation_Fails()
    {
        using var queue = new OperationQueue();

        Func<Task> failing = () => queue.EnqueueAsync<int>(
            ct => Task.FromException<int>(new InvalidOperationException("fallo simulado")),
            CancellationToken.None);

        await failing.Should().ThrowAsync<InvalidOperationException>();

        var result = await queue.EnqueueAsync(_ => Task.FromResult(42), CancellationToken.None);

        result.Should().Be(42);
        queue.State.OperationInProgress.Should().BeFalse();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("La operación no empezó a tiempo.");
            }

            await Task.Delay(10);
        }
    }
}
