using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
	/// <summary>
	/// The terrain lab's UI: binds a panel of controls to a
	/// <see cref="TerrainWorldComponent"/> and reports what it built.
	///
	/// THIS IS ONLY UI. It knows about OptionButtons, a status label and a
	/// preview node to pan and zoom. It does not know what a splat renderer is,
	/// which projections exist, how a world is generated or how a map is drawn -
	/// all of that belongs to the world component, so a developer can build a
	/// completely different creation screen on the same component, or none at
	/// all and just configure the node.
	///
	/// It used to be the other way round: the only code that could create a world
	/// lived in a scene controller in tests/examples, welded to one particular
	/// panel. Every demo that wanted a map reimplemented the same three steps.
	///
	/// Split by concern: this file resolves and populates the controls,
	/// .Navigation frames and moves the preview.
	/// </summary>
	[Tool]
	[GlobalClass]
	public partial class TerrainLabComponent : Node
	{
		/// <summary>The world this panel drives. Everything else here is a control.</summary>
		[Export] public NodePath WorldPath { get; set; } = new("");

		/// <summary>The node holding the renderers, panned and zoomed as one.</summary>
		[Export] public NodePath PreviewPath { get; set; } = new("");

		[ExportGroup("Setup Controls")]
		[Export] public NodePath MapTypePath { get; set; } = new("");
		[Export] public NodePath MapSizePath { get; set; } = new("");
		[Export] public NodePath WorldAgePath { get; set; } = new("");
		[Export] public NodePath TemperaturePath { get; set; } = new("");
		[Export] public NodePath RainfallPath { get; set; } = new("");
		[Export] public NodePath SeaLevelPath { get; set; } = new("");
		[Export] public NodePath ResourceLevelPath { get; set; } = new("");
		[Export] public NodePath ResourceSetPath { get; set; } = new("");
		[Export] public NodePath SeedPath { get; set; } = new("");
		[Export] public NodePath ViewPath { get; set; } = new("");

		/// <summary>
		/// The painted projection's art STYLES, in the order the view menu lists them after the four
		/// projections. Each names itself through <see cref="TerrainMapArt.DisplayName"/>.
		///
		/// A list rather than a property per style. This was PixelArtProfile and CartoonProfile, and
		/// the menu text, the index-to-view mapping and its reverse each carried a branch per style -
		/// so a third style meant editing four places and the scene, which is the shape that keeps a
		/// new style out. Adding one is now adding a resource to this array.
		/// </summary>
		[Export] public Godot.Collections.Array<TerrainMapArt> StyleProfiles { get; set; } = new();

		[ExportGroup("Actions")]
		[Export] public NodePath GenerateButtonPath { get; set; } = new("");
		[Export] public NodePath RandomSeedButtonPath { get; set; } = new("");
		[Export] public NodePath ResetViewButtonPath { get; set; } = new("");
		[Export] public NodePath StatusPath { get; set; } = new("");
		[Export] public NodePath DiagnosticsButtonPath { get; set; } = new("");
		/// <summary>
		/// A CanvasItem holding renderers that are map diagnostics in this panel - the
		/// resource icons in the lab - shown only while diagnostics are on. The world
		/// component owns each renderer's own Visible (it sets it per projection on every
		/// draw), so the panel toggles their parent instead of fighting that.
		/// </summary>
		[Export] public NodePath DiagnosticsLayerPath { get; set; } = new("");
		[Export] public NodePath CancelButtonPath { get; set; } = new("");
		[Export] public NodePath GenerationProgressPath { get; set; } = new("");

		// There are deliberately no paths here for relief, rivers, resource
		// density, lake size, beach width, frequency, octaves, landform, or raw
		// width and height. Nineteen such exports and their fields were resolved
		// and never read: the map-setup AXES own those facts, and ApplyMapSetup
		// derives every one of them from the chosen world. Three of the orphans
		// were CheckButtons wired to regenerate, so they looked like working
		// controls while the values they claimed to own were hardcoded.

		[ExportGroup("Preview Navigation")]
		/// <summary>
		/// Low enough for the ISOMETRIC view. Its cells are 111x64 against the
		/// flat view's 64px tile, so a map that fits the panel flat needs roughly
		/// a third of the zoom in isometric - a 96x60 map fits at 0.106, and a
		/// floor of 0.15 meant it simply could not be zoomed out far enough.
		/// </summary>
		[Export] public float MinimumZoom { get; set; } = 0.04f;
		[Export] public float MaximumZoom { get; set; } = 3.0f;
		[Export] public float ZoomStep { get; set; } = 1.15f;

		private TerrainWorldComponent? _world;
		private Node2D? _preview;

		private OptionButton? _mapType;
		private OptionButton? _mapSize;
		private OptionButton? _worldAge;
		private OptionButton? _temperature;
		private OptionButton? _rainfall;
		private OptionButton? _seaLevel;
		private OptionButton? _resourceLevel;
		private OptionButton? _resourceSet;
		private OptionButton? _view;
		private SpinBox? _seed;
		private Label? _status;

		private bool _isPanning;

		public override void _Ready()
		{
			if (Engine.IsEditorHint())
				return;

			ResolveNodes();
			PopulateOptions();
			if (_world is not null)
			{
				_world.WorldBuilt += OnWorldBuilt;
				_world.GenerationProgress += OnGenerationProgress;
				_world.GenerationFinished += OnGenerationFinished;
			}
			if (!CancelButtonPath.IsEmpty && GetNodeOrNull<Button>(CancelButtonPath) is { } cancel)
				cancel.Pressed += () => _world?.CancelGeneration();

			// Reframe when the window changes size. The preview's zoom and
			// position are computed FROM the viewport, so after a resize they
			// describe a viewport that no longer exists - which is why enlarging
			// the window to see more of the map left it off to one side.
			GetViewport().SizeChanged += ResetPreviewView;

			GetNodeOrNull<Button>(GenerateButtonPath)?.Pressed += Generate;
			GetNodeOrNull<Button>(RandomSeedButtonPath)?.Pressed += RandomizeSeed;
			GetNodeOrNull<Button>(ResetViewButtonPath)?.Pressed += ResetPreviewView;
			if (!DiagnosticsButtonPath.IsEmpty
				&& GetNodeOrNull<BaseButton>(DiagnosticsButtonPath) is { } diagnostics)
			{
				SetDiagnostics(diagnostics.ButtonPressed);
				diagnostics.Toggled += SetDiagnostics;
			}

			foreach (OptionButton? axis in new[]
					 { _mapType, _mapSize, _worldAge, _temperature, _rainfall,
					   _seaLevel, _resourceLevel, _resourceSet })
			{
				if (axis is not null)
					axis.ItemSelected += _ => Generate();
			}

			// Changing the projection reframes as well as redraws: they have
			// different footprints and different origins.
			if (_view is not null)
			{
				_view.ItemSelected += _ =>
				{
					if (_world is null) return;
					ApplyView(Selected(_view, SelectedView()));
					_world.Redraw();
					ResetPreviewView();
				};
			}

			if (_world?.BuiltSize.X > 0)
				OnWorldBuilt(_world.BuiltSize);
			else if (_world is not null && !_world.BuildOnReady)
				CallDeferred(nameof(Generate));
		}

		public override void _ExitTree()
		{
			if (Engine.IsEditorHint()) return;
			GetViewport().SizeChanged -= ResetPreviewView;
			if (GodotObject.IsInstanceValid(_world))
			{
				_world!.WorldBuilt -= OnWorldBuilt;
				_world.GenerationProgress -= OnGenerationProgress;
				_world.GenerationFinished -= OnGenerationFinished;
			}
		}

		public override string[] _GetConfigurationWarnings()
		{
			if (WorldPath.IsEmpty)
				return new[] { "WorldPath should point to a TerrainWorldComponent." };
			if (PreviewPath.IsEmpty)
				return new[] { "PreviewPath should point to the Node2D holding the renderers." };
			return System.Array.Empty<string>();
		}

		/// <summary>
		/// Reads the controls onto the world component and builds.
		///
		/// Every line here is a control being copied onto a property. There is
		/// no generation logic to get wrong, because none of it lives here.
		/// </summary>
		public void Generate()
		{
			if (_world is null || _world.IsGenerating)
				return;

			_world.MapType = (TerrainShape)Selected(_mapType, (int)_world.MapType);
			_world.MapSize = (TerrainMapSize)Selected(_mapSize, (int)_world.MapSize);
			_world.WorldAge = (TerrainWorldAge)Selected(_worldAge, (int)_world.WorldAge);
			_world.Temperature = (TerrainTemperature)Selected(_temperature, (int)_world.Temperature);
			_world.Rainfall = (TerrainRainfall)Selected(_rainfall, (int)_world.Rainfall);
			_world.SeaLevel = (TerrainSeaLevel)Selected(_seaLevel, (int)_world.SeaLevel);
			_world.ResourceLevel = (TerrainResourceLevel)Selected(_resourceLevel, (int)_world.ResourceLevel);
			_world.Resources = (ResourceSet)Selected(_resourceSet, (int)_world.Resources);
			ApplyView(Selected(_view, SelectedView()));
			if (_seed is not null)
				_world.Seed = Mathf.Clamp((int)_seed.Value, 0, int.MaxValue);

			if (!_world.BeginNewWorld()) _status?.SetText("Unable to start generation");
		}

		private void OnGenerationProgress(string stage, float fraction)
		{
			SetGenerationBusy(true);
			_status?.SetText(stage);
			if (!GenerationProgressPath.IsEmpty && GetNodeOrNull<ProgressBar>(GenerationProgressPath) is { } bar)
				if (stage != "Cancelling") bar.Value = fraction * 100;
			if (!CancelButtonPath.IsEmpty && GetNodeOrNull<Button>(CancelButtonPath) is { } cancel)
				cancel.Disabled = stage == "Cancelling" || _world?.CanCancelGeneration != true;
		}

		private void OnGenerationFinished(bool success, string message)
		{
			SetGenerationBusy(false);
			if (!success) _status?.SetText(message);
		}

		private void SetGenerationBusy(bool busy)
		{
			foreach (OptionButton? axis in new[] { _mapType, _mapSize, _worldAge, _temperature,
						 _rainfall, _seaLevel, _resourceLevel, _resourceSet, _view })
				if (axis is not null) axis.Disabled = busy;
			if (_seed is not null) _seed.Editable = !busy;
			foreach (NodePath path in new[] { GenerateButtonPath, RandomSeedButtonPath, DiagnosticsButtonPath })
				if (!path.IsEmpty && GetNodeOrNull<BaseButton>(path) is { } button) button.Disabled = busy;
			if (!CancelButtonPath.IsEmpty && GetNodeOrNull<Button>(CancelButtonPath) is { } cancel)
				cancel.Visible = busy;
			if (!GenerationProgressPath.IsEmpty && GetNodeOrNull<ProgressBar>(GenerationProgressPath) is { } bar)
				bar.Visible = busy;
		}

		private void OnWorldBuilt(Vector2I size)
		{
			_ = size;
			if (_world is null) return;
			SetGenerationBusy(false);
			_status?.SetText(_world.StatusLine());

			// Preserve pan/zoom for same-size regeneration, but fit changed map extents.
			if (!_hasFramedPreview || _world.PreviewExtent() != _framedExtent)
				ResetPreviewView();
		}

		private void SetDiagnostics(bool enabled)
		{
			if (!DiagnosticsLayerPath.IsEmpty && GetNodeOrNull<CanvasItem>(DiagnosticsLayerPath) is { } layer)
				layer.Visible = enabled;
			if (_world is null || _world.MapOverlayPath.IsEmpty) return;
			var overlay = _world.GetNodeOrNull<TerrainMapOverlayComponent>(_world.MapOverlayPath);
			if (overlay is null) return;
			overlay.ShowStartPositions = enabled;
			overlay.ShowUndergroundResources = enabled;
			if (_world.BuiltSize.X > 0) overlay.Rebuild();
		}

		/// <summary>A chooser's selection, or the world's current value when it is absent.</summary>
		private static int Selected(OptionButton? option, int fallback)
			=> option is null ? fallback : Mathf.Max(0, option.Selected);

		private void RandomizeSeed()
		{
			if (_seed is null)
				return;

			_seed.Value = GD.Randi() % int.MaxValue;
			Generate();
		}

		private void ResolveNodes()
		{
			_world = GetNodeOrNull<TerrainWorldComponent>(WorldPath);
			_preview = GetNodeOrNull<Node2D>(PreviewPath);

			_mapType = GetNodeOrNull<OptionButton>(MapTypePath);
			_mapSize = GetNodeOrNull<OptionButton>(MapSizePath);
			_worldAge = GetNodeOrNull<OptionButton>(WorldAgePath);
			_temperature = GetNodeOrNull<OptionButton>(TemperaturePath);
			_rainfall = GetNodeOrNull<OptionButton>(RainfallPath);
			_seaLevel = GetNodeOrNull<OptionButton>(SeaLevelPath);
			_resourceLevel = GetNodeOrNull<OptionButton>(ResourceLevelPath);
			_resourceSet = GetNodeOrNull<OptionButton>(ResourceSetPath);
			_view = GetNodeOrNull<OptionButton>(ViewPath);
			_seed = GetNodeOrNull<SpinBox>(SeedPath);
			_status = GetNodeOrNull<Label>(StatusPath);
		}

		/// <summary>
		/// Fills a chooser from the names given, and selects its default.
		///
		/// It REPLACES whatever the scene authored. This used to add items only to an empty chooser,
		/// which made the scene a second owner of every menu's contents: the view chooser had six
		/// items typed into terrain_generator_lab.tscn, so two art styles added to StyleProfiles were
		/// simply not listed, and nothing reported it - the menu looked authored and correct. These
		/// lists are derived (the axis enums, and now the styles), so the code that derives them owns
		/// what is on screen.
		/// </summary>
		private static void Fill(OptionButton? option, string[] names, int selected = 0)
		{
			if (option is null)
				return;

			option.Clear();
			foreach (string name in names) option.AddItem(name);
			option.Selected = Mathf.Clamp(selected, 0, option.ItemCount - 1);
		}

		/// <summary>
		/// Setup axes use enum order. The presentation menu lists the four projections and then every
		/// art style, each of which draws through the painted projection - see ApplyView.
		/// </summary>
		private void PopulateOptions()
		{
			if (_world is null) return;
			Fill(_mapType, TerrainShapePresets.DisplayNames(), (int)_world.MapType);
			Fill(_mapSize, TerrainMapSetup.MapSizeNames, (int)_world.MapSize);
			Fill(_worldAge, TerrainMapSetup.WorldAgeNames, (int)_world.WorldAge);
			Fill(_temperature, TerrainMapSetup.TemperatureNames, (int)_world.Temperature);
			Fill(_rainfall, TerrainMapSetup.RainfallNames, (int)_world.Rainfall);
			Fill(_seaLevel, TerrainMapSetup.SeaLevelNames, (int)_world.SeaLevel);
			Fill(_resourceLevel, TerrainMapSetup.ResourceLevelNames, (int)_world.ResourceLevel);
			Fill(_resourceSet, TerrainMapSetup.ResourceSetNames, (int)_world.Resources);
			Fill(_view, ViewNames(), SelectedView());
			if (_seed is not null) _seed.Value = _world.Seed;
		}

		/// <summary>One entry per projection, then one per art style, named by the style itself.</summary>
		private static readonly string[] ProjectionNames =
			{ "Original", "Game tiles", "Isometric", "Isometric tiles" };

		private string[] ViewNames()
		{
			var names = new List<string>(ProjectionNames);
			foreach (TerrainMapArt? profile in StyleProfiles)
				names.Add(string.IsNullOrWhiteSpace(profile?.DisplayName)
					? $"Style {names.Count - ProjectionNames.Length + 1}"
					: profile!.DisplayName);
			return names.ToArray();
		}

		private int SelectedView()
		{
			if (_world is null) return 0;
			if (_world.Projection != TerrainProjection.Painted) return (int)_world.Projection;
			if (_world.MapArt is null) return 0;
			int style = StyleProfiles.IndexOf(_world.MapArt);
			return style < 0 ? 0 : ProjectionNames.Length + style;
		}

		private void ApplyView(int index)
		{
			if (_world is null) return;
			int style = index - ProjectionNames.Length;
			_world.Projection = style >= 0 ? TerrainProjection.Painted : (TerrainProjection)index;
			_world.MapArt = style >= 0 && style < StyleProfiles.Count ? StyleProfiles[style] : null;
		}
	}
}
