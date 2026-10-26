# Workspace Interaction Contract

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
|---|---|---|---|---|
| Command icons | CommandIcons and icon generator | design.md | compact / large / menu | architecture-ui-checks |
| Ribbon navigation | RibbonScroll | theme.xaml | fit / horizontal scrolling | architecture-ui-checks |
| Scrollbar | WPF ScrollViewer styles | theme.xaml | ribbon / settings panel | architecture-ui-checks |
| Select/Listbox | WPF ComboBox and ListBox | theme.xaml | native desktop selection | desktop-interaction-checks |
| Status feedback | MainWindow.StatusText and RenderPanel.StatusText | localization resources | command / render progress | desktop-interaction-checks |
| Render navigation | Shared NavigationCamera and SceneNavigator | main-window-raytrace.cs | model / live preview | desktop-interaction-checks |

Commands exposed in several surfaces use the same command identifier and icon artwork. Presentation
commands reveal the shared Render material and walkthrough controls rather than creating separate editors.
Camera changes remain shared between the model and Render. A project containing only in-place family
geometry counts as a modeled scene for framing, live preview, and final rendering.

Every newly supported command must route to a real operation. Unimplemented capabilities remain
identified by the existing availability status until their domain and persistence behavior are implemented.
