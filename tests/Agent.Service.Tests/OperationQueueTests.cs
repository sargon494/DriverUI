using Agent.Service.Operations;
using Core.Models;
using FluentAssertions;

namespace Agent.Service.Tests;

public class OperationQueueTests
{
    private static InstallProgress CreateProgress(int percent, InstallPhase phase) => new()
    {
        PercentComplete = percent,
        StatusMessage = "Prueba",
        Phase = phase
    };

    public class EnqueueAsync
    {
        [Fact]
        public async Task Should_Run_Operation_And_Return_Result()
        {
            var queue = new OperationQueue();

            var result = await queue.EnqueueAsync(ct => Task.FromResult(42), CancellationToken.None);

            result.Should().Be(42);
            queue.State.OperationInProgress.Should().BeFalse();
            queue.State.QueuedOperations.Should().Be(0);
        }

        [Fact]
        public async Task Should_Serialize_Operations_One_At_A_Time()
        {
            var queue = new OperationQueue();
            using var firstStarted = new SemaphoreSlim(0, 1);
            using var releaseFirst = new SemaphoreSlim(0, 1);
            using var firstCompleted = new ManualResetEventSlim(false);

            var first = queue.EnqueueAsync(async ct =>
            {
                firstStarted.Release();
                await releaseFirst.WaitAsync(ct);
                return 1;
            }, CancellationToken.None);

            var secondStarted = new ManualResetEventSlim(false);
            var second = queue.EnqueueAsync(_ =>
            {
                secondStarted.Set();
                return Task.FromResult(2);
            }, CancellationToken.None);

            firstStarted.Wait(TimeSpan.FromSeconds(5));
            queue.State.QueuedOperations.Should().Be(2);

            // La segunda operación no debe arrancar mientras la primera siga en curso.
            secondStarted.IsSet.Should().BeFalse();

            releaseFirst.Release();
            (await first).Should().Be(1);
            firstCompleted.Set();
            (await second).Should().Be(2);
            secondStarted.IsSet.Should().BeTrue();
            queue.State.QueuedOperations.Should().Be(0);
        }

        [Fact]
        public async Task Should_Throw_When_Cancelled_Before_Acquiring_The_Gate()
        {
            var queue = new OperationQueue();
            using var firstStarted = new SemaphoreSlim(0, 1);
            using var releaseFirst = new SemaphoreSlim(0, 1);
            using var cts = new CancellationTokenSource();

            var first = queue.EnqueueAsync(async ct =>
            {
                firstStarted.Release();
                await releaseFirst.WaitAsync(ct);
                return 1;
            }, CancellationToken.None);

            firstStarted.Wait(TimeSpan.FromSeconds(5));

            cts.Cancel();

            var act = () => queue.EnqueueAsync(ct => Task.FromResult(2), cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();

            // El estado de la cola no debe corromperse por el intento cancelado.
            releaseFirst.Release();
            (await first).Should().Be(1);
            queue.State.QueuedOperations.Should().Be(0);
        }

        [Fact]
        public async Task Should_Release_The_Gate_When_The_Operation_Throws()
        {
            var queue = new OperationQueue();

            var act = () => queue.EnqueueAsync<object?>(_ => throw new InvalidOperationException("boom"), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            queue.State.OperationInProgress.Should().BeFalse();
            queue.State.QueuedOperations.Should().Be(0);

            // La cola debe seguir operativa tras el fallo.
            var next = await queue.EnqueueAsync(ct => Task.FromResult(7), CancellationToken.None);
            next.Should().Be(7);
        }

        [Fact]
        public async Task Should_Cancel_The_Running_Operation_When_The_Token_Fires()
        {
            var queue = new OperationQueue();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            var act = () => queue.EnqueueAsync<object?>(async ct =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return null;
            }, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
            queue.State.OperationInProgress.Should().BeFalse();
        }
    }

    public class State
    {
        [Fact]
        public void Should_Reflect_Progress_Updates()
        {
            var queue = new OperationQueue();

            queue.UpdateProgress("update-1", CreateProgress(70, InstallPhase.Installing));

            var state = queue.State;
            state.CurrentUpdateId.Should().Be("update-1");
            state.PercentComplete.Should().Be(70);
            state.Phase.Should().Be(InstallPhase.Installing);
        }

        [Fact]
        public void Should_Latch_PendingReboot_Until_Service_Restarts()
        {
            var queue = new OperationQueue();

            queue.MarkPendingReboot();

            queue.State.PendingReboot.Should().BeTrue();

            // Una operación posterior no limpia el indicador: solo el reinicio
            // del equipo (y con ello del servicio) lo hace.
            queue.UpdateProgress("update-2", CreateProgress(10, InstallPhase.Downloading));
            queue.State.PendingReboot.Should().BeTrue();
        }

        [Fact]
        public void Should_Expose_LastScanUtc_After_Scan_Completed()
        {
            var queue = new OperationQueue();
            var before = DateTimeOffset.UtcNow;

            queue.MarkScanCompleted();

            var lastScan = queue.State.LastScanUtc;
            lastScan.Should().NotBeNull();
            lastScan!.Value.Should().BeOnOrAfter(before);
        }

        [Fact]
        public void Should_Throw_On_Invalid_Progress_Arguments()
        {
            var queue = new OperationQueue();
            var progress = CreateProgress(1, InstallPhase.Downloading);

            var actNullId = () => queue.UpdateProgress("", progress);
            var actNullProgress = () => queue.UpdateProgress("id", null!);

            actNullId.Should().Throw<ArgumentException>();
            actNullProgress.Should().Throw<ArgumentNullException>();
        }
    }
}
