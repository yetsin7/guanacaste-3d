using Guanacaste3D.Core;
using Guanacaste3D.Core.Geometry;
var session=new DocumentSession(ArchitecturalLibrary.CreateProject("Thin ceilings"));
var level=session.Document.Levels[0].Id;
Point2[] boundary=[new(0,0),new(4,0),new(4,4),new(0,4)];
var ceilings=ArchitecturalLibrary.SlabPresets.Where(p=>p.Kind==FloorKind.Ceiling).ToArray();
foreach(var preset in ceilings)session.AddSlabFromPreset(level,boundary,preset,2.8);
var doc=session.Document;doc.Validate();
var loaded=ProjectFile.DeserializeDocument(ProjectFile.SerializeDocument(doc));
if(loaded.Floors.Count!=ceilings.Length || loaded.Floors.Where((f,i)=>f.Thickness!=ceilings[i].Thickness).Any() || BuildingMesher.Build(loaded).TriangleCount==0)throw new Exception("Ceiling roundtrip or mesh failed");
foreach(var kind in new[]{FloorKind.Ceiling,FloorKind.Floor,FloorKind.Roof}){
 double min=kind==FloorKind.Ceiling?.005:.03;
 foreach(double thickness in new[]{min,3.0}){var test=doc.Clone();test.Floors=[doc.Floors[0] with{Kind=kind,Thickness=thickness}];test.Validate();}
 foreach(double thickness in new[]{0.0,-.01,min-.0001,3.01,double.NaN,double.PositiveInfinity}){
  var test=doc.Clone();test.Floors=[doc.Floors[0] with{Kind=kind,Thickness=thickness}];
  bool rejected=false;try{test.Validate();}catch(InvalidDataException){rejected=true;}
  if(!rejected)throw new Exception($"Accepted invalid {kind} thickness {thickness}");
 }
}
Console.WriteLine($"PASS: {ceilings.Length} catalog ceilings, serialization and meshing; floor/roof/ceiling valid boundaries and 18 invalid inputs.");
