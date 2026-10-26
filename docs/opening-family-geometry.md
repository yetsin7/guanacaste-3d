# Opening family geometry parameters

Door and window types now carry a frame width and a window light count. The
type editor exposes both parameters. A light count of zero selects the prior
automatic layout (one light for narrow windows, two for wider windows).
Updating a type regenerates every instance's shared 3D mesh in one undoable
transaction. The same type parameters survive project and `.g3df` save/reopen.

The frame width controls the opening lining and window sash members. Light
count changes the actual number of framed glass solids. Validation rejects a
frame or division that would leave a nonpositive or unusably narrow panel.
Existing projects and family files use the previous 60 mm frame and automatic
light layout because the new fields have defaults.

Import compares geometry and material parameters as well as name and size.
If an incoming family type has the same name and size as a project type but
different geometry, import keeps both types with a numeric name suffix. A
second import of the same family reuses that suffix rather than adding a copy.

The current `.g3df` model still represents hosted door and window types. It
does not yet encode arbitrary solids, nested families, texture maps or a
standalone family editing workspace. Renderer and material engine work is
underway in other parts of the repository.

Validation: isolated Release desktop build and full core check suite. The
checks cover three actual glass solids, undo/redo, family and project
serialization, same-name import, repeat import, and invalid geometry. Isolated
validation was needed while concurrent render changes in the shared checkout
temporarily prevented a clean build there.
