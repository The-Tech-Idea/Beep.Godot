using Godot;
using Beep.ECS;

namespace Beep.Tests.Examples;

/// <summary>
/// Seeds a small, deterministic logical world for the 15-piece terrain demo.
/// Visual ownership remains in design-time TileMapLayer nodes.
/// </summary>
[GlobalClass]
public partial class Terrain15PieceLayersDemoController : Node
{
    [Export] public NodePath CellsPath { get; set; } = new("Cells");

    public override void _Ready()
    {
        GridCellDataComponent? cells = GetNodeOrNull<GridCellDataComponent>(CellsPath);
        if (cells is null)
            return;

        cells.ClearCells();
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 18; x++)
                cells.SetTerrainKind(new Vector2I(x, y), "grass");
        }

        // Lake on the west and a compact desert field on the east exercise
        // corners, concave joins, diagonal bridges, and solid interiors.
        for (int y = 1; y < 9; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                float dx = x - 2.3f;
                float dy = y - 4.8f;
                if ((dx * dx * 0.55f) + (dy * dy) < 13.5f)
                    cells.SetTerrainKind(new Vector2I(x, y), "water");
            }
        }

        for (int y = 2; y < 8; y++)
        {
            for (int x = 10; x < 17; x++)
            {
                float dx = x - 13.2f;
                float dy = y - 4.6f;
                if ((dx * dx) + (dy * dy * 1.35f) < 10.0f)
                    cells.SetTerrainKind(new Vector2I(x, y), "desert");
            }
        }

        for (int y = 0; y < 3; y++)
        {
            for (int x = 7; x < 11; x++)
            {
                float dx = x - 8.7f;
                float dy = y - 1.0f;
                if ((dx * dx) + (dy * dy) < 3.1f)
                    cells.SetTerrainKind(new Vector2I(x, y), "volcano");
            }
        }

        foreach (string path in new[] { "GroundBaseTerrain15", "GrassTerrain15", "DesertTerrain15", "VolcanoTerrain15", "WaterTerrain15" })
            GetNodeOrNull<GridTerrainTransitionLayerComponent>(path)?.RefreshTransitions();

        GetNodeOrNull<SeededTerrainPropScatterComponent>("EnvironmentProps")?.Rebuild();
    }
}
