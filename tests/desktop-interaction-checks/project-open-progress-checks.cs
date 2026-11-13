using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;

internal static partial class InteractionChecks
{
    /// <summary>Checks that a slow load can be cancelled without publishing its eventual result.</summary>
    private static void ProjectOpenProgressChecks(DocumentSession session)
    {
        var original = session.Document;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var returned = new ManualResetEventSlim();
        var dialogType = typeof(MainWindow).Assembly.GetType("Guanacaste3D.Desktop.ProjectOpenProgressWindow")!;
        /// <summary>Blocks the reader until cancellation has been exercised.</summary>
        BuildingDocument LoadAfterRelease()
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(20));
            var document = BuildingDocument.Create("Discarded project");
            returned.Set();
            return document;
        }
        var dialog = (Window)Activator.CreateInstance(dialogType,
            [_window, "Loading", "Loading project", "Cancel", (Func<BuildingDocument>)LoadAfterRelease])!;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        var attempts = 0;
        var hadProgress = false;
        timer.Tick += (_, _) =>
        {
            if (!entered.IsSet && ++attempts < 200) return;
            timer.Stop();
            var content = (StackPanel)dialog.Content;
            hadProgress = content.Children.OfType<ProgressBar>().Any(bar => bar.IsIndeterminate);
            content.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
        timer.Start();
        dialog.ShowDialog();
        release.Set();
        var completed = returned.Wait(TimeSpan.FromSeconds(20));
        Pump();
        Check(completed && entered.IsSet && hadProgress && dialogType.GetProperty("LoadedDocument")!.GetValue(dialog) is null &&
              ReferenceEquals(session.Document, original),
            "Cancelling an active project load discards its result and preserves the current document");

        var expected = BuildingDocument.Create("Loaded project");
        var success = (Window)Activator.CreateInstance(dialogType,
            [_window, "Loading", "Loading project", "Cancel", (Func<BuildingDocument>)(() => expected)])!;
        success.ShowDialog();
        Check(ReferenceEquals(dialogType.GetProperty("LoadedDocument")!.GetValue(success), expected),
            "A completed project load returns its validated document");
        var failed = (Window)Activator.CreateInstance(dialogType,
            [_window, "Loading", "Loading project", "Cancel", (Func<BuildingDocument>)(() => throw new InvalidDataException("Invalid project."))])!;
        failed.ShowDialog();
        Check(dialogType.GetProperty("LoadError")!.GetValue(failed) is InvalidDataException &&
              dialogType.GetProperty("LoadedDocument")!.GetValue(failed) is null,
            "A failed project load reports its error without publishing a document");
    }
}
