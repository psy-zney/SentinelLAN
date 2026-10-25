using SentinelLAN.Setup;

namespace SentinelLAN.Agent.Tests;

public sealed class SetupWorkflowTests
{
    [Fact]
    public async Task ExistingIdentitySkipsCodeInputAndResumesConnection()
    {
        var workflow = new SetupWorkflow(() => true);
        var resumed = false;
        await workflow.RunAsync(() => throw new InvalidOperationException("A connection code must not be read."),
            () => { resumed = true; return Task.CompletedTask; });
        Assert.True(resumed);
        Assert.True(workflow.Completed);
        Assert.False(workflow.RequiresEnrollment);
    }

    [Fact]
    public async Task NewMachineEnrollsBeforeCompletion()
    {
        var hasIdentity = false;
        var workflow = new SetupWorkflow(() => hasIdentity);
        Assert.True(workflow.RequiresEnrollment);
        await workflow.RunAsync(() => { hasIdentity = true; return Task.CompletedTask; },
            () => throw new InvalidOperationException("A new machine cannot resume."));
        Assert.True(workflow.Completed);
        Assert.False(workflow.RequiresEnrollment);
    }

    [Fact]
    public async Task FailureAfterIdentityWasSavedRetriesWithoutReusingTheCode()
    {
        var hasIdentity = false;
        var enrollments = 0;
        var workflow = new SetupWorkflow(() => hasIdentity);
        Task Enroll()
        {
            enrollments++;
            hasIdentity = true;
            throw new IOException("First heartbeat was not confirmed.");
        }
        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(Enroll, () => Task.CompletedTask));
        Assert.False(workflow.Completed);
        Assert.False(workflow.RequiresEnrollment);
        await workflow.RunAsync(Enroll, () => Task.CompletedTask);
        Assert.True(workflow.Completed);
        Assert.Equal(1, enrollments);
    }

    [Fact]
    public async Task FailedResumeCanBeRetriedAndMissingIdentityReturnsToEnrollment()
    {
        var hasIdentity = true;
        var workflow = new SetupWorkflow(() => hasIdentity);
        await Assert.ThrowsAsync<HttpRequestException>(() => workflow.RunAsync(() => Task.CompletedTask,
            () => throw new HttpRequestException("Offline")));
        Assert.False(workflow.Completed);
        hasIdentity = false;
        Assert.True(workflow.RequiresEnrollment);
        await workflow.RunAsync(() => { hasIdentity = true; return Task.CompletedTask; },
            () => throw new InvalidOperationException("The missing identity must not be reused."));
        Assert.True(workflow.Completed);
    }
}
