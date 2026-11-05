using System.Numerics;
using Guanacaste3D.Core.Navigation;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;
var mesh = new SceneMesh();
mesh.AddQuad(new(-10,-10,0), new(10,-10,0), new(10,10,0), new(-10,10,0), SurfaceKind.Ground);
var path = new CameraPath();
path.Add(new CameraMark(new(-5,-5,2), Vector3.Zero,45,0));
path.Add(new CameraMark(new(5,-5,2), Vector3.Zero,45,1));
var events = new List<TourProgress>();
var folder=Path.Combine(Path.GetTempPath(),"g3d-progress-"+Guid.NewGuid().ToString("N"));
var result = await TourRenderer.RenderAsync(new RenderSceneBuilder().AddBuilding(mesh).Build(),path,new RenderSettings(),64,36,4,3,new ImageFinish(),folder,new DirectProgress(events),CancellationToken.None);
if(result.FramesWritten!=result.TotalFrames || !events.Any(p=>p.Frame==0 && p.Sample==1 && p.Samples==3) || !events.Any(p=>p.Frame==p.Total && p.Sample==0)) throw new Exception("Missing in-frame or final progress");
Console.WriteLine($"PASS: {result.FramesWritten} frames and {events.Count} progress updates, including first sample and completion.");
sealed class DirectProgress(List<TourProgress> events) : IProgress<TourProgress>{ public void Report(TourProgress value) => events.Add(value); }

