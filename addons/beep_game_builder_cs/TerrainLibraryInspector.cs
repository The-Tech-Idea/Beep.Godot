using Godot;
using Beep.ECS;

namespace Beep.GameBuilder;

[Tool]
public partial class TerrainLibraryInspector : EditorInspectorPlugin
{
    private readonly BeepGameBuilderPlugin _plugin;
    public TerrainLibraryInspector(BeepGameBuilderPlugin plugin) => _plugin = plugin;
    public override bool _CanHandle(GodotObject obj) => obj is TerrainStructureLayerComponent or TerrainTileRendererComponent or TerrainIsometricAutotileRendererComponent or TerrainLibraryEditSession
        || obj is TileMapLayer layer && layer.GetParent() is TerrainLibraryEditSession;

    public override void _ParseBegin(GodotObject obj)
    {
        if (obj is TerrainStructureLayerComponent structures) { AddStructureControls(structures); return; }
        Node renderer = obj is TerrainLibraryEditSession existing ? existing.GetParent()
            : obj is TileMapLayer workingLayer ? workingLayer.GetParent().GetParent() : (Node)obj;
        var box = new VBoxContainer { Name = "TerrainLibraryControls" };
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        var row = new HBoxContainer();
        var edit = new Button { Name = "BeginTerrainEdit", Text = "Edit", TooltipText = "Create or select the native terrain working layer." };
        var apply = new Button { Name = "ApplyTerrainEdit", Text = "Apply", TooltipText = "Validate and commit terrain changes as one undoable action." };
        var discard = new Button { Name = "DiscardTerrainEdit", Text = "Discard", TooltipText = "Close the working copy without changing live terrain." };
        var theme = EditorInterface.Singleton.GetBaseControl();
        edit.Icon = theme.GetThemeIcon("Edit", "EditorIcons");
        apply.Icon = theme.GetThemeIcon("Apply", "EditorIcons");
        discard.Icon = theme.GetThemeIcon("Close", "EditorIcons");
        void Refresh(string message = "")
        {
            var current = TerrainLibraryEditSession.Find(renderer);
            apply.Disabled = current?.Active != true;
            discard.Disabled = current?.Active != true;
            status.Text = message.Length > 0 ? message : !string.IsNullOrEmpty(current?.Problem) ? current.Problem
                : current?.Active == true ? "Working copy active" : "Live terrain";
        }
        edit.Pressed += () =>
        {
            var session = TerrainLibraryEditSession.GetOrCreate(renderer);
            string problem = session.Active ? "" : session.Begin();
            Refresh(problem);
            if (problem.Length == 0 && session.WorkingLayer is { } working)
            {
                var editor = EditorInterface.Singleton;
                editor.MarkSceneAsUnsaved();
                editor.GetSelection().Clear();
                editor.GetSelection().AddNode(working);
                editor.EditNode(working);
            }
        };
        apply.Pressed += () =>
        {
            var session = TerrainLibraryEditSession.Find(renderer);
            if (session is null) return;
            var transaction = session.PrepareApply();
            if (transaction.Count == 0) { Refresh(session.Problem); return; }
            var history = _plugin.GetUndoRedo();
            history.CreateAction("Apply terrain edits", UndoRedo.MergeMode.Disable, session);
            history.AddDoMethod(session, nameof(TerrainLibraryEditSession.Commit), transaction, true);
            history.AddUndoMethod(session, nameof(TerrainLibraryEditSession.Commit), transaction, false);
            history.CommitAction();
            EditorInterface.Singleton.MarkSceneAsUnsaved();
            Refresh(session.Problem);
        };
        discard.Pressed += () =>
        {
            TerrainLibraryEditSession.Find(renderer)?.Discard();
            EditorInterface.Singleton.MarkSceneAsUnsaved();
            Refresh();
        };
        row.AddChild(edit); row.AddChild(apply); row.AddChild(discard);
        box.AddChild(row); box.AddChild(status);
        AddElevationControls(box, renderer, Refresh);
        var timer = new Timer { WaitTime = 0.25, Autostart = true };
        timer.Timeout += () => { if (GodotObject.IsInstanceValid(renderer)) Refresh(); };
        box.AddChild(timer);
        AddCustomControl(box);
        Refresh();
    }

    private void AddStructureControls(TerrainStructureLayerComponent layer)
    {
        var box = new VBoxContainer { Name = "StructureControls" };
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        var choices = new OptionButton { Name = "StructureChoice" };
        var existing = new OptionButton { Name = "ExistingStructure", TooltipText = "Select a live instance to load its placement ID and anchor." };
        var idField = new LineEdit { Name = "PlacementId", PlaceholderText = "Unique placement ID" };
        var x = new SpinBox { Name = "StructureX", MinValue = -1000000, MaxValue = 1000000, Prefix = "X" };
        var y = new SpinBox { Name = "StructureY", MinValue = -1000000, MaxValue = 1000000, Prefix = "Y" };
        existing.ItemSelected += index =>
        {
            if (index < 1) return;
            string id = existing.GetItemText((int)index);
            if (layer.GetNodeOrNull<Node2D>(new NodePath(id)) is not { } live) return;
            idField.Text = id;
            var anchor = live.GetMeta("terrain_structure_anchor").AsVector2I();
            x.Value = anchor.X; y.Value = anchor.Y;
        };
        Button Command(string name, string text, System.Action action)
        {
            var button = new Button { Name = name, Text = text };
            button.Pressed += () => { action(); status.Text = layer.Problem; EditorInterface.Singleton.MarkSceneAsUnsaved(); };
            box.AddChild(button);
            return button;
        }
        Command("BeginStructureEdit", "Edit Structures", () => layer.BeginStructureEdit());
        box.AddChild(new Label { Text = "Catalog" }); box.AddChild(choices);
        box.AddChild(new Label { Text = "Existing Instance" }); box.AddChild(existing);
        box.AddChild(idField); box.AddChild(x); box.AddChild(y);
        var stage = Command("StageStructure", "Stage", () =>
        {
            if (choices.Selected >= 0) layer.StageStructure(idField.Text, choices.GetItemText(choices.Selected), new Vector2I((int)x.Value, (int)y.Value));
        });
        var apply = Command("ApplyStructures", "Apply", () =>
        {
            if (layer.PendingStructureMove.Count > 0)
            {
                var move = layer.PrepareStructureMoveApply();
                if (move.Count == 0) return;
                var moveHistory = _plugin.GetUndoRedo();
                moveHistory.CreateAction("Move terrain structure", UndoRedo.MergeMode.Disable, layer);
                moveHistory.AddDoMethod(layer, nameof(TerrainStructureLayerComponent.CommitStructureMove), move, true);
                moveHistory.AddUndoMethod(layer, nameof(TerrainStructureLayerComponent.CommitStructureMove), move, false);
                moveHistory.CommitAction();
                return;
            }
            var nodes = layer.PrepareStructureApply();
            if (nodes.Count == 0) return;
            var history = _plugin.GetUndoRedo();
            history.CreateAction("Apply structure additions", UndoRedo.MergeMode.Disable, layer);
            history.AddDoMethod(layer, nameof(TerrainStructureLayerComponent.CommitStructures), nodes, true);
            history.AddUndoMethod(layer, nameof(TerrainStructureLayerComponent.CommitStructures), nodes, false);
            history.CommitAction();
        });
        var moveStage = Command("StageStructureMove", "Stage Move", () =>
            layer.StageStructureMove(idField.Text, new Vector2I((int)x.Value, (int)y.Value)));
        moveStage.TooltipText = "Move the existing placement ID to X/Y on Apply; leaves live art unchanged while staged.";
        var discard = Command("DiscardStructures", "Discard", layer.DiscardStructures);
        box.AddChild(status);
        void Refresh()
        {
            if (!GodotObject.IsInstanceValid(layer)) return;
            void Sync(OptionButton menu, System.Collections.Generic.List<string> names)
            {
                bool same = menu.ItemCount == names.Count;
                for (int i = 0; same && i < names.Count; i++) same = menu.GetItemText(i) == names[i];
                if (same) return;
                string selected = menu.Selected >= 0 ? menu.GetItemText(menu.Selected) : "";
                menu.Clear();
                foreach (string name in names) menu.AddItem(name);
                int index = names.IndexOf(selected);
                if (index >= 0) menu.Select(index);
            }
            var catalog = new System.Collections.Generic.List<string>();
            if (layer.LibraryPack is { } currentPack)
                foreach (var pair in currentPack.Structures)
                    if (pair.Value is not null && currentPack.StructureLayouts.TryGetValue(pair.Key, out var layout) && layout is not null)
                        catalog.Add(pair.Key);
            catalog.Sort(System.StringComparer.Ordinal);
            Sync(choices, catalog);
            var instances = new System.Collections.Generic.List<string> { "Select instance" };
            instances.AddRange(layer.ExistingStructureIds());
            Sync(existing, instances);
            string setup = layer.StructureSetupProblem();
            status.Text = layer.Problem.Length > 0 ? layer.Problem : setup.Length > 0 ? setup
                : layer.PendingStructureMove.Count > 0 ? "Move pending" : layer.StructureEditActive ? "Working copy active" : "Live structures";
            stage.Disabled = !layer.StructureEditActive || choices.ItemCount == 0 || setup.Length > 0;
            moveStage.Disabled = !layer.StructureEditActive;
            apply.Disabled = discard.Disabled = !layer.StructureEditActive;
        }
        var timer = new Timer { WaitTime = 0.25, Autostart = true };
        timer.Timeout += Refresh;
        box.AddChild(timer);
        AddCustomControl(box);
        Refresh();
    }

    private void AddElevationControls(VBoxContainer box, Node renderer, System.Action<string> refresh)
    {
        var pack = renderer is TerrainTileRendererComponent flat ? flat.LibraryPack
            : (renderer as TerrainIsometricAutotileRendererComponent)?.LibraryPack;
        if (pack is null || pack.ElevationValues.Count == 0) return;
        var profile = new OptionButton { Name = "ElevationProfile", TooltipText = "Authored elevation profile; pixel rise is independent of logical elevation." };
        foreach (var pair in pack.ElevationValues)
        {
            int rise = pack.ElevationRisePixels.TryGetValue(pair.Key, out int value) ? value : 0;
            profile.AddItem($"{pair.Key} ({rise}px)");
            profile.SetItemMetadata(profile.ItemCount - 1, pair.Key);
        }
        box.AddChild(new Label { Text = "Elevation Region" });
        box.AddChild(profile);
        var fields = new GridContainer { Columns = 2 };
        SpinBox Number(string name, int value, bool size = false)
        {
            fields.AddChild(new Label { Text = name });
            var input = new SpinBox { MinValue = size ? 1 : -1000000, MaxValue = 1000000, Step = 1, Value = value,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            fields.AddChild(input);
            return input;
        }
        var origin = renderer is TerrainTileRendererComponent square ? square.BoundsOrigin
            : ((TerrainIsometricAutotileRendererComponent)renderer).BoundsOrigin;
        var x = Number("Cell X", origin.X);
        var y = Number("Cell Y", origin.Y);
        var width = Number("Width", 1, true);
        var height = Number("Height", 1, true);
        box.AddChild(fields);
        var stage = new Button { Name = "StageElevation", Text = "Set Elevation", TooltipText = "Stage this region in the working copy. Apply commits it to live cells.",
            Icon = EditorInterface.Singleton.GetBaseControl().GetThemeIcon("Edit", "EditorIcons") };
        stage.Pressed += () =>
        {
            var session = TerrainLibraryEditSession.Find(renderer);
            if (session is null) { refresh("Begin a terrain edit session first."); return; }
            var region = new Rect2I((int)x.Value, (int)y.Value, (int)width.Value, (int)height.Value);
            var patch = session.PrepareElevationPaint(region, profile.GetSelectedMetadata().AsString());
            if (patch.Count == 0) { refresh(session.Problem); return; }
            var history = _plugin.GetUndoRedo();
            history.CreateAction("Paint terrain elevation", UndoRedo.MergeMode.Disable, session);
            history.AddDoProperty(session.WorkingLayer!, "tile_map_data", patch["after"]);
            history.AddUndoProperty(session.WorkingLayer!, "tile_map_data", patch["before"]);
            history.CommitAction();
            EditorInterface.Singleton.MarkSceneAsUnsaved();
            refresh("");
        };
        box.AddChild(stage);
        void RefreshElevation()
        {
            if (!GodotObject.IsInstanceValid(renderer)) return;
            var session = TerrainLibraryEditSession.Find(renderer);
            bool enabled = session?.Active == true && session.Pack == pack;
            stage.Disabled = !enabled;
            profile.Disabled = !enabled;
            foreach (var input in new[] { x, y, width, height }) input.Editable = enabled;
        }
        var timer = new Timer { WaitTime = 0.25, Autostart = true };
        timer.Timeout += RefreshElevation;
        box.AddChild(timer);
        RefreshElevation();
    }
}
