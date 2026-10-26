using System.Numerics;
using System.Windows;
using System.Windows.Threading;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Rendering;
using Guanacaste3D.Desktop.Controls;

namespace Guanacaste3D.Desktop;

/// <summary>
/// The camera, scene and navigation shared by the 3D view and the Render tab. Both show the same
/// <see cref="NavigationCamera"/>; the scene is built once in the background, cached, and used by the live preview, the
/// final render, cursor-centered zoom and collisions.
/// </summary>
public partial class MainWindow
{
    private const int Model3DTabIndex = 1;
    private const int RenderTabIndex = 6;

    private readonly NavigationCamera _navigation = NavigationCamera.Frame(new Vector3(-5, -5, 0), new Vector3(5, 5, 3));
    private SceneNavigator? _modelNavigator;
    private readonly LinkedModels _links = new();
    private Task<RenderScene?>? _sceneTask;
    private SceneQuery? _query;
    private int _sceneVersion;
    private (int Version, (Vector3 Min, Vector3 Max) Bounds)? _boundsCache;
    private DispatcherTimer? _sceneDebounce;
    private bool _modelDirty = true;
    private event Action? SceneChanged;
    private event Action? QueryChanged;

    /// <summary>Connects navigation to the 3D view and the render panel; called once from the constructor.</summary>
    private void InitializeNavigationAndRender()
    {
        _modelNavigator = new SceneNavigator(ModelSurface, _navigation)
        {
            Bounds = SceneBounds,
            Pick = (origin, direction) => _query?.Pick(origin, direction)
        };
        _modelNavigator.Changed += UpdateModelCamera;
        RenderPanelControl.Attach(new RenderSource(this));
        InitializeModelSelection();
        InitializeViewTabs();
        InitializeProjectedViewZoom();
        _links.Changed += OnLinksChanged;
        _sceneDebounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(350) };
        _sceneDebounce.Tick += (_, _) =>
        {
            _sceneDebounce.Stop();
            if (ViewTabs.SelectedIndex is Model3DTabIndex or RenderTabIndex) _ = GetSceneAsync();
            SceneChanged?.Invoke();
        };
        ViewTabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, ViewTabs)) return;
            if (ViewTabs.SelectedIndex == Model3DTabIndex)
            {
                if (_modelDirty) RebuildModelVisual();
                else UpdateModelCamera();
            }
            UpdateModelSelection(true);
            if (ViewTabs.SelectedIndex is Model3DTabIndex or RenderTabIndex) _ = GetSceneAsync();
        };
    }

    /// <summary>Adapts the window to what the render panel asks of its host.</summary>
    private sealed class RenderSource(MainWindow window) : IRenderSource
    {
        public NavigationCamera Camera => window._navigation;
        public string ProjectName => window._session.Document.Name;
        public (Vector3 Min, Vector3 Max) SceneBounds() => window.SceneBounds();
        public Task<RenderScene?> GetSceneAsync() => window.GetSceneAsync();
        public SceneQuery? Query => window._query;

        public event Action? SceneChanged
        {
            add => window.SceneChanged += value;
            remove => window.SceneChanged -= value;
        }

        public event Action? QueryChanged
        {
            add => window.QueryChanged += value;
            remove => window.QueryChanged -= value;
        }
    }

    // ---------------------------------------------------------------- scene

    /// <summary>Whether the project has anything to show: model elements or visible linked models.</summary>
    private bool HasSceneContent()
    {
        var document = _session.Document;
        return document.Walls.Count + document.Floors.Count + document.Columns.Count + document.StructuralElements.Count > 0 ||
               _links.Visible().Any();
    }

    /// <summary>Bounds of the model and visible linked models, or a 10 m site for an empty project. Cached per scene version.</summary>
    private (Vector3 Min, Vector3 Max) SceneBounds()
    {
        if (_boundsCache is { } cached && cached.Version == _sceneVersion) return cached.Bounds;
        var document = _session.Document;
        var hasModel = document.Walls.Count + document.Floors.Count + document.Columns.Count + document.StructuralElements.Count > 0;
        var min = new Vector3(-5, -5, 0);
        var max = new Vector3(5, 5, 3);
        if (hasModel) (min, max) = BuildingMesher.Build(document).Bounds();
        if (_links.Bounds() is { } linked)
        {
            (min, max) = hasModel ? (Vector3.Min(min, linked.Min), Vector3.Max(max, linked.Max)) : linked;
        }
        _boundsCache = (_sceneVersion, (min, max));
        return (min, max);
    }

    /// <summary>Marks the cached scene stale; it is rebuilt a moment after edits stop, and only if a view needs it.</summary>
    private void MarkSceneDirty()
    {
        _sceneVersion++;
        _sceneTask = null;
        _query = null;
        _modelDirty = true;
        if (_modelNavigator is not null) _modelNavigator.Collision = null;
        _sceneDebounce?.Stop();
        _sceneDebounce?.Start();
    }

    /// <summary>The current render scene, built on a worker thread and cached until the project changes.</summary>
    private Task<RenderScene?> GetSceneAsync()
    {
        if (_sceneTask is { } existing) return existing;
        if (!HasSceneContent()) return _sceneTask = Task.FromResult<RenderScene?>(null);

        // The BIM mesh is built here (it reads the live document); copying everything into one scene happens off-thread.
        var mesh = BuildingMesher.Build(_session.Document, includeGround: true);
        var links = _links.Visible().ToList();
        var version = _sceneVersion;
        var task = Task.Run<RenderScene?>(() =>
        {
            var builder = new RenderSceneBuilder().AddBuilding(mesh);
            foreach (var (link, loaded) in links) builder.AddModel(loaded.Model, link.Placement);
            var scene = builder.Build();
            MemoryTrim.AfterHeavyBuild();
            return scene.TriangleCount == 0 ? null : scene;
        });
        _sceneTask = task;
        _ = task.ContinueWith(finished =>
        {
            if (finished.IsCompletedSuccessfully && finished.Result is { } scene && version == _sceneVersion)
                Dispatcher.BeginInvoke(() => BuildQuery(scene, version));
        }, TaskScheduler.Default);
        return task;
    }

    /// <summary>Builds ray queries for navigation off-thread, then publishes them on the UI thread if the scene is still current.</summary>
    private void BuildQuery(RenderScene scene, int version)
    {
        _ = Task.Run(() => SceneQuery.Build(scene)).ContinueWith(finished =>
        {
            // Without a query (for example, out of memory) zoom falls back to the scene size and collisions stay off.
            if (!finished.IsCompletedSuccessfully) return;
            var query = finished.Result;
            Dispatcher.BeginInvoke(() =>
            {
                if (version != _sceneVersion) return;
                _query = query;
                if (_modelNavigator is not null) _modelNavigator.Collision = query.Collision();
                QueryChanged?.Invoke();
            });
        }, TaskScheduler.Default);
    }

    // ---------------------------------------------------------------- 3D view

    /// <summary>Places the shared camera so the whole model is in view.</summary>
    private void FrameCameraToScene()
    {
        var (min, max) = SceneBounds();
        var framed = NavigationCamera.Frame(min, max);
        _navigation.Position = framed.Position;
        _navigation.YawDegrees = framed.YawDegrees;
        _navigation.PitchDegrees = framed.PitchDegrees;
        _navigation.FieldOfViewDegrees = framed.FieldOfViewDegrees;
        UpdateModelCamera();
        RenderPanelControl?.Navigator?.FrameAll();
    }

    /// <summary>Restores the three-quarter view of the whole model.</summary>
    private void ResetOrbit_Click(object sender, RoutedEventArgs e) => FrameCameraToScene();

    /// <summary>Shows the photorealistic render view.</summary>
    private void ShowRender_Click(object sender, RoutedEventArgs e) => ViewTabs.SelectedIndex = RenderTabIndex;

    /// <summary>Opens the Render tab and starts a final render with the panel's settings.</summary>
    private void StartRender_Click(object sender, RoutedEventArgs e)
    {
        ViewTabs.SelectedIndex = RenderTabIndex;
        RenderPanelControl.RequestRender();
    }

    /// <summary>Keeps the interactive camera aspect in sync with the viewport.</summary>
    private void ModelViewport_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateModelCamera();

    /// <summary>Stops any render or video before the window closes.</summary>
    private void CancelRender() => RenderPanelControl.CancelAll();

    /// <summary>Whether the pointer is flying through the 3D view or the render preview, so letters mean movement, not shortcuts.</summary>
    private bool IsNavigating3D() => _modelNavigator?.IsActive == true || RenderPanelControl?.Navigator?.IsActive == true;

    /// <summary>Whether a key belongs to flight navigation right now.</summary>
    private bool NavigationOwnsKey(System.Windows.Input.Key key) =>
        _modelNavigator?.OwnsKey(key) == true || RenderPanelControl?.Navigator?.OwnsKey(key) == true;
}
