# Writes a SketchUp model as a Guanacaste exchange file (.g3dx), the same format the Revit add-in writes
# (see docs/revit-import.md). SketchUp has no BIM categories, so each top-level group or component is classified by
# its name, its definition's name and its tag: "muro", "puerta", "ventana", "losa", "mesa"... Thin box-shaped walls
# become native walls, doors and windows inside them become hosted openings, flat floors become slabs, and every
# other object becomes a mesh family shared by all instances of the same component.
require 'json'

module Guanacaste3D
  module Exporter
    METERS = 0.0254 # SketchUp stores inches

    CATEGORIES = [
      [/muro|pared|wall|tabique/i, 'OST_Walls'],
      [/puerta|door|port[oó]n/i, 'OST_Doors'],
      [/ventana|window/i, 'OST_Windows'],
      [/cielo|ceiling|plaf[oó]n|cielorraso/i, 'OST_Ceilings'],
      [/losa|piso|floor|slab|contrapiso|entrepiso/i, 'OST_Floors'],
      [/techo|cubierta|roof|teja/i, 'OST_Roofs'],
      [/escalera|stair/i, 'OST_Stairs'],
      [/baranda|barandal|railing|pasamanos/i, 'OST_Railings'],
      [/columna|column|pilar/i, 'OST_Columns'],
      [/viga|beam/i, 'OST_StructuralFraming'],
      [/inodoro|lavamanos|lavatorio|ducha|toilet|sink|ba[ñn]era|tina|fregadero/i, 'OST_PlumbingFixtures'],
      [/l[aá]mpara|luminaria|luz|light/i, 'OST_LightingFixtures'],
      [/[aá]rbol|tree|planta|plant|palmera|arbusto|shrub/i, 'OST_Planting'],
      [/persona|person|people|carro|auto|car\b|veh[ií]culo/i, 'OST_Entourage'],
      [/mueble|silla|mesa|sof[aá]|cama|chair|table|bed|furniture|armario|closet|escritorio|desk/i, 'OST_Furniture'],
      [/cocina|kitchen|gabinete|cabinet/i, 'OST_Casework']
    ].freeze

    # One element's triangles in its own frame (meters), with a material index per triangle.
    class MeshData
      attr_reader :positions, :indices, :materials

      def initialize
        @positions = []
        @indices = []
        @materials = []
      end

      def empty?
        @materials.empty?
      end

      def add_triangle(a, b, c, material)
        start = @positions.length / 3
        [a, b, c].each { |p| @positions.push(p[0], p[1], p[2]) }
        @indices.push(start, start + 1, start + 2)
        @materials << material
      end

      def bounds
        xs = []
        ys = []
        zs = []
        @positions.each_slice(3) do |x, y, z|
          xs << x
          ys << y
          zs << z
        end
        [[xs.min, ys.min, zs.min], [xs.max, ys.max, zs.max]]
      end

      def to_h
        {
          'positions' => [@positions.pack('e*')].pack('m0'),
          'indices' => [@indices.pack('l<*')].pack('m0'),
          'materials' => [@materials.pack('l<*')].pack('m0')
        }
      end
    end

    class Writer
      attr_reader :element_count, :triangle_count

      def initialize(model)
        @model = model
        @materials = []
        @material_index = {}
        @meshes = []
        @mesh_by_key = {}
        @elements = []
        @next_id = 1
        @element_count = 0
        @triangle_count = 0
      end

      def export(path)
        entities = @model.entities
        total = [entities.length, 1].max
        loose = MeshData.new
        entities.each_with_index do |entity, index|
          Sketchup.status_text = format('Guanacaste 3D: leyendo el modelo… %d %%', 100 * index / total)
          next unless visible?(entity)
          case entity
          when Sketchup::Group, Sketchup::ComponentInstance then visit(entity)
          when Sketchup::Face then add_face(entity, IDENTITY, nil, loose)
          end
        end
        unless loose.empty?
          add_element('Geometría suelta', 'OST_GenericModel', loose, [0.0, 0.0, 0.0], 0.0, 'mesh', '', '')
        end
        infer_walls_and_openings
        levels = build_levels
        write(path, levels)
        Sketchup.status_text = ''
      end

      private

      IDENTITY = Geom::Transformation.new

      def visible?(entity)
        return false if entity.respond_to?(:hidden?) && entity.hidden?
        return false if entity.respond_to?(:layer) && entity.layer && !entity.layer.visible?
        true
      end

      def definition_of(entity)
        entity.respond_to?(:definition) ? entity.definition : entity.entities.parent
      end

      # A top-level group or component: shared by definition when placed upright without scaling, otherwise baked.
      def visit(instance)
        definition = definition_of(instance)
        name = [instance.name, definition.name].reject { |text| text.nil? || text.empty? }.first || 'Objeto'
        tag = instance.layer ? instance.layer.name : ''
        category = classify("#{instance.name} #{definition.name} #{tag}")
        t = instance.transformation
        rigid = rigid?(t)
        origin = rigid ? point_m(t.origin) : [0.0, 0.0, 0.0]
        rotation = rigid ? Math.atan2(t.xaxis.y, t.xaxis.x) : 0.0
        inherited = instance.material
        key = rigid ? "#{definition.entityID}:#{inherited ? inherited.entityID : 0}" : nil
        mesh = MeshData.new
        local = rigid ? IDENTITY : t
        definition.entities.each { |child| collect(child, local, inherited, mesh) if visible?(child) }
        return if mesh.empty?
        family = instance.is_a?(Sketchup::ComponentInstance) ? definition.name : ''
        element = add_element(name, category, mesh, origin, rotation, 'instance', family, tag, key)
        element[:instance] = instance
        element[:transformation] = t
        element[:rigid] = rigid
      end

      def collect(entity, transformation, inherited, mesh)
        case entity
        when Sketchup::Face
          add_face(entity, transformation, inherited, mesh)
        when Sketchup::Group, Sketchup::ComponentInstance
          material = entity.material || inherited
          definition_of(entity).entities.each do |child|
            collect(child, transformation * entity.transformation, material, mesh) if visible?(child)
          end
        end
      end

      def add_face(face, transformation, inherited, mesh)
        material = material_index(face.material || face.back_material || inherited)
        polygon_mesh = face.mesh(0)
        points = polygon_mesh.points.map { |point| point_m(transformation * point) }
        polygon_mesh.polygons.each do |polygon|
          corners = polygon.map { |index| points[index.abs - 1] }
          (1...(corners.length - 1)).each do |i|
            mesh.add_triangle(corners[0], corners[i], corners[i + 1], material)
          end
        end
      end

      def material_index(material)
        key = material ? material.entityID : 0
        return @material_index[key] if @material_index.key?(key)
        entry =
          if material
            color = material.color
            {
              'name' => material.display_name, 'class' => '', 'category' => '',
              'color' => format('#%02X%02X%02X', color.red, color.green, color.blue),
              'transparency' => (1.0 - material.alpha).round(3), 'shininess' => 0.0, 'ownerCategory' => ''
            }
          else
            { 'name' => 'Predeterminado', 'class' => '', 'category' => '', 'color' => '#B4B4B4',
              'transparency' => 0.0, 'shininess' => 0.0, 'ownerCategory' => '' }
          end
        @materials << entry
        @material_index[key] = @materials.length - 1
      end

      def add_element(name, category, mesh, origin, rotation, kind, family, tag, key = nil)
        index = key && @mesh_by_key[key]
        unless index
          @meshes << mesh
          index = @meshes.length - 1
          @mesh_by_key[key] = index if key
          @triangle_count += mesh.materials.length
        end
        element = {
          id: @next_id, name: name, category: category, family: family, tag: tag, kind: kind,
          mesh: index, origin: origin, rotation: rotation, local_mesh: mesh
        }
        @next_id += 1
        @elements << element
        element
      end

      # Upright, not mirrored, not scaled: the definition's coordinates are the instance's own frame.
      def rigid?(t)
        a = t.to_a
        scales = [a[0..2], a[4..6], a[8..10]].map { |column| Math.sqrt(column.sum { |value| value * value }) }
        scales.all? { |scale| (scale - 1.0).abs < 1e-6 } && t.zaxis.z > 0.999 && (t.xaxis * t.yaxis) % t.zaxis > 0
      end

      def classify(text)
        CATEGORIES.each { |pattern, category| return category if text =~ pattern }
        'OST_GenericModel'
      end

      def point_m(point)
        [point.x.to_f * METERS, point.y.to_f * METERS, point.z.to_f * METERS]
      end

      # Frame point → model point.
      def to_world(element, local)
        cos = Math.cos(element[:rotation])
        sin = Math.sin(element[:rotation])
        o = element[:origin]
        [o[0] + local[0] * cos - local[1] * sin, o[1] + local[0] * sin + local[1] * cos, o[2] + local[2]]
      end

      # Thin upright boxes classified as walls become native walls; doors and windows inside them become openings;
      # flat floors become slabs.
      def infer_walls_and_openings
        walls = []
        @elements.each do |element|
          next unless element[:rigid]
          lo, hi = element[:local_mesh].bounds
          size = [hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]]
          case element[:category]
          when 'OST_Walls'
            thin, long = size[0] <= size[1] ? [0, 1] : [1, 0]
            next unless size[thin] >= 0.05 && size[thin] <= 0.6 && size[long] >= 0.3 && size[2] >= 0.3
            middle = (lo[thin] + hi[thin]) / 2
            a = [0.0, 0.0, lo[2]]
            b = [0.0, 0.0, lo[2]]
            a[thin] = middle
            b[thin] = middle
            a[long] = lo[long]
            b[long] = hi[long]
            start = to_world(element, a)
            finish = to_world(element, b)
            material = @materials[element[:local_mesh].materials.group_by(&:itself).max_by { |_, list| list.length }[0]]
            element[:kind] = 'wall'
            element[:wall] = {
              wallKind: 'Basic', width: size[thin], function: 'Exterior',
              start: start[0..1], end: finish[0..1], curved: false,
              baseZ: start[2], height: size[2], bottomZ: start[2], topZ: start[2] + size[2],
              topOffset: 0, profileEdited: false, topAttached: false, baseAttached: false,
              layers: [{ function: 'Structure', width: size[thin], material: material ? material['name'] : '', class: '' }]
            }
            walls << element
          when 'OST_Floors', 'OST_Ceilings'
            slab = top_outline(element) if size[2] <= 0.6
            next unless slab
            element[:kind] = element[:category] == 'OST_Ceilings' ? 'ceiling' : 'floor'
            element[:slab] = { flat: true, loops: slab[:loops], z: slab[:z], thickness: [size[2], 0.03].max }
          end
        end
        @elements.each do |element|
          next unless element[:rigid] && %w[OST_Doors OST_Windows].include?(element[:category])
          lo, hi = element[:local_mesh].bounds
          width = [hi[0] - lo[0], hi[1] - lo[1]].max
          center = to_world(element, [(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]])
          host = walls.find { |wall| inside_wall?(wall[:wall], center) }
          next unless host
          element[:kind] = element[:category] == 'OST_Doors' ? 'door' : 'window'
          element[:opening] = {
            host: host[:id], point: center, width: width, height: hi[2] - lo[2], sillZ: center[2],
            facingFlipped: false, handFlipped: false, mirrored: false
          }
        end
      end

      def inside_wall?(wall, point)
        sx, sy = wall[:start]
        ex, ey = wall[:end]
        length = Math.hypot(ex - sx, ey - sy)
        return false if length < 1e-6
        dx = (ex - sx) / length
        dy = (ey - sy) / length
        along = (point[0] - sx) * dx + (point[1] - sy) * dy
        across = ((point[0] - sx) * -dy + (point[1] - sy) * dx).abs
        along > 0 && along < length && across <= wall[:width] / 2 + 0.15 &&
          point[2] >= wall[:baseZ] - 0.05 && point[2] < wall[:topZ]
      end

      # The largest upward face of a group: its outline and holes in model coordinates.
      def top_outline(element)
        instance = element[:instance]
        t = element[:transformation]
        best = nil
        definition_of(instance).entities.grep(Sketchup::Face).each do |face|
          normal = t * face.normal
          next unless normal.z > 0.999
          best = face if best.nil? || face.area > best.area
        end
        return nil unless best
        loops = [best.outer_loop] + (best.loops - [best.outer_loop])
        {
          loops: loops.map { |loop| loop.vertices.map { |vertex| point_m(t * vertex.position)[0..1] } },
          z: point_m(t * best.vertices.first.position)[2]
        }
      end

      # SketchUp has no levels: one per distinct wall base elevation, at least the ground.
      def build_levels
        elevations = @elements.select { |element| element[:wall] }.map { |element| element[:wall][:baseZ].round(2) }
        elevations = (elevations + [0.0]).uniq.sort
        merged = []
        elevations.each { |z| merged << z if merged.empty? || z - merged.last > 0.5 }
        levels = merged.each_with_index.map { |z, index| { id: index + 1, name: "Nivel #{index + 1}", elevation: z } }
        @elements.each do |element|
          base = element[:wall] ? element[:wall][:baseZ] : element[:origin][2]
          level = levels.select { |candidate| candidate[:elevation] <= base + 0.05 }.max_by { |candidate| candidate[:elevation] } || levels.first
          element[:level] = level[:id]
          element[:wall][:baseLevel] = level[:id] if element[:wall]
        end
        levels
      end

      def write(path, levels)
        elements = @elements.map do |element|
          record = {
            'id' => element[:id], 'uniqueId' => element[:id].to_s, 'category' => element[:category],
            'categoryName' => element[:tag], 'name' => element[:name], 'family' => element[:family],
            'type' => element[:name], 'kind' => element[:kind], 'level' => element[:level],
            'mesh' => element[:mesh], 'origin' => element[:origin], 'rotation' => element[:rotation]
          }
          record['wall'] = element[:wall] if element[:wall]
          record['opening'] = element[:opening] if element[:opening]
          record['slab'] = element[:slab] if element[:slab]
          record
        end
        document = {
          'format' => 'guanacaste-exchange', 'version' => 1,
          'source' => "SketchUp #{Sketchup.version}", 'sourcePath' => @model.path.to_s,
          'projectName' => @model.title.to_s.empty? ? 'Modelo SketchUp' : @model.title,
          'units' => 'm', 'levels' => levels, 'materials' => @materials,
          'meshes' => @meshes.map(&:to_h), 'elements' => elements, 'skipped' => {}
        }
        File.open(path + '.partial', 'wb') { |file| file.write(JSON.generate(document)) }
        File.rename(path + '.partial', path)
        @element_count = elements.length
      end
    end
  end
end
