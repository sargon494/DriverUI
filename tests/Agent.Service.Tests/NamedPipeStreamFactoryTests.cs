using System.IO.Pipes;
using Agent.Service.Ipc;
using FluentAssertions;

namespace Agent.Service.Tests;

public class NamedPipeStreamFactoryTests
{
    [Fact]
    public void NormalizePipeOptions_Should_Keep_FirstPipeInstance_On_The_First_Instance()
    {
        var options = PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance | PipeOptions.WriteThrough;

        var normalized = NamedPipeStreamFactory.NormalizePipeOptions(isFirstInstance: true, options);

        normalized.Should().Be(options);
    }

    [Fact]
    public void NormalizePipeOptions_Should_Strip_FirstPipeInstance_On_Subsequent_Instances()
    {
        var options = PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance | PipeOptions.WriteThrough;

        var normalized = NamedPipeStreamFactory.NormalizePipeOptions(isFirstInstance: false, options);

        normalized.Should().Be(PipeOptions.Asynchronous | PipeOptions.WriteThrough);
    }
}
