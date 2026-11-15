using HouseFlowChecks;

/// <summary>
/// Builds the house step by step through the real window and prints one line per step. A failing step does not stop the
/// run: the list of failures is the usability defect log for MEJORAS.md section 6.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var driver = HouseDriver.Start();
        var failures = 0;
        foreach (var step in HouseSteps.All)
        {
            string? problem;
            try
            {
                step.Do(driver);
                driver.Pump();
                problem = step.Expect(driver);
            }
            catch (Exception exception)
            {
                problem = (exception.InnerException ?? exception).Message;
            }
            if (problem is not null) failures++;
            Console.WriteLine($"[{(problem is null ? " OK  " : "FALLA")}] {step.Number:00} {step.Revit}" +
                              (problem is null ? "" : $"\n         -> {problem}"));
        }
        driver.Close();
        Console.WriteLine(failures == 0 ? "House flow: all steps passed." : $"House flow: {failures} step(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
