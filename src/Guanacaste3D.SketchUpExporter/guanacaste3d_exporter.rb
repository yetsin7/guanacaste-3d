# Guanacaste 3D exporter for SketchUp.
# Registers the extension; its code lives in the guanacaste3d_exporter folder and loads only when enabled.
require 'sketchup.rb'
require 'extensions.rb'

module Guanacaste3D
  module Exporter
    unless file_loaded?(__FILE__)
      extension = SketchupExtension.new('Guanacaste 3D', 'guanacaste3d_exporter/main')
      extension.description = 'Exporta el modelo a Guanacaste 3D (.g3dx): muros, puertas, ventanas y losas se ' \
                              'convierten en elementos editables; los componentes, en familias .g3df.'
      extension.version = '1.0.0'
      extension.creator = 'Guanacaste 3D'
      extension.copyright = 'MIT License'
      Sketchup.register_extension(extension, true)
      file_loaded(__FILE__)
    end
  end
end
