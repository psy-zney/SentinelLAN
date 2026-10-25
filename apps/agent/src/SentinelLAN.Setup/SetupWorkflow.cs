namespace SentinelLAN.Setup;

internal sealed class SetupWorkflow(Func<bool> hasIdentity)
{
    public bool RequiresEnrollment => !hasIdentity();
    public bool Completed { get; private set; }

    public async Task RunAsync(Func<Task> enroll, Func<Task> resume)
    {
        Completed = false;
        if (RequiresEnrollment) await enroll();
        else await resume();
        Completed = true;
    }
}
