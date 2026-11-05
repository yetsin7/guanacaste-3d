using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Core.Geometry;
using Guanacaste3D.Core.Rendering;

if(args.Length<3) throw new ArgumentException("Usage: house-photo-render input.g3d resources output [width=1600] [samples=320] [shot=all]");
var source=Path.GetFullPath(args[0]); var resources=Path.GetFullPath(args[1]); var output=Path.GetFullPath(args[2]);
int width=args.Length>3?int.Parse(args[3]):1600, samples=args.Length>4?int.Parse(args[4]):320;
if(width<320||width>4096||samples<1||samples>4096) throw new ArgumentOutOfRangeException("Use width 320..4096, samples 1..4096.");
var filter=args.Length>5?args[5]:"all";
if(filter is not ("all" or "01" or "02" or "03" or "video")) throw new ArgumentException("Shot must be all, 01, 02, 03 or video.");
if(source==Path.Combine(output,"Casa-terraza-fotografia.g3d")) throw new ArgumentException("The source must not be the output project.");
Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"textures"));
var doc=NativeDocumentStore.LoadProject(source).Clone();
// This scene recipe deliberately targets the reference house, not arbitrary user buildings.
if(!doc.Name.StartsWith("Casa terraza")) throw new ArgumentException("Expected the Casa terraza reference project.");
var g=doc.Levels.Single(l=>l.Role==LevelRole.BaseFloor).Id; var r=doc.Levels.Single(l=>l.Role==LevelRole.BaseRoof).Id;
var site=doc.Levels.Single(l=>l.Name=="Jardín").Id;
var session=new DocumentSession(doc);
Guid Family(string file){var family=NativeDocumentStore.LoadFamily(Path.Combine(resources,"families",file));return session.LoadComponentFamily(family);}
var tree=Family("small-tree.g3df"); var bush=Family("shrub-dense-b.g3df"); var fern=Family("fern.g3df"); var tuft=Family("grass-tuft.g3df");
doc=session.Document;
for(int i=0;i<doc.StructuralElements.Count;i++) {var e=doc.StructuralElements[i]; if(e.Kind==StructuralElementKind.Planting) doc.StructuralElements[i]=e with{FamilyId=e.Height>2?tree:bush,Depth=0};}
// Recess the front-right tree so the stone pier remains visible.
for(int i=0;i<doc.StructuralElements.Count;i++){var e=doc.StructuralElements[i]; if(e.Kind==StructuralElementKind.Planting&&e.Height>2&&e.Start.X>8&&e.Start.Y<1) doc.StructuralElements[i]=e with{Start=new(8.8,5),End=new(8.8,5),Width=1.8,Depth=0,Height=3.2};}
var random=new Random(41);
for(int i=0;i<24;i++){var p=new Point2(6.3+random.NextDouble()*2, .5+random.NextDouble()*2);session.PlaceComponentFamily(site,i%3==0?fern:tuft,p,random.NextDouble()*Math.Tau,1.0);}
// Landscape context is modelled behind the parcel, separate from the house.
foreach(var p in new[]{new Point2(-4,8),new Point2(-2,15),new Point2(3,17),new Point2(9,16),new Point2(13,12)}){
 var id=session.PlaceComponentFamily(site,tree,p); var e=session.Document.StructuralElements.Single(e=>e.Id==id); session.UpdateStructuralElement(id,"Árbol de contexto",5.0,7,0,e.Style);
}
Point2[] Rect(double a,double b,double c,double d)=>[new(a,b),new(c,b),new(c,d),new(a,d)];
var paving=ArchitecturalLibrary.SlabPresets.Single(p=>p.Name=="Concrete Slab 150 mm");
for(int x=0;x<9;x++)for(int y=0;y<3;y++) session.AddSlabFromPreset(site,Rect(x+.012,-3+y+.012,x+.988,-2+y-.012),paving,.01);
// Warm fixtures are native components, so the render lights have visible sources.
var lights=new List<RenderLight>();
foreach(var item in new[]{("Two Seat Sofa",4.7,9.8,0.0),("Coffee Table",4.7,8.5,0.0),("Lounge Chair",3.3,8.1,1.0),("Bistro Table",7.2,5.0,0.0),("Dining Chair",7.2,4.2,Math.PI),("Dining Chair",7.2,5.8,0.0)})
 session.AddElementFromPreset(r,ArchitecturalLibrary.ElementPresets.Single(p=>p.Name==item.Item1),new(item.Item2,item.Item3),new(item.Item2,item.Item3),item.Item4);
var soffit=ArchitecturalLibrary.SlabPresets.Single(p=>p.Name=="Timber Plank Ceiling 20 mm");
var woodCeiling=session.AddSlabFromPreset(r,Rect(2.85,6.75,6.75,10.45),soffit,2.23);
void Fixture(Guid level,double x,double y,double z,float power){
 var preset=ArchitecturalLibrary.ElementPresets.Single(p=>p.Name=="Ceiling Light") with{Width=.16,Depth=.16,Height=.06,Offset=z-doc.Levels.Single(l=>l.Id==level).Elevation};
 session.AddElementFromPreset(level,preset,new(x,y),new(x,y)); lights.Add(new(new((float)x,(float)y,(float)z-.08f),new Vector3(1,.65f,.34f)*power,.10f));
}
foreach(var p in new[]{(2.7,4.5),(7.0,4.8),(3.0,7.8),(7.0,10.0)}) Fixture(g,p.Item1,p.Item2,3.35,4);
foreach(double x in new[]{3.5,4.6,5.7}) Fixture(g,x,2.5,3.3,1.2f);
foreach(double x in new[]{3.4,5.8}) Fixture(r,x,7.0,5.9,2);
doc=session.Document.Clone();
var resolved=new Dictionary<(Guid?,SurfaceKind),RenderMaterial>();
void Assign(SurfaceKind kind,string? texture=null,string? preset=null,string? tint=null,Guid? id=null,double size=1,double relief=.12){
 var path=preset is null?"stock:":"material:"+preset;
 RenderMaterial material=preset is null?MaterialLibrary.ForKind(kind):MaterialPresets.All.Single(p=>p.Id==preset).Material;
 if(texture is not null){var file=Path.Combine(resources,"textures",texture);var destination=Path.Combine(output,"textures",texture);File.Copy(file,destination,true);path="textures/"+texture;
 using var stream=File.OpenRead(file);var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var bmp=new FormatConvertedBitmap(decoder.Frames[0],PixelFormats.Bgra32,null,0);var pixels=new byte[bmp.PixelWidth*bmp.PixelHeight*4];bmp.CopyPixels(pixels,bmp.PixelWidth*4,0);
 material=MaterialLibrary.Textured(kind,RenderTexture.FromBgra32(bmp.PixelWidth,bmp.PixelHeight,pixels),size,relief);
 }
 material=MaterialTint.Apply(material,tint);resolved[(id,kind)]=material;
 doc.RenderMaterials.RemoveAll(a=>a.Category==kind&&a.ElementId==id);doc.RenderMaterials.Add(new(kind,path){ElementId=id,TextureSize=size,Relief=relief,TintHex=tint});
}
Assign(SurfaceKind.Wall,preset:"nicaragua-fino");Assign(SurfaceKind.InteriorWall,preset:"nicaragua-fino");
Assign(SurfaceKind.OpeningFrame,preset:"nicaragua-dark-aluminium");Assign(SurfaceKind.Glass,preset:"glass-low-iron");
Assign(SurfaceKind.WallWood,"ambientcg-wood051.jpg",size:1.6);Assign(SurfaceKind.DoorLeaf,"ambientcg-wood051.jpg",size:1.6);
Assign(SurfaceKind.WallStone,"stone-paving.png",size:1.2,relief:.45); Assign(SurfaceKind.Floor,"ambientcg-wood051.jpg",id:woodCeiling,size:1.2); Assign(SurfaceKind.Upholstery,preset:"boucle"); Assign(SurfaceKind.Bedding,preset:"linen-white");
foreach(var e in doc.StructuralElements.Where(e=>e.Kind==StructuralElementKind.Railing)) Assign(SurfaceKind.Structure,preset:"nicaragua-dark-aluminium",id:e.Id);
RenderMaterial Resolve(SurfaceKind kind)=>resolved.TryGetValue((null,kind),out var m)?m:MaterialLibrary.ForKind(kind);
RenderMaterial Element(Guid id,SurfaceKind kind)=>resolved.TryGetValue((id,kind),out var m)?m:Resolve(kind);
var shots=new[]{
 new Shot("01-fachada-dorada",new(12,-9,3.1f),new(4.5f,5.0f,2.8f),42,LightingScenario.GoldenHour),
 new Shot("02-atardecer-frontal",new(-4,-10,2.6f),new(4.6f,4.8f,2.8f),42,LightingScenario.Dusk),
 new Shot("03-terraza-y-jardin",new(14,-7,10),new(4.6f,5.5f,2.7f),43,LightingScenario.Sunny)
};
foreach(var shot in shots){var delta=shot.Target-shot.Position;var setup=new RenderSetup{CameraX=shot.Position.X,CameraY=shot.Position.Y,CameraZ=shot.Position.Z,YawDegrees=Math.Atan2(delta.Y,delta.X)*180/Math.PI,PitchDegrees=Math.Atan2(delta.Z,Math.Sqrt(delta.X*delta.X+delta.Y*delta.Y))*180/Math.PI,FieldOfViewDegrees=shot.Fov,Scenario=shot.Scenario,DayOfYear=355,Latitude=10,SolarHour=15.5,Width=width,Height=width*3/4,Quality=RenderQuality.High,Look=ToneMapper.AgX,AutoExposure=true,CorrectVerticals=shot.Scenario!=LightingScenario.Sunny,ArtificialLights=true};doc.RenderScenes.Add(new(Guid.NewGuid(),shot.Name,setup));}
doc.RenderSetup=doc.RenderScenes[^3].Setup;doc.Validate();
var enhanced=Path.Combine(output,"Casa-terraza-fotografia.g3d");NativeDocumentStore.SaveProject(enhanced,doc);var loaded=NativeDocumentStore.LoadProject(enhanced);loaded.Validate();
if(loaded.RenderScenes.Count!=doc.RenderScenes.Count||loaded.ComponentFamilies.Count!=doc.ComponentFamilies.Count||loaded.RenderMaterials.Count!=doc.RenderMaterials.Count)throw new InvalidDataException("Saved scene lost cameras, families or materials.");
File.WriteAllText(Path.Combine(output,"luces-y-tomas.json"),JsonSerializer.Serialize(new{lights=lights.Select(l=>new{X=l.Position.X,Y=l.Position.Y,Z=l.Position.Z,R=l.Intensity.X,G=l.Intensity.Y,B=l.Intensity.Z}),width,samples},new JsonSerializerOptions{WriteIndented=true}));
var mesh=BuildingMesher.Build(doc,includeGround:true,plants:PlantDetail.Render);var builder=new RenderSceneBuilder().AddBuilding(mesh,Resolve,Element);foreach(var l in lights)builder.AddLight(l);var scene=builder.Build();
Console.WriteLine($"Scene: {scene.TriangleCount:N0} triangles, {scene.DroppedTriangles} dropped, {doc.RenderMaterials.Count} materials, {doc.RenderScenes.Count} cameras.");
if(scene.DroppedTriangles!=0)throw new InvalidDataException("Invalid scene triangles.");
if(filter=="video"){
 const int frames=60; int height=width*9/16; Directory.CreateDirectory(Path.Combine(output,"frames"));
 var settings=RenderPresets.Lighting(LightingScenario.GoldenHour,10,355,15.5,RenderQuality.High) with{MaxBounces=4,AdaptiveMinimumSamples=12,UseRadianceCache=false,CloudCover=.12f,Clouds=CloudStyle.Cirrus};
 var tracer=new PathTracer(scene,width,height,new RenderCamera(new(12,-9,3.1f),new(4.5f,5,2.8f),42),settings);
 if(!Mp4VideoWriter.TryCreate(Path.Combine(output,"Casa-terraza.mp4"),width,height,6,out var video,out var problem)) throw new InvalidOperationException(problem);
 using(video){
 for(int frame=0;frame<frames;frame++){
   float t=(float)frame/(frames-1);float ease=t*t*(3-2*t);float angle=(-64+25*ease)*MathF.PI/180;
   var target=new Vector3(4.5f,5,2.8f);var position=new Vector3(4.5f+17*MathF.Cos(angle),5+17*MathF.Sin(angle),3.3f+3.6f*ease);
   tracer.SetCamera(new RenderCamera(position,target,42){CorrectVerticals=true});
   for(int pass=0;pass<samples;pass++)tracer.RenderPass();
   var pixels=tracer.ToBgra32(true,new ImageFinish{AutoExposure=false,ExposureStops=.35f,ToneMapper=ToneMapper.AgX,Sharpen=.12f,Bloom=.05f,Vignette=.02f}); video!.Add(pixels,width,height); if(video.Failed) throw new InvalidOperationException(video.Problem); PngCodec.Save(Path.Combine(output,"frames",$"frame-{frame:D4}.png"),RasterImage.FromBgra32(width,height,pixels));
   Console.WriteLine($"Video frame {frame+1}/{frames}");
 }
 }
 return;
}
foreach(var shot in shots.Where(s=>filter=="all"||s.Name.StartsWith(filter))){
 int height=width*3/4;var settings=RenderPresets.Lighting(shot.Scenario,10,355,15.5,RenderQuality.High) with{MaxBounces=6,AdaptiveMinimumSamples=48,AdaptiveTolerance=.012f,UseRadianceCache=false,CloudCover=.16f,Clouds=CloudStyle.Cirrus,Turbidity=2.4f};
 var camera=new RenderCamera(shot.Position,shot.Target,shot.Fov){CorrectVerticals=shot.Scenario!=LightingScenario.Sunny};
 var tracer=new PathTracer(scene,width,height,camera,settings);var watch=System.Diagnostics.Stopwatch.StartNew();
 for(int pass=0;pass<samples;pass++){tracer.RenderPass();if((pass+1)%16==0||pass==samples-1)Console.WriteLine($"{shot.Name}: {pass+1}/{samples}, {watch.Elapsed.TotalSeconds:F0}s");if((pass+1)%32==0) PngCodec.Save(Path.Combine(output,shot.Name+"-progress.png"),RasterImage.FromBgra32(width,height,tracer.ToBgra32(true,new ImageFinish{AutoExposure=true,ToneMapper=ToneMapper.AgX})));}
 var finish=new ImageFinish{AutoExposure=true,ToneMapper=ToneMapper.AgX,Highlights=-.12f,Shadows=.08f,Vibrance=.12f,Sharpen=.15f,Bloom=.06f,Vignette=.035f,Temperature=shot.Scenario==LightingScenario.Dusk?-.05f:.04f};
 PngCodec.Save(Path.Combine(output,shot.Name+".png"),RasterImage.FromBgra32(width,height,tracer.ToBgra32(true,finish)));Console.WriteLine("SAVED "+shot.Name);
}
internal sealed record Shot(string Name,Vector3 Position,Vector3 Target,float Fov,LightingScenario Scenario);







