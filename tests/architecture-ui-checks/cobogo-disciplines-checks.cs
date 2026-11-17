using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Guanacaste3D.Core;
using Guanacaste3D.Desktop;
using Guanacaste3D.Desktop.Controls;

/// <summary>
/// The engineering and documentation phases of the cobogó house: structure, plumbing, electrical, lighting,
/// ventilation, the drawing set per discipline and the budget, each done with the program's own tools.
/// </summary>
internal static partial class HouseFromScratchChecks
{
    private static IEnumerable<(string Name, Func<string> Action)> CobogoDisciplineSteps()
    {
        // ---------------------------------------------------------------- 4. Structure
        yield return ("4.1 Cimentación corrida bajo los muros de carga", () =>
        {
            ActiveLevel(Ground);
            var before = Count(StructuralElementKind.WallFoundation);
            foreach (var (a, b) in new[] { ((0.15, 0.4), (0.15, 12.0)), ((6.55, -0.4), (6.55, 12.0)), ((15.05, -0.4), (15.05, 12.0)), ((0.15, 11.85), (15.05, 11.85)) })
            {
                App("WallFoundation");
                Click(a.Item1, a.Item2);
                Click(b.Item1, b.Item2);
                Escape();
            }
            Expect(Count(StructuralElementKind.WallFoundation) == before + 4, $"hay {Count(StructuralElementKind.WallFoundation) - before} de 4 cimientos corridos (estado: {Status})");
            return "4 cimientos corridos";
        });
        yield return ("4.2 Zapatas aisladas bajo el pórtico", () =>
        {
            var before = Count(StructuralElementKind.IsolatedFoundation);
            foreach (var (x, y) in new[] { (6.55, -0.4), (15.05, -0.4) })
            {
                App("IsolatedFoundation");
                Click(x, y);
                Escape();
            }
            Expect(Count(StructuralElementKind.IsolatedFoundation) == before + 2, $"hay {Count(StructuralElementKind.IsolatedFoundation) - before} de 2 zapatas (estado: {Status})");
            return "2 zapatas";
        });
        yield return ("4.3 Vigas de entrepiso en el perímetro", () =>
        {
            ActiveLevel(Upper);
            var before = Count(StructuralElementKind.Beam);
            foreach (var (a, b) in new[] { ((0.0, 0.0), (15.2, 0.0)), ((15.2, 0.0), (15.2, 12.0)), ((15.2, 12.0), (0.0, 12.0)), ((0.0, 12.0), (0.0, 0.0)) })
            {
                App("Beam");
                Click(a.Item1, a.Item2);
                Click(b.Item1, b.Item2);
                Escape();
            }
            Expect(Count(StructuralElementKind.Beam) == before + 4, $"hay {Count(StructuralElementKind.Beam) - before} de 4 vigas (estado: {Status})");
            return "4 vigas perimetrales";
        });

        // ---------------------------------------------------------------- 5. Plumbing and drainage
        yield return ("5.1 Aparatos de Planta alta: inodoro, lavamanos y ducha", () =>
        {
            ActiveLevel(Upper);
            var placed = 0;
            foreach (var (name, x, y) in new[] { ("Toilet", 7.2, 11.4), ("Bathroom Vanity", 8.2, 11.5), ("Shower Enclosure", 8.2, 8.2) })
            {
                var before = Doc.StructuralElements.Count;
                Library(entry => entry == name);
                Click(x, y);
                Escape();
                if (Doc.StructuralElements.Count > before) placed++;
            }
            Expect(placed == 3, $"se colocaron {placed} de 3 aparatos");
            return "3 aparatos en el baño";
        });
        yield return ("5.2 Calentador de agua en la lavandería", () =>
        {
            ActiveLevel(Ground);
            var before = Doc.StructuralElements.Count;
            Library(entry => entry == "Water Heater");
            Click(14.5, 5.7);
            Expect(Doc.StructuralElements.Count > before, "no se colocó el calentador");
            return "calentador colocado";
        });
        yield return ("5.3 Medidor de agua en la acera y salida a cloaca al fondo", () =>
        {
            App("PlumbingService");
            Click(16.5, -5.0);
            Escape();
            App("PlumbingService");
            // Space cycles: meter, cistern with pump, elevated tank, sewer outlet.
            for (var press = 0; press < 3; press++) Key(System.Windows.Input.Key.Space);
            Click(16.5, 13.0);
            Escape();
            var kinds = Doc.PlumbingPoints.Select(point => point.Kind).ToList();
            Expect(kinds.Contains(PlumbingPointKind.WaterMeter) && kinds.Contains(PlumbingPointKind.SewerOutlet),
                "faltan medidor o salida a cloaca: " + string.Join(", ", kinds));
            return string.Join(", ", kinds);
        });
        yield return ("5.4 Conectar la plomería de toda la casa", () =>
        {
            App("ConnectPlumbing");
            Expect(Doc.PlumbingSystems.Count >= 2, $"hay {Doc.PlumbingSystems.Count} sistemas de plomería (estado: {Status})");
            Expect(!Status.Contains("no cumplen", StringComparison.Ordinal) || Status.Contains(" 0 aparatos no cumplen", StringComparison.Ordinal),
                "hay aparatos que no cumplen: " + Status);
            return Status;
        });
        yield return ("5.5 Cuadro de plomería", () =>
        {
            App("PlumbingSchedule");
            Expect(Dialogs.Any(dialog => dialog.Contains("PlumbingScheduleWindow")), "no se abrió el cuadro de plomería");
            return "cuadro abierto";
        });

        // ---------------------------------------------------------------- 6. Electrical
        yield return ("6.1 Tablero eléctrico en la lavandería", () =>
        {
            ActiveLevel(Ground);
            App("ElectricalEquipment");
            Click(14.95, 6.6);
            Escape();
            Expect(Doc.ElectricalDevices.Any(device => device.Kind == ElectricalDeviceKind.Panel), $"no se colocó el tablero (estado: {Status})");
            return "tablero colocado";
        });
        yield return ("6.2 Tomacorrientes", () =>
        {
            var before = Doc.ElectricalDevices.Count;
            App("ElectricalDevice");
            foreach (var (x, y) in new[] { (0.35, 4.0), (0.35, 9.0), (6.4, 10.0), (14.9, 10.5), (12.0, 11.7) }) Click(x, y);
            Escape();
            var placed = Doc.ElectricalDevices.Count - before;
            Expect(placed == 5, $"se colocaron {placed} de 5 tomacorrientes (estado: {Status})");
            return "5 tomacorrientes";
        });
        yield return ("6.3 Luminarias de techo por habitación", () =>
        {
            var before = Doc.ElectricalDevices.Count;
            App("LightingFixture");
            foreach (var (x, y) in new[] { (3.2, 6.0), (8.7, 8.5), (12.8, 9.8), (11.8, 6.5), (13.8, 6.5), (10.8, 2.5) }) Click(x, y);
            Escape();
            var placed = Doc.ElectricalDevices.Count - before;
            Expect(placed == 6, $"se colocaron {placed} de 6 luminarias (estado: {Status})");
            return "6 luminarias";
        });
        yield return ("6.4 Apagadores junto a las puertas", () =>
        {
            var before = Doc.ElectricalDevices.Count;
            App("LightSwitch");
            foreach (var (x, y) in new[] { (6.4, 7.0), (10.65, 9.0), (11.0, 8.0) }) Click(x, y);
            Escape();
            var placed = Doc.ElectricalDevices.Count - before;
            Expect(placed == 3, $"se colocaron {placed} de 3 apagadores (estado: {Status})");
            return "3 apagadores";
        });
        yield return ("6.5 Circuitos: tomas y alumbrado desde el tablero; apagador a sus luces", () =>
        {
            var panel = Doc.ElectricalDevices.First(device => device.Kind == ElectricalDeviceKind.Panel).Position;
            App("Wire");
            Click(panel.X, panel.Y);
            foreach (var outlet in Doc.ElectricalDevices.Where(device => device.Kind == ElectricalDeviceKind.Outlet).ToList()) Click(outlet.Position.X, outlet.Position.Y);
            Escape();
            App("Wire");
            Click(panel.X, panel.Y);
            foreach (var light in Doc.ElectricalDevices.Where(device => device.IsLuminaire).ToList()) Click(light.Position.X, light.Position.Y);
            Escape();
            var living = Doc.ElectricalDevices.First(device => device.IsSwitch).Position;
            App("Wire");
            Click(living.X, living.Y);
            Click(3.2, 6.0);
            Escape();
            var unassigned = Doc.ElectricalDevices.Count(device => device.Kind != ElectricalDeviceKind.Panel && !device.IsSwitch && device.CircuitId is null);
            Expect(Doc.ElectricalCircuits.Count >= 2 && unassigned == 0,
                $"{Doc.ElectricalCircuits.Count} circuitos y {unassigned} aparatos sin circuito (estado: {Status})");
            return $"{Doc.ElectricalCircuits.Count} circuitos";
        });
        yield return ("6.6 Cuadro de cargas del tablero", () =>
        {
            string? title = null;
            var rows = 0;
            ExpectDialog("ElectricalPanelWindow", window =>
            {
                title = window.Title;
                rows = Descendants<TextBlock>(window).Count();
            });
            App("PanelSchedule");
            for (var wait = 0; wait < 20 && title is null; wait++) _pump();
            Expect(title is not null, "no se abrió el cuadro de cargas: " + Status);
            return $"{title} ({rows} textos)";
        });

        // ---------------------------------------------------------------- 7. Lighting
        yield return ("7.1 La planta arquitectónica queda limpia y una planta eléctrica muestra los lux de cada habitación", () =>
        {
            ActiveLevel(Ground);
            Invoke("RenderPlan");
            _pump();
            int Lux() => PlanCanvas.Children.OfType<TextBlock>().Count(block => block.Text.Contains("lx", StringComparison.Ordinal));
            var architectural = Lux();
            Expect(architectural == 0, $"la planta arquitectónica muestra {architectural} rótulos de lux encima de los nombres");
            var plan = Doc.Views.First(view => view.Kind == ProjectViewKind.Plan && view.LevelId == LevelId(Ground));
            Invoke("SelectProjectView", plan.Id);
            App("DuplicateView");
            App("ViewDiscipline");
            PickMenu(item => Equals(item.Tag, ViewDiscipline.Electrical));
            ExpectDialog("PromptWindow", dialog => Answer(dialog, $"{Ground} - Cálculo de luz"));
            ExpectDialog("Window", dialog => Answer(dialog, $"{Ground} - Cálculo de luz"));
            Key(System.Windows.Input.Key.F2);
            Invoke("RenderPlan");
            _pump();
            var labels = Lux();
            Expect(labels >= 4, $"la planta eléctrica muestra {labels} rótulos de iluminación; se esperaba uno por habitación con luminaria");
            Invoke("SelectProjectView", plan.Id);
            return $"arquitectónica sin lux; eléctrica con {labels} habitaciones calculadas";
        });
        // ---------------------------------------------------------------- 8. Ventilation and air conditioning
        yield return ("8.1 Extracción del medio baño: rejilla y ducto al exterior", () =>
        {
            var terminals = Doc.StructuralElements.Count;
            App("AirTerminal");
            Click(11.8, 6.5);
            Escape();
            App("Duct");
            Click(11.8, 6.5);
            Click(11.8, 4.9);
            Escape();
            Expect(Doc.StructuralElements.Count >= terminals + 2, $"se crearon {Doc.StructuralElements.Count - terminals} de 2 elementos (rejilla y ducto) (estado: {Status})");
            return "rejilla y ducto";
        });
        yield return ("8.2 Aire acondicionado minisplit en la sala", () =>
        {
            var before = Doc.ElectricalDevices.Count(device => device.Kind == ElectricalDeviceKind.MiniSplit);
            App("MechanicalEquipment");
            Click(0.35, 6.0);
            Escape();
            Expect(Doc.ElectricalDevices.Count(device => device.Kind == ElectricalDeviceKind.MiniSplit) > before, $"no se colocó el minisplit (estado: {Status})");
            return "minisplit en la sala";
        });

        // ---------------------------------------------------------------- 9. Documentation
        yield return ("9.1 Cotas automáticas en las dos plantas", () =>
        {
            var before = Doc.Dimensions.Count;
            foreach (var level in new[] { Ground, Upper })
            {
                ActiveLevel(level);
                App("AlignedDimension");
            }
            Expect(Doc.Dimensions.Count > before, "no se crearon cotas");
            return $"{Doc.Dimensions.Count - before} cotas";
        });
        yield return ("9.2 Etiquetar puertas, ventanas y habitaciones", () =>
        {
            ActiveLevel(Ground);
            App("TagAll");
            Arch(ArchitectureCommand.TagAllRooms);
            var tags = PlanCanvas.Children.OfType<Border>().Count();
            Expect(tags >= 6, $"el plano muestra {tags} etiquetas");
            return $"{tags} etiquetas en Planta baja";
        });
        yield return ("9.3 Secciones A-A y B-B", () =>
        {
            var before = Doc.Views.Count(view => view.Kind == ProjectViewKind.Section && view.Cut is not null);
            foreach (var (a, b) in new[] { ((-1.5, 2.8), (16.7, 2.8)), ((9.6, -2.5), (9.6, 13.0)) })
            {
                ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
                _pump();
                App("Section");
                Click(a.Item1, a.Item2);
                Click(b.Item1, b.Item2);
            }
            var after = Doc.Views.Count(view => view.Kind == ProjectViewKind.Section && view.Cut is not null);
            Expect(after == before + 2, $"se crearon {after - before} de 2 secciones");
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            return "2 secciones";
        });
        foreach (var (discipline, word) in new[] { ("Estructural", "Estructural"), ("Eléctrica", "Eléctrica"), ("Iluminación", "Iluminación"),
                     ("Hidrosanitaria", "Hidrosanitaria"), ("Ventilación", "Ventilación") })
        {
            yield return ($"9.4 Vista de planta {discipline} (duplicar vista y disciplina)", () =>
            {
                ActiveLevel(Ground);
                var groundPlan = Doc.Views.First(view => view.Kind == ProjectViewKind.Plan && view.LevelId == LevelId(Ground));
                Invoke("SelectProjectView", groundPlan.Id);
                var plans = Doc.Views.Count(view => view.Kind == ProjectViewKind.Plan);
                App("DuplicateView");
                Expect(Doc.Views.Count(view => view.Kind == ProjectViewKind.Plan) == plans + 1,
                    "no hay forma de duplicar la vista de planta para hacer la planta " + discipline);
                var copy = Doc.Views.Single(view => view.Id == (Guid?)typeof(MainWindow).GetField("_selectedViewId", Private)!.GetValue(_window));
                var target = discipline switch
                {
                    "Estructural" => ViewDiscipline.Structural, "Eléctrica" or "Iluminación" => ViewDiscipline.Electrical,
                    "Hidrosanitaria" => ViewDiscipline.Plumbing, _ => ViewDiscipline.Mechanical
                };
                App("ViewDiscipline");
                PickMenu(item => Equals(item.Tag, target));
                ExpectDialog("PromptWindow", dialog => Answer(dialog, $"{Ground} - {word}"));
                ExpectDialog("Window", dialog => Answer(dialog, $"{Ground} - {word}"));
                Key(System.Windows.Input.Key.F2);
                var view = Doc.Views.Single(candidate => candidate.Id == copy.Id);
                Expect(view.Discipline == target, $"la vista quedó con disciplina {view.Discipline}, no {target}");
                Expect(view.Name == $"{Ground} - {word}", $"F2 no renombró la vista: se llama «{view.Name}»");
                var dimmed = PlanCanvas.Children.OfType<FrameworkElement>().Count(element => element.Opacity < 0.5);
                Expect(dimmed > 0, "la planta de disciplina no atenúa lo de otras disciplinas");
                return $"«{view.Name}» ({target}), {dimmed} elementos atenuados";
            });
        }
        yield return ("9.5 Láminas por disciplina con sus vistas", () =>
        {
            // Engineering sheets carry their tables and the symbol legend next to the plan, as a permit set needs.
            var wanted = new (string Number, string Name, string[] Views)[]
            {
                ("A-101", "Planta baja", [Ground]), ("A-102", "Planta alta", [Upper]), ("A-201", "Alzados", ["Vista Norte"]),
                ("A-301", "Secciones", ["Sección"]), ("S-101", "Cimentaciones", ["Estructural"]),
                ("E-101", "Eléctrica", ["Eléctrica", "Cuadro de cargas", "Simbología"]), ("I-101", "Iluminación", ["Iluminación", "Simbología"]),
                ("H-101", "Hidrosanitaria", ["Hidrosanitaria", "Cuadro hidrosanitario", "Simbología"]), ("M-101", "Ventilación", ["Ventilación", "Cuadro de ventilación"]),
                ("P-001", "Presupuesto", ["Presupuesto de obra"])
            };
            var made = new List<string>();
            var missing = new List<string>();
            foreach (var (number, name, views) in wanted)
            {
                App("Sheet");
                var sheet = Doc.Sheets[^1];
                var added = 0;
                ExpectDialog("SheetEditorWindow", dialog =>
                {
                    ((TextBox)dialog.GetType().GetField("_name", Private)!.GetValue(dialog)!).Text = name;
                    ((TextBox)dialog.GetType().GetField("_number", Private)!.GetValue(dialog)!).Text = number;
                    var picker = (ComboBox)dialog.GetType().GetField("_views", Private)!.GetValue(dialog)!;
                    foreach (var view in views)
                    {
                        var choice = picker.Items.Cast<object>().FirstOrDefault(item =>
                            item.GetType().GetProperty("Label")?.GetValue(item)?.ToString()?.Contains(view, StringComparison.OrdinalIgnoreCase) == true);
                        if (choice is null) continue;
                        picker.SelectedItem = choice;
                        Call(dialog, "AddView_Click");
                        added++;
                    }
                    Call(dialog, "Apply_Click");
                });
                typeof(MainWindow).GetField("_selectedSheetId", Private)!.SetValue(_window, (Guid?)sheet.Id);
                Invoke("EditSheet_Click", _window, new RoutedEventArgs());
                var updated = Doc.Sheets.Single(candidate => candidate.Id == sheet.Id);
                var placed = updated.Viewports.Select(viewport => Doc.Views.FirstOrDefault(view => view.Id == viewport.ViewId)?.Name
                    ?? Doc.Schedules.FirstOrDefault(schedule => schedule.Id == viewport.ViewId)?.Name ?? "").ToList();
                if (added == views.Length && updated.Number == number &&
                    views.All(view => placed.Any(name => name.Contains(view, StringComparison.OrdinalIgnoreCase)))) made.Add(number);
                else missing.Add($"{number} ({added} de {views.Length} vistas: {string.Join(", ", views)})");
            }
            Expect(missing.Count == 0, $"láminas completas: {string.Join(", ", made)}; incompletas: {string.Join(", ", missing)}");
            return string.Join(", ", made);
        });
        yield return ("9.6 Exportar el juego de planos a PDF", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "casa-cobogo-" + Guid.NewGuid().ToString("N") + ".pdf");
            var exported = Doc.Sheets.Count;
            var bytes = Guanacaste3D.Core.Interop.PdfPlanExporter.ExportDocumentation(Doc, SheetPaperSize.A1, DateTimeOffset.Now);
            File.WriteAllBytes(path, bytes);
            var text = System.Text.Encoding.Latin1.GetString(bytes);
            var pages = System.Text.RegularExpressions.Regex.Matches(text, "/Type\\s*/Page[^s]").Count;
            File.Delete(path);
            Expect(pages >= exported, $"el PDF tiene {pages} páginas para {exported} láminas");
            return $"PDF de {pages} páginas";
        });

        yield return ("9.7 Tablas de puertas, ventanas y habitaciones con los botones de Tablas", () =>
        {
            var before = Doc.Schedules.Count;
            var report = new List<string>();
            ExpectDialog("SchedulesWindow", window =>
            {
                var box = (System.Windows.Controls.ComboBox)window.FindName("NewCategoryBox");
                var grid = (System.Windows.Controls.DataGrid)window.FindName("PreviewGrid");
                var create = window.GetType().GetMethod("NewSchedule_Click", Private)!;
                foreach (var category in new[] { ElementCategory.Doors, ElementCategory.Windows, ElementCategory.Rooms })
                {
                    box.SelectedValue = category;
                    create.Invoke(window, [null, new RoutedEventArgs()]);
                    window.UpdateLayout(); _pump();
                    report.Add($"{category}: {grid.Items.Count} filas; título «{((System.Windows.Controls.TextBlock)window.FindName("PreviewTitle")).Text}»; campos {Doc.Schedules.Last().Fields.Count}");
                    Expect(grid.Items.Count > 0, $"la tabla de {category} sale vacía: " + report[^1]);
                }
            });
            App("Schedules");
            Expect(Doc.Schedules.Count == before + 3, $"se esperaban 3 tablas nuevas y hay {Doc.Schedules.Count - before}");
            return string.Join(", ", report);
        });

        foreach (var step in CobogoFamilySteps()) yield return step;

        // ---------------------------------------------------------------- 10. Budget and file
        yield return ("10.1 Presupuesto de obra de la casa", () =>
        {
            App("MaterialBudget");
            var budget = ConstructionBudget.Build(Doc, Session.GetBudgetSettings());
            var trades = budget.ByCategory().Select(trade => trade.Category).ToList();
            Expect(trades.Contains(MaterialCategory.Masonry) && trades.Contains(MaterialCategory.Structure) && trades.Contains(MaterialCategory.Drywall) &&
                   trades.Contains(MaterialCategory.DoorsAndWindows) && trades.Contains(MaterialCategory.Foundations),
                "faltan rubros: " + string.Join(", ", Enum.GetValues<MaterialCategory>().Except(trades)));
            // Stairs, guards, fixtures and the electrical and ventilation work are bought too, not only the shell.
            Expect(trades.Contains(MaterialCategory.StairsAndGuards) && trades.Contains(MaterialCategory.Electrical) &&
                   trades.Contains(MaterialCategory.Plumbing),
                "el presupuesto no cuenta escaleras/barandas, instalación eléctrica o sanitarios: " + string.Join(", ", trades));
            // The table, chairs and lamp made in the Family editor are bought too: one line per family, a piece each.
            var furniture = budget.Lines.Where(line => line.Takeoff.Material.Category == MaterialCategory.Furnishings).ToList();
            var pieces = furniture.Sum(line => line.Takeoff.PurchaseQuantity);
            Expect(furniture.Count >= 3 && pieces >= 6 && furniture.All(line => line.Total > 0),
                $"el presupuesto trae {furniture.Count} muebles propios ({pieces} piezas)");
            return $"₡{budget.Total:#,0} en {trades.Count} rubros, {pieces} muebles propios";
        });
        yield return ("10.2 Guardar y volver a abrir", () =>
        {
            var path = Path.Combine(Path.GetTempPath(), "casa-cobogo-" + Guid.NewGuid().ToString("N") + ProjectFile.NativeExtension);
            typeof(MainWindow).GetField("_filePath", Private)!.SetValue(_window, path);
            Expect((bool)Invoke("SaveProject", false)!, "no se guardó");
            var counts = Counts(Doc) + $", {Doc.ElectricalDevices.Count} eléctricos, {Doc.PlumbingPoints.Count} plomería, {Doc.Sheets.Count} láminas";
            Invoke("OpenProjectPath", path);
            string Now() => Counts(Doc) + $", {Doc.ElectricalDevices.Count} eléctricos, {Doc.PlumbingPoints.Count} plomería, {Doc.Sheets.Count} láminas";
            for (var wait = 0; wait < 60 && Now() != counts; wait++) _pump();
            File.Delete(path);
            Expect(Now() == counts, $"al reabrir cambió: {counts} → {Now()}");
            return counts;
        });

        // ---------------------------------------------------------------- 13. Site: lawn, driveway, street and planting
        yield return ("13.1 Césped, acceso de la cochera, acera y calle con el botón Suelo", () =>
        {
            ActiveLevel(Ground);
            Zoom(14, 6, -9);
            var before = Doc.Floors.Count;
            SketchFloor("Lawn 150 mm", (-6, -5.6), (6.4, -5.6), (6.4, -0.4), (0, -0.4), (0, 0.4), (-6, 0.4));
            SketchFloor("Lawn 150 mm", (15.2, -5.6), (22, -5.6), (22, 12), (15.2, 12));
            SketchFloor("Concrete Slab 150 mm", (6.4, -5.6), (15.2, -5.6), (15.2, -0.4), (6.4, -0.4));
            SketchFloor("Concrete Slab 150 mm", (-6, -7.85), (22, -7.85), (22, -5.6), (-6, -5.6));
            SketchFloor("Asphalt Road 100 mm", (-6, -14), (22, -14), (22, -7.85), (-6, -7.85));
            Expect(Doc.Floors.Count == before + 5, $"se crearon {Doc.Floors.Count - before} de 5 superficies de terreno (estado: {Status})");
            SaveWindow($"{Evidence}/13-terreno.png");
            return "césped, acceso, acera y calle";
        });
        yield return ("13.2 Árboles y arbustos desde la biblioteca", () =>
        {
            var placed = 0;
            foreach (var (name, x, y) in new[] { ("Broadleaf Tree 8 m", 3.4, -2.6), ("Broadleaf Tree 8 m", -3.0, -3.5), ("Broadleaf Tree 8 m", 19.0, -3.0),
                         ("Shrub 1.5 m", 6.2, -4.9), ("Shrub 1.5 m", 6.3, -3.6), ("Shrub 1.5 m", 1.2, -1.0), ("Shrub 1.5 m", -0.6, -0.4) })
            {
                var before = Doc.StructuralElements.Count;
                Library(entry => entry == name);
                Click(x, y);
                if (Doc.StructuralElements.Count > before) placed++;
                Escape();
            }
            Expect(placed == 7, $"se colocaron {placed} de 7 plantas");
            SaveWindow($"{Evidence}/13-plantas.png");
            return "3 árboles y 4 arbustos";
        });

        yield return ("13.3 Nada estorba frente a las puertas (columnas, muebles, sanitarios y escalera)", () =>
        {
            var found = DoorClearance.Analyze(Doc);
            var columns = found.Count(item => Doc.Columns.Any(column => column.Id == item.ObstructionId));
            var names = found.Where(item => Doc.Columns.All(column => column.Id != item.ObstructionId))
                .Select(item => Doc.StructuralElements.FirstOrDefault(element => element.Id == item.ObstructionId)?.Name ?? "?").Distinct().ToList();
            Expect(found.Count == 0, $"{found.Count} estorbos frente a puertas ({columns} columnas; otros: {string.Join(", ", names)})");
            return "todas las puertas tienen su zona libre";
        });

        yield return ("13.4 Revisar advertencias: estado de diseño de la casa", () =>
        {
            var warnings = ModelWarnings.Analyze(Doc);
            var summary = warnings.GroupBy(warning => warning.Code).OrderBy(group => group.Key).Select(group => $"{group.Key}×{group.Count()}").ToList();
            // The design rules must stay clean for doors; anything else is listed so each run shows what is left to design.
            Expect(!warnings.Any(warning => warning.Code == "ObstructedDoor"), "hay puertas obstruidas");
            return summary.Count == 0 ? "sin advertencias" : string.Join(", ", summary);
        });

        // ---------------------------------------------------------------- 12. Render inside the program
        yield return ("12.1 Botón «Ir a Render» de la cinta abre el Render integrado con la casa", () =>
        {
            ((TabControl)_window.FindName("ViewTabs")).SelectedIndex = 0;
            _pump();
            App("ShowRender");
            var tabs = (TabControl)_window.FindName("ViewTabs");
            Expect(tabs.SelectedItem == _window.FindName("RenderTab"), "el botón Ir a Render no abrió la pestaña Render");
            for (var wait = 0; wait < 40; wait++) { _pump(); System.Threading.Thread.Sleep(100); }
            SaveWindow($"{Evidence}/12-render-panel.png");
            return "pestaña Render abierta";
        });
        yield return ("12.2 Iniciar render de la casa y esperar la imagen", () =>
        {
            ((ComboBox)((FrameworkElement)_window.FindName("RenderPanelControl")).FindName("QualityPicker")).SelectedIndex = 1;
            // The real application runs under Dispatcher.Run, which installs this context; the harness pumps by hand.
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
            App("StartRender");
            var panel = (Guanacaste3D.Desktop.Controls.RenderPanel)_window.FindName("RenderPanelControl");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var banner = (UIElement)panel.FindName("RenderCompletedBanner");
            while (clock.Elapsed.TotalSeconds < 120 && banner.Visibility != Visibility.Visible) { _pump(); System.Threading.Thread.Sleep(100); }
            SaveWindow($"{Evidence}/12-render-final.png");
            var status = ((TextBlock)panel.FindName("StatusText")).Text;
            Expect(banner.Visibility == Visibility.Visible, $"el render no terminó en {clock.Elapsed.TotalSeconds:0} s: {status}");
            return $"render listo en {clock.Elapsed.TotalSeconds:0} s: {status}";
        });
    }
}
