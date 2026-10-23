namespace Guanacaste3D.Core;

/// <summary>
/// A reusable wall type. Its thickness is a type parameter shared by every
/// associated wall instance. Legacy walls without a TypeId remain independent.
/// </summary>
public sealed record WallType(Guid Id, string Name, double Thickness);
