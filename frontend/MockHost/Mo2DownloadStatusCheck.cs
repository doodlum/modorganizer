using NexusMods.App.UI.Pages.Downloads;
using NexusMods.Paths;
using NexusMods.Sdk.Jobs;
using R3;

namespace Mo2.Frontend;

internal static class Mo2DownloadStatusCheck
{
    internal static void Run()
    {
        using var states = new Subject<JobStatus>();
        var activeSubscriptions = 0;
        var tracked = Observable.Create<JobStatus>(observer => {
            activeSubscriptions++;
            var subscription = states.Subscribe(observer);
            return Disposable.Create(() => { subscription.Dispose(); activeSubscriptions--; });
        });
        using var native = new DownloadComponents.StatusComponent(Percent.Zero, JobStatus.Failed,
            Observable.Return(Percent.Zero), tracked);
        using var mo2 = new DownloadComponents.StatusComponent(Percent.Zero, JobStatus.Failed,
            Observable.Return(Percent.Zero), tracked, canRetryFailed: true, canCancelInactive: false);
        void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        Assert(!native.CanResume.Value && !native.CanCancel.Value, "Default failed-job behavior changed");
        Assert(mo2.Status.Value == JobStatus.Failed && mo2.CanResume.Value && !mo2.CanCancel.Value && !mo2.CanPause.Value,
            "MO2 failed download retry controls are incorrect");
        states.OnNext(JobStatus.Running);
        Assert(mo2.CanPause.Value && !mo2.CanResume.Value && mo2.CanCancel.Value, "Resumed transfer controls are stale");
        states.OnNext(JobStatus.Paused);
        Assert(mo2.CanResume.Value && !mo2.CanPause.Value && !mo2.CanCancel.Value && native.CanResume.Value && native.CanCancel.Value, "Paused transfer controls are incorrect");
        states.OnNext(JobStatus.Failed);
        Assert(mo2.CanResume.Value && !mo2.CanCancel.Value && !native.CanResume.Value, "Failure transition lost retry support");
        states.OnNext(JobStatus.Completed);
        Assert(mo2.IsCompleted.Value && !mo2.CanResume.Value && !mo2.CanPause.Value && !mo2.CanCancel.Value,
            "Completed transfer retained actions");

        foreach (var (partial, paused, failed, pause, resume, cancel) in new[] {
            (true, false, false, true, false, true),
            (true, true, false, false, true, false),
            (true, true, true, false, true, false),
            (true, false, true, false, true, false),
            (false, false, false, false, false, false)
        }) {
            var file = new Mo2Download("fixture.zip", "/fixture.zip", 1, partial, false, paused, failed);
            Assert(file.CanControl("pause") == pause && file.CanControl("resume") == resume &&
                file.CanControl("cancel") == cancel && file.CanControl("delete") == (!partial || paused || failed) && !file.CanControl("invalid"), "File action policy disagrees with native states");
        }
        mo2.Dispose(); native.Dispose();
        Assert(activeSubscriptions == 0, "Disposed download status retained subscriptions");
        Console.WriteLine("PASS download status: MO2 retry, running/paused/failed/completed transitions, inactive cancel suppressed, file action policy, subscription cleanup, unchanged default behavior");
    }
}
