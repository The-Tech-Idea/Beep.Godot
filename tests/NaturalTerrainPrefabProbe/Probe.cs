using Godot;
using Beep.ECS;
using System;

public partial class Probe : Node2D
{
	public override async void _Ready()
	{
		try
		{
			int count = 0;
			foreach (NaturalTerrainPrefabComponent.RockKind material in Enum.GetValues<NaturalTerrainPrefabComponent.RockKind>())
			foreach (NaturalTerrainPrefabComponent.HeightKind height in Enum.GetValues<NaturalTerrainPrefabComponent.HeightKind>())
			{
				var part = new NaturalTerrainPrefabComponent { MaterialTheme = material, Elevation = height, ArtScale = 0.22f };
				part.Position = new Vector2(210 + (int)material * 420, 220 + (int)height * 220);
				AddChild(part);
				Check(part.GetVisibleBounds().Size.X > 100, "Texture bounds missing");
				Check(part.GetGroundAnchor() == Vector2.Zero, "Ground origin moved");
				Check(part.GetVisibleBounds().End.Y == 0, "Image not grounded");
				Check(part.GetTopFrontAnchor().Y < 0, "Top anchor not above base");
				Check(part.GetSurfacePolygon().Length == 8, "Default surface missing");
				Check(part.GetFootprintPolygon().Length == 8, "Default footprint missing");
				var customChild = new Node2D { Name = "UserDecoration" };
				part.AddChild(customChild);
				part.Refresh();
				part.Refresh();
				Check(part.GetChildCount(true) == 2, "Refresh duplicated artwork or deleted user child");
				part.HideGreenBackground = false;
				part.Refresh();
				part.HideGreenBackground = true;
				part.Refresh();
				count++;
			}
			var switching = new NaturalTerrainPrefabComponent();
			AddChild(switching);
			switching.Elevation = NaturalTerrainPrefabComponent.HeightKind.Quarter;
			switching.MaterialTheme = NaturalTerrainPrefabComponent.RockKind.GreyGranite;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			Check(switching.GetAssetPath().Contains("grey_granite_quarter"), "Selection not refreshed");
			switching.SurfaceRisePixels = 40;
			switching.ArtScale = 0.5f;
			Check(switching.GetTopFrontAnchor().Y == -20, "Custom rise not scaled");
			switching.QueueFree();
			await TestGeometry();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (DisplayServer.GetName() != "headless")
			{
				await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image frame = GetViewport().GetTexture().GetImage();
                int brightPixels = 0;
                for (int y = 0; y < frame.GetHeight(); y += 3)
                for (int x = 0; x < frame.GetWidth(); x += 3)
                {
                    Color pixel = frame.GetPixel(x, y);
                    Check(pixel.G - Math.Max(pixel.R, pixel.B) < 0.25f, "Green canvas leaked into render");
                    if (Math.Max(pixel.R, pixel.B) > 0.2f) brightPixels++;
                }
                Check(brightPixels > 3000, "Render blank or missing terrain");
                frame.SavePng("user://natural_terrain_prefab_preview.png");
				GD.Print("Preview: " + ProjectSettings.GlobalizePath("user://natural_terrain_prefab_preview.png"));
			}
			GD.Print($"PASS: {count} terrain variants, refresh ownership, ground anchors, selection and scale");
			GetTree().Quit();
		}
		catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
	}
	private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private async System.Threading.Tasks.Task TestGeometry()
    {
        var terrain = new NaturalTerrainPrefabComponent { Position = new Vector2(3000, 3000), ArtScale = 0.3f };
        AddChild(terrain);
        Vector2[] surface = terrain.GetSurfacePolygon();
        Vector2 middle = (surface[0] + surface[4]) / 2;
        Check(terrain.ContainsSurfacePoint(terrain.ToGlobal(middle)), "Surface containment failed");
        Check(!terrain.ContainsSurfacePoint(terrain.ToGlobal(new Vector2(9999, 0))), "Outside point accepted");
        Vector2[] footprint = terrain.GetFootprintPolygon();
        Vector2 groundMiddle = terrain.ToGlobal((footprint[0] + footprint[4]) / 2);
        var query = new PhysicsPointQueryParameters2D { Position = groundMiddle, CollisionMask = 2 };
        await PhysicsSync();
        Check(GetWorld2D().DirectSpaceState.IntersectPoint(query).Count == 0, "Blocking must default off");
        terrain.BlockingLayer = 2;
        terrain.BlockingEnabled = true;
        terrain.Refresh();
        await PhysicsSync();
        Check(GetWorld2D().DirectSpaceState.IntersectPoint(query).Count == 1, "Footprint does not block on selected layer");
        query.CollisionMask = 4;
        Check(GetWorld2D().DirectSpaceState.IntersectPoint(query).Count == 0, "Collision layer ignored");
        query.CollisionMask = 2;
        terrain.BlockingEnabled = false;
        terrain.Refresh();
        await PhysicsSync();
        Check(GetWorld2D().DirectSpaceState.IntersectPoint(query).Count == 0, "Disabled footprint still blocks");
        var custom = new[] { new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.2f), new Vector2(0.5f, 0.8f) };
        terrain.SurfaceOutline = custom;
        custom[0] = Vector2.Zero;
        terrain.MaterialTheme = NaturalTerrainPrefabComponent.RockKind.PaleLimestone;
        terrain.Elevation = NaturalTerrainPrefabComponent.HeightKind.Half;
        terrain.Refresh();
        Check(terrain.SurfaceOutline[0] == new Vector2(0.1f, 0.2f), "Custom geometry overwritten or array aliased");
        Vector2 beforeScale = terrain.GetSurfacePolygon()[0];
        terrain.ArtScale = 0.6f;
        terrain.Refresh();
        Check(terrain.GetSurfacePolygon()[0].IsEqualApprox(beforeScale * 2), "Geometry scale drift");
        terrain.BlockingEnabled = true;
        terrain.FootprintOutline = new[] { Vector2.Zero, Vector2.One };
        terrain.Refresh();
        Check(terrain.GetFootprintPolygon().Length == 0 && terrain._GetConfigurationWarnings().Length > 0, "Invalid outline not rejected");
        terrain.SurfaceOutline = new[] { Vector2.Zero, Vector2.One, new Vector2(0, 1), new Vector2(1, 0) };
        Check(terrain.GetSurfacePolygon().Length == 0, "Self-intersecting surface accepted");
        terrain.QueueFree();
        await PhysicsSync();
        GD.Print("PASS: surface queries, opt-in physics, collision layers, custom outlines, scale and invalid geometry");
    }

    private async System.Threading.Tasks.Task PhysicsSync()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
