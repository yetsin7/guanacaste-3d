using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;
using Microsoft.Win32;

namespace Guanacaste3D.Desktop.Controls;

public partial class RenderPanel
{
    // ---------------------------------------------------------------- walk-through

    /// <summary>Parses the duration of a camera mark within the supported playback range.</summary>
    private float MarkSeconds()
    {
        var text = MarkSecondsInput.Text.Replace(',', '.');
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is > 0.05f and < 600 ? value : 4;
    }

    /// <summary>Stores a valid duration on the currently selected camera mark.</summary>
    private void MarkSeconds_LostFocus(object sender, RoutedEventArgs e) =>
        MarkSecondsInput.Text = MarkSeconds().ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Captures the shared camera as a named step in the current walkthrough.</summary>
    private void AddMark_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null) return;
        var camera = _source.Camera;
        var focus = _source.Query?.Pick(camera.Position, camera.Forward) is { } distance ? Math.Max(1f, distance) : 10f;
        _path.Add(new CameraMark(camera.Position, camera.Position + camera.Forward * focus, camera.FieldOfViewDegrees, MarkSeconds()));
        RefreshMarks();
        MarksList.SelectedIndex = _path.Marks.Count - 1;
    }

    /// <summary>Synchronizes the camera-mark list with the current walkthrough path.</summary>
    private void RefreshMarks()
    {
        var selected = MarksList.SelectedIndex;
        MarksList.Items.Clear();
        for (var i = 0; i < _path.Marks.Count; i++)
            MarksList.Items.Add(UiText.Format("RenderPanel.MarkName", i + 1, i == 0 ? 0 : _path.Marks[i].SecondsFromPrevious));
        MarksList.SelectedIndex = Math.Min(selected, MarksList.Items.Count - 1);
    }

    /// <summary>Updates walkthrough controls for the selected camera mark.</summary>
    private void MarksList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        GoToMarkButton.IsEnabled = RemoveMarkButton.IsEnabled = MarksList.SelectedIndex >= 0;

    /// <summary>Applies the selected camera mark to the shared model and render camera.</summary>
    private void GoToMark_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null || MarksList.SelectedIndex < 0) return;
        var mark = _path.Marks[MarksList.SelectedIndex];
        ApplyCamera(mark.Position, mark.Target, mark.FieldOfViewDegrees);
    }

    /// <summary>Removes the selected walkthrough mark and refreshes its list.</summary>
    private void RemoveMark_Click(object sender, RoutedEventArgs e)
    {
        if (MarksList.SelectedIndex < 0) return;
        _path.RemoveAt(MarksList.SelectedIndex);
        RefreshMarks();
    }

    /// <summary>Updates shared position, orientation, and lens parameters from a walkthrough sample.</summary>
    private void ApplyCamera(Vector3 position, Vector3 target, float fieldOfView)
    {
        if (_source is null) return;
        _source.Camera.Position = position;
        _source.Camera.FieldOfViewDegrees = fieldOfView;
        _source.Camera.LookAt(target);
        OnCameraChanged();
    }

    /// <summary>Starts or stops timed camera-path playback on the UI dispatcher.</summary>
    private void PlayTour_Click(object sender, RoutedEventArgs e)
    {
        if (_tourTimer is not null)
        {
            StopTour();
            return;
        }
        if (_path.Marks.Count < 2)
        {
            StatusText.Text = UiText.Get("RenderPanel.TourNeedsMarks");
            return;
        }
        _tourStart = DateTime.UtcNow;
        PlayTourButton.Content = UiText.Get("RenderPanel.StopTour");
        _tourTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _tourTimer.Tick += (_, _) =>
        {
            var seconds = (float)(DateTime.UtcNow - _tourStart).TotalSeconds;
            var (position, target, fieldOfView) = _path.Sample(seconds);
            ApplyCamera(position, target, fieldOfView);
            if (seconds >= _path.Duration) StopTour();
        };
        _tourTimer.Start();
    }

    /// <summary>Releases the playback timer and restores the walkthrough action label.</summary>
    private void StopTour()
    {
        _tourTimer?.Stop();
        _tourTimer = null;
        PlayTourButton.Content = UiText.Get("RenderPanel.PlayTour");
    }

    /// <summary>Exports a bounded image sequence from the camera path with cancellation and memory checks.</summary>
    private async void RenderTour_Click(object sender, RoutedEventArgs e)
    {
        if (_source is null || _rendering || _renderingTour) return;
        if (_path.Marks.Count < 2)
        {
            StatusText.Text = UiText.Get("RenderPanel.TourNeedsMarks");
            return;
        }
        var dialog = new OpenFolderDialog { Title = UiText.Get("RenderPanel.RenderVideo") };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var scene = await _source.GetSceneAsync();
        if (scene is null) return;

        var quality = Quality();
        var (baseWidth, baseHeight) = SelectedBaseSize();
        var (width, height) = RenderPresets.OutputSize(baseWidth, baseHeight, quality);
        var fit = RenderSafety.FitToMemory(width, height, scene.TriangleCount, scene.EstimatedBytes, RenderSafety.ReadMemory());
        if (!fit.Fits)
        {
            StatusText.Text = UiText.Get("RenderPanel.NoMemory");
            return;
        }
        var fps = int.TryParse((FpsPicker.SelectedItem as ComboBoxItem)?.Tag as string, out var chosen) ? chosen : 30;
        var folder = Path.Combine(dialog.FolderName, SafeName(_source.ProjectName) + "-walkthrough");
        // Video frames need far fewer samples than a still: the next frame is nearly the same picture.
        var samples = Math.Max(12, RenderPresets.Profile(quality).TargetSamples / 4);

        _preview?.Suspend();
        _renderingTour = true;
        _showingFinal = true;
        _cancel = new CancellationTokenSource();
        RenderButton.Content = UiText.Get("RenderPanel.Stop");
        RenderTourButton.IsEnabled = false;
        RenderProgress.Visibility = Visibility.Visible;
        var progress = new Progress<TourProgress>(p =>
        {
            RenderProgress.Value = p.Frame / (double)Math.Max(1, p.Total);
            StatusText.Text = UiText.Format("RenderPanel.TourFrame", p.Frame, p.Total, p.Seconds);
        });
        TourResult? result = null;
        try
        {
            result = await TourRenderer.RenderAsync(scene, _path, LightingFor(quality, ResourceUse()), fit.Width, fit.Height, fps, samples,
                CurrentFinish(), folder, progress, _cancel.Token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusText.Text = UiText.Format("RenderPanel.Failed", ex.Message);
        }
        finally
        {
            _renderingTour = false;
            RenderButton.Content = UiText.Get("RenderPanel.Render");
            RenderTourButton.IsEnabled = true;
            RenderProgress.Visibility = Visibility.Collapsed;
        }
        if (result is null) return;
        StatusText.Text = result.FramesWritten == result.TotalFrames
            ? UiText.Format("RenderPanel.TourDone", result.FramesWritten, result.Folder)
            : result.StoppedForMemory
                ? UiText.Format("RenderPanel.MemoryStop", result.FramesWritten)
                : UiText.Format("RenderPanel.TourStopped", result.FramesWritten, result.Folder);
    }
}
