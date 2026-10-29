# ambientCG texture collection

The built-in render texture collection includes 1K JPEG color maps from ambientCG.
The source assets are available under [CC0 1.0](https://docs.ambientcg.com/license/).
The original maps are included without modification. Their source pages are:

- [Wood096](https://ambientcg.com/a/Wood096), [Wood092](https://ambientcg.com/a/Wood092), [Wood051](https://ambientcg.com/a/Wood051)
- [Bricks105](https://ambientcg.com/a/Bricks105), [Bricks113A](https://ambientcg.com/a/Bricks113A)
- [Concrete046](https://ambientcg.com/a/Concrete046), [Concrete034](https://ambientcg.com/a/Concrete034)
- [Tiles141](https://ambientcg.com/a/Tiles141), [Tiles107](https://ambientcg.com/a/Tiles107)
- [Marble012](https://ambientcg.com/a/Marble012), [Plaster001](https://ambientcg.com/a/Plaster001)
- [Ground108](https://ambientcg.com/a/Ground108), [Fabric085](https://ambientcg.com/a/Fabric085)

The renderer currently uses the color map and a relief approximation derived from its luminance.
It does not load ambientCG normal, displacement, or roughness maps. The suggested tile size
uses the source width when available and is an editable approximation otherwise; the current
projection uses one scale for both axes and cannot accurately represent non-square physical tiles.
The material assignment applies to a surface category, rather than to individual elements.
