# Menu, toolbar and commands of the Guanacaste 3D exporter for SketchUp.
require 'sketchup.rb'
require_relative 'exporter'

module Guanacaste3D
  module Exporter
    ICONS = File.join(__dir__, 'icons')

    # Saves the model as a .g3dx file the user chooses.
    def self.export_to_file
      model = Sketchup.active_model
      name = model.title.to_s.empty? ? 'Modelo' : model.title
      folder = model.path.to_s.empty? ? nil : File.dirname(model.path)
      path = UI.savepanel('Exportar a Guanacaste 3D', folder, "#{name}.g3dx")
      return unless path
      path += '.g3dx' unless path.downcase.end_with?('.g3dx')
      writer = run(model, path)
      return unless writer
      UI.messagebox("Se exportaron #{writer.element_count} elementos (#{writer.triangle_count} triángulos) a\n#{path}\n\n" \
                    'Ábralo en Guanacaste 3D (Importar, o arrástrelo a la ventana) para convertirlo en un proyecto editable.')
    end

    # Exports to a temporary file and opens it in Guanacaste 3D.
    def self.open_in_guanacaste
      application = guanacaste_path
      unless application
        UI.messagebox('No se encontró Guanacaste 3D en esta computadora. Ábralo una vez (así queda registrado) o use ' \
                      '«Exportar a Guanacaste 3D» y abra el archivo desde Guanacaste 3D.')
        return
      end
      model = Sketchup.active_model
      folder = File.join(ENV['TEMP'] || Dir.tmpdir, 'Guanacaste3D', 'sketchup')
      FileUtils.mkdir_p(folder)
      name = (model.title.to_s.empty? ? 'Modelo' : model.title).gsub(/[\\\/:*?"<>|]/, '_')
      path = File.join(folder, "#{name}.g3dx")
      return unless run(model, path)
      Process.spawn(application, path.tr('/', '\\'))
    end

    def self.run(model, path)
      writer = Writer.new(model)
      writer.export(path)
      writer
    rescue StandardError => e
      Sketchup.status_text = ''
      UI.messagebox("No se pudo exportar el modelo: #{e.message}")
      nil
    end

    # Guanacaste 3D writes its location to %LOCALAPPDATA%\Guanacaste3D\application-path.txt each time it starts.
    def self.guanacaste_path
      file = File.join(ENV['LOCALAPPDATA'].to_s, 'Guanacaste3D', 'application-path.txt')
      return nil unless File.exist?(file)
      path = File.read(file, encoding: 'bom|utf-8').strip
      File.exist?(path) ? path : nil
    end

    def self.command(text, tip, icon, &block)
      command = UI::Command.new(text, &block)
      command.tooltip = text
      command.status_bar_text = tip
      command.small_icon = File.join(ICONS, "#{icon}-16.png")
      command.large_icon = File.join(ICONS, "#{icon}-24.png")
      command
    end

    unless file_loaded?(__FILE__)
      require 'fileutils'
      require 'tmpdir'
      open = command('Abrir en Guanacaste 3D', 'Exporta el modelo y lo abre en Guanacaste 3D como proyecto editable.', 'open') do
        open_in_guanacaste
      end
      export = command('Exportar a Guanacaste 3D (.g3dx)…', 'Guarda el modelo como archivo de intercambio de Guanacaste 3D.', 'export') do
        export_to_file
      end
      menu = UI.menu('Extensions').add_submenu('Guanacaste 3D')
      menu.add_item(open)
      menu.add_item(export)
      toolbar = UI::Toolbar.new('Guanacaste 3D')
      toolbar.add_item(open)
      toolbar.add_item(export)
      toolbar.restore
      file_loaded(__FILE__)
    end
  end
end
