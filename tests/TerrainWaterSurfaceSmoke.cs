using Beep.ECS;
using Godot;
using System;

public partial class TerrainWaterSurfaceSmoke : Node
{
    public bool Run()
    {
        VerifySampleKinds();
        VerifyScratchLifetime();
        VerifyFeatureRanking();
        VerifyErosionParity();
        VerifyCompactCellMetadata();
        VerifyCellChunks();
        VerifyChunkSnapshots();
        VerifyScatter();
        VerifyIsometricScatter();
        foreach (int seed in new[] { 31415, 12345, 98765, 8675309 })
            VerifyWorld(seed);
        return true;
    }

    private static void VerifySampleKinds()
    {
        foreach (var size in new[] { new Vector2I(1, 1), new Vector2I(31, 33), new Vector2I(67, 97), new Vector2I(1024, 1024) })
        {
            var source = new string[size.X * size.Y];
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
                source[y * size.X + x] = x < 32 ? "grass" : (x + y) % 3 == 0 ? "sand" : "water";
            var packed = new TerrainSampleKinds(source, size.X, size.Y);
            for (int i = 0; i < source.Length; i++)
                Check(packed[i] == source[i], "Chunk compression changed fine terrain labels");
            Check(packed.UniformChunkCount > 0, "Uniform chunks were not compressed");
            if (size.X == 1024)
                Check(packed.PayloadBytes < source.Length * IntPtr.Size / 3, "Sample label payload was not reduced");
            string before = packed[0];
            source[0] = "edited";
            Check(packed[0] == before, "Published chunks retain mutable source array");
        }
        var unique = new string[1024];
        for (int i = 0; i < unique.Length; i++) unique[i] = "kind_" + i;
        var varied = new TerrainSampleKinds(unique, 32, 32);
        for (int i = 0; i < unique.Length; i++) Check(varied[i] == unique[i], "Palette truncated more than 256 terrain labels");
    }

    private void VerifyChunkSnapshots()
    {
        var cells = new GridCellDataComponent();
        AddChild(cells);
        foreach (var at in new[] { new Vector2I(-33, -1), Vector2I.Zero, new Vector2I(64, 97) })
        {
            cells.SetTerrainKind(at, "grass");
            cells.SetMetadata(at, "point", new Vector2(1.25f, -4.5f));
            cells.SetMetadata(at, "color", Colors.Red);
            cells.SetMetadata(at, "nested", new Godot.Collections.Dictionary { ["values"] = new Godot.Collections.Array { 1, "two" } });
        }
        var snapshot = cells.CaptureChunkState();
        var json = new Json();
        Check(json.Parse(Json.Stringify(snapshot)) == Error.Ok, "Chunk snapshot JSON failed");
        var copy = new GridCellDataComponent();
        AddChild(copy);
        copy.RestoreChunkState(json.Data.AsGodotDictionary());
        Check(copy.CellCount == 3 && copy.GetMetadata(Vector2I.Zero, "point").AsVector2() == new Vector2(1.25f, -4.5f), "JSON lost chunk coordinates or vector metadata");
        Check(copy.GetMetadata(Vector2I.Zero, "color").AsColor() == Colors.Red, "JSON lost color metadata");
        var broken = snapshot.Duplicate(true);
        broken["chunks"].AsGodotArray()[1].AsGodotDictionary()["data"] = "not-base64!";
        ulong revision = copy.TerrainRevision;
        bool rejected = false;
        try { copy.RestoreChunkState(broken); } catch (FormatException) { rejected = true; }
        Check(rejected && copy.CellCount == 3 && copy.TerrainRevision == revision, "Invalid chunk partially replaced live cells");
        var duplicated = snapshot.Duplicate(true);
        duplicated["chunks"].AsGodotArray().Add(duplicated["chunks"].AsGodotArray()[0]);
        rejected = false;
        try { copy.RestoreChunkState(duplicated); } catch (FormatException) { rejected = true; }
        Check(rejected && copy.TerrainRevision == revision, "Duplicate chunk changed live cells");
        foreach (Variant invalid in new Variant[] { "invalid", 0.5, double.PositiveInfinity, (long)int.MaxValue + 1 })
        {
            var malformed = snapshot.Duplicate(true);
            malformed["chunks"].AsGodotArray()[0].AsGodotDictionary()["x"] = invalid;
            rejected = false;
            try { copy.RestoreChunkState(malformed); } catch (FormatException) { rejected = true; }
            Check(rejected && copy.TerrainRevision == revision, "Malformed chunk coordinate changed live cells");
        }
        cells.SetMetadata(Vector2I.Zero, "unsafe", this);
        rejected = false;
        try { cells.CaptureChunkState(); } catch (FormatException) { rejected = true; }
        Check(rejected, "Snapshot serialized a live node reference");
        cells.Free();
        copy.Free();
        json.Dispose();
    }

    private void VerifyCellChunks()
    {
        var cells = new GridCellDataComponent();
        var restored = new GridCellDataComponent();
        AddChild(cells);
        AddChild(restored);
        var expected = new System.Collections.Generic.Dictionary<Vector2I, string>();
        var random = new Random(42);
        for (int i = 0; i < 5000; i++)
        {
            var at = new Vector2I(random.Next(-70, 71), random.Next(-70, 71));
            string kind = i % 2 == 0 ? "grass" : "sand";
            cells.SetTerrainKind(at, kind);
            expected[at] = kind;
        }
        foreach (int x in new[] { int.MinValue, -33, -32, -1, 0, 31, 32, int.MaxValue })
        {
            var at = new Vector2I(x, x);
            cells.SetTerrainKind(at, "mud");
            expected[at] = "mud";
        }
        Check(cells.CellCount == expected.Count, "Chunk store counted replacements as new cells");
        var seen = new System.Collections.Generic.HashSet<Vector2I>();
        foreach (var chunk in cells.GetStoredChunks())
        {
            var snapshot = cells.GetChunkCells(chunk);
            Check(snapshot.Count <= 1024, "Logical chunk exceeded 32x32 cells");
            foreach (var record in snapshot)
            {
                var at = record["cell"].AsVector2I();
                Check(ChunkedCellStore<string>.ChunkFor(at) == chunk && seen.Add(at), "Chunk duplicated or misplaced a cell");
                Check(record["terrain"].AsString() == expected[at], "Chunk snapshot changed cell terrain");
            }
            // The snapshot importer rejects int.MinValue as its missing-coordinate sentinel.
            // Boundary lookup itself is still valid; exercise ordinary persistence separately.
            if (chunk.X != (int.MinValue >> 5)) restored.LoadCells(snapshot, false);
        }
        Check(seen.Count == expected.Count, "Chunk snapshots omitted stored cells");
        foreach (var (at, kind) in expected)
        {
            Check(cells.GetTerrainKind(at) == kind, "Chunk lookup changed a stored cell");
            if (at.X != int.MinValue) Check(restored.GetTerrainKind(at) == kind, "Chunk-local save/load changed terrain");
        }
        cells.ClearCells();
        Check(cells.StoredChunkCount == 0 && cells.CellCount == 0, "Clear retained cell chunks");
        cells.Free();
        restored.Free();
    }

    private void VerifyCompactCellMetadata()
    {
        var cells = new GridCellDataComponent();
        AddChild(cells);
        var patch = GridTerrainWaterPatch.Create(2, (_, _) => false);
        cells.LoadGeneratedCells(new[]
        {
            (Vector2I.Zero, "grass", "woods", 1, 0.9f, 0.3f, "", patch, "grass", 1f, (GridTerrainWaterPatch?)null, 0f),
            (Vector2I.One, "sand", "", 0, 1f, 0f, "", patch, "sand", 0f, (GridTerrainWaterPatch?)null, 0f)
        });
        Check(cells.MetadataDictionaryCount == 0, "Generated cells allocated native metadata dictionaries");
        Check(cells.GetMetadata(Vector2I.Zero, "terrain_feature").AsString() == "woods", "Typed feature missing");
        Check(cells.GetMetadata(Vector2I.Zero, "terrain_relief").AsInt32() == 1, "Typed relief missing");
        Check(cells.GetMetadata(Vector2I.Zero, "terrain_shade").AsSingle() == 0.9f, "Typed shade missing");
        Check(cells.GetMetadata(Vector2I.One, "terrain_shore_inland").VariantType == Variant.Type.Nil, "Absent shore metadata became present");
        var snapshot = cells.GetCells();
        Check(cells.MetadataDictionaryCount == 0, "Reading snapshots materialized live native metadata");
        var copy = new GridCellDataComponent();
        AddChild(copy);
        copy.LoadCells(snapshot);
        foreach (var at in new[] { Vector2I.Zero, Vector2I.One })
        {
            var before = cells.GetCell(at)["metadata"].AsGodotDictionary();
            var after = copy.GetCell(at)["metadata"].AsGodotDictionary();
            Check(before.RecursiveEqual(after), "Compact metadata changed save representation");
        }
        cells.SetMetadata(Vector2I.Zero, "terrain_feature", "jungle");
        cells.SetMetadata(Vector2I.Zero, "custom", 42);
        Check(cells.MetadataDictionaryCount == 1, "Metadata edit did not allocate only its own dictionary");
        Check(cells.GetMetadata(Vector2I.Zero, "terrain_feature").AsString() == "jungle", "Generated metadata defeated explicit override");
        Check(copy.GetMetadata(Vector2I.Zero, "terrain_feature").AsString() == "woods", "Snapshot shared mutable metadata");
        cells.SetMetadata(Vector2I.Zero, "terrain_shore_inland", "mud");
        cells.SetTerrainKind(Vector2I.Zero, "desert");
        Check(cells.GetMetadata(Vector2I.Zero, "terrain_shore_inland").VariantType == Variant.Type.Nil
            && cells.GetMetadata(Vector2I.Zero, "terrain_beach_width").VariantType == Variant.Type.Nil,
            "Terrain edit retained compact generated shore");
        Check(cells.GetMetadata(Vector2I.Zero, "custom").AsInt32() == 42, "Terrain edit lost custom metadata");
        cells.Free();
        copy.Free();
    }

    private static void VerifyErosionParity()
    {
        foreach (int seed in new[] { 7, 31415, 42 })
        foreach (float strength in new[] { 0f, 0.25f, 1f, 4f })
        {
            var world = new TerrainGenerationBuffer(37, 29, 1);
            var reference = new TerrainGenerationBuffer(37, 29, 1);
            var random = new Random(seed);
            for (int i = 0; i < world.Count; i++)
            {
                world.Land[i] = reference.Land[i] = random.Next(5) != 0;
                world.Elevation[i] = reference.Elevation[i] = random.Next(50) / 50f;
                world.CoastDistance[i] = reference.CoastDistance[i] = random.Next(20);
            }
            TerrainErosionStage.Apply(world, default(TerrainGenerationSettings) with { ErosionStrength = strength });
            ReferenceErosion(reference, strength);
            for (int i = 0; i < world.Count; i++)
                Check(BitConverter.SingleToInt32Bits(world.Elevation[i]) == BitConverter.SingleToInt32Bits(reference.Elevation[i]),
                    "Optimized erosion changed a height bit");
        }
    }

    private static void ReferenceErosion(TerrainGenerationBuffer world, float strength)
    {
        if (strength <= 0) return;
        var downstream = new int[world.Count];
        var order = new int[world.Count];
        var flow = new float[world.Count];
        int count = TerrainFlow.Accumulate(world, downstream, order, flow);
        if (count == 0) return;
        var sorted = new float[count];
        for (int i = 0; i < count; i++) sorted[i] = flow[order[i]];
        Array.Sort(sorted);
        float typical = Mathf.Max(1f, sorted[count / 2]);
        float dial = Mathf.Clamp(strength, 0f, 4f);
        var settled = new float[world.Count];
        for (int pass = 0; pass < 12; pass++)
        {
            for (int i = 0; i < count; i++)
            {
                int index = order[i], to = downstream[index];
                if (to < 0) continue;
                float slope = Mathf.Max(0f, world.Elevation[index] - world.Elevation[to]);
                if (slope <= 0) continue;
                float drainage = Mathf.Min(3f, Mathf.Pow(flow[index] / typical, 0.5f));
                float lowering = (0.12f * dial) * drainage * slope;
                world.Elevation[index] = Mathf.Max(world.Elevation[to], world.Elevation[index] - lowering);
            }
            for (int i = 0; i < count; i++)
            {
                int index = order[i], x = index % world.Width, y = index / world.Width;
                float total = 0;
                int neighbors = 0;
                for (int side = 0; side < 4; side++)
                {
                    int nx = x + (side == 0 ? 1 : side == 1 ? -1 : 0);
                    int ny = y + (side == 2 ? 1 : side == 3 ? -1 : 0);
                    if (!world.InBounds(nx, ny) || !world.Land[world.Index(nx, ny)]) continue;
                    total += world.Elevation[world.Index(nx, ny)];
                    neighbors++;
                }
                settled[index] = neighbors == 0 ? world.Elevation[index] : world.Elevation[index]
                    + (0.35f * dial * ((total / neighbors) - world.Elevation[index]));
            }
            for (int i = 0; i < count; i++) world.Elevation[order[i]] = Mathf.Clamp(settled[order[i]], 0f, 1f);
        }
    }

    private static void VerifyFeatureRanking()
    {
        var random = new Random(31415);
        var values = new float[67 * 71];
        var mask = new bool[values.Length];
        var local = new float[64];
        for (int i = 0; i < values.Length; i++) values[i] = random.Next(20) / 20f;
        for (int by = 0; by < 71; by += 8)
        for (int bx = 0; bx < 67; bx += 8)
        {
            Array.Clear(mask);
            int count = 0;
            for (int y = by; y < Math.Min(by + 8, 71); y++)
            for (int x = bx; x < Math.Min(bx + 8, 67); x++)
                if (random.Next(3) > 0)
                {
                    int index = y * 67 + x;
                    mask[index] = true;
                    local[count++] = values[index];
                }
            Array.Sort(local, 0, count);
            foreach (float percentile in new[] { 0f, 0.1f, 0.5f, 0.73f, 1f })
                Check(TerrainFeatureStage.RankedValue(local, count, percentile) == TerrainGeometry.Percentile(values, mask, percentile),
                    "Local feature ranking differs from full-map mask ranking");
        }
    }

    private static void VerifyScratchLifetime()
    {
        var world = new TerrainGenerationBuffer(64, 64, 4);
        Check(world.ScratchPayloadBytes == 0, "Buffer eagerly allocated stage scratch");
        world.Elevation[0] = 0.75f;
        world.CoastDistance[0] = 2;
        Check(world.ScratchPayloadBytes == 64 * 64 * 8, "Scratch allocation accounting differs");
        world.ReleaseCoastDistances();
        Check(world.ScratchPayloadBytes == 64 * 64 * 4, "Coast scratch remained rooted");
        bool rejected = false;
        try { _ = world.CoastDistance; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Released coast distances silently reallocated");
        world.Shade[0] = 0.6f;
        world.Terrain[0] = "grass";
        world.ReleaseGenerationScratch();
        Check(world.ScratchPayloadBytes == 0, "Finished scratch remained rooted");
        Check(world.Shade[0] == 0.6f && world.Shade[1] == 1f && world.Terrain[0] == "grass", "Scratch release changed output arrays");
        rejected = false;
        try { _ = world.Elevation; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Finished scratch silently reallocated");
    }

    private static void VerifyScatter()
    {
        Span<Vector2> offsets = stackalloc Vector2[5];
        float nearestSum = 0;
        for (int seed = 0; seed < 64; seed++)
        {
            Check(TerrainFeatureScatter.Fill(offsets, new Vector2I(-7, 11), seed, 0.85f,
                Vector2.Zero, _ => true) == 5, "Dry cell lost requested feature count");
            for (int i = 0; i < offsets.Length; i++)
            {
                Check(offsets[i].Abs().X <= 0.4251f && offsets[i].Abs().Y <= 0.4251f,
                    "Scatter left its declared spread");
                float nearest = float.MaxValue;
                for (int j = 0; j < offsets.Length; j++)
                    if (i != j) nearest = Mathf.Min(nearest, offsets[i].DistanceTo(offsets[j]));
                nearestSum += nearest;
            }
        }
        float averageNearest = nearestSum / (64 * 5);
        Check(averageNearest > 0.2f, "Feature clumps still collapse into one small blob");
        Check(TerrainFeatureScatter.Fill(offsets, Vector2I.Zero, 0, 1f, Vector2.Zero,
            at => at == Vector2.Zero) == 1, "Rejected coast candidates stacked at the centre");
        Check(TerrainFeatureScatter.Fill(offsets, Vector2I.Zero, 0, 1f, Vector2.Zero,
            _ => false) == 0, "Submerged cell retained feature anchors");
        Check(TerrainFeatureScatter.Fill(offsets, Vector2I.Zero, 0, 0f, Vector2.Zero,
            _ => true) == 5, "Explicit zero spread lost requested count");
        GD.Print($"[terrain-water-surface] scatter mean nearest spacing={averageNearest:F3} cells");
    }

    private void VerifyIsometricScatter()
    {
        var host = new Node2D();
        AddChild(host);
        var origin = new Vector2I(-7, 11);
        var cell = origin + Vector2I.One;
        var cells = new GridCellDataComponent { Name = "Cells", DefaultTerrainKind = "deep_water" };
        host.AddChild(cells);
        var patch = GridTerrainWaterPatch.Create(8, (x, y) => x < 3 || y > 5);
        // InlandTerrain "" and BeachWidth 0f: this cell is not a shoreline, so it has no inland
        // counterpart and no beach. Added when LoadGeneratedCells grew those two fields.
        cells.LoadGeneratedCells(new[] { (cell, "grass", "woods", 0, 0.5f, 0f, "", patch, "", 0f, (GridTerrainWaterPatch?)null, 0f) });
        var iso = new TerrainIsometricRendererComponent
        {
            Name = "Iso", RefreshOnReady = false, CellDataPath = new NodePath("../Cells"),
            BoundsOrigin = origin, BoundsSize = new Vector2I(3, 3),
            BlockSheetPath = "res://addons/beep_game_builder_cs/textures/iso/kenney_voxel_blocks.png",
            SheetColumns = 8, SheetRows = 7, CellSize = new Vector2I(111, 64),
        };
        host.AddChild(iso);
        iso.Rebuild();
        var flat = new TerrainFeatureRendererComponent
        {
            RefreshOnReady = false, CellDataPath = new NodePath("../Cells"),
            BoundsOrigin = origin, BoundsSize = new Vector2I(3, 3), SpritesPerTile = 5,
            PositionJitter = 1f, WoodsSheetPath = "res://addons/beep_game_builder_cs/textures/plants/forest_trees.png",
        };
        host.AddChild(flat);
        var props = new TerrainIsometricFeatureRendererComponent
        {
            RefreshOnReady = false, IsometricRendererPath = new NodePath("../Iso"),
            BoundsSize = new Vector2I(3, 3), SpritesPerTile = 5, PositionJitter = 1f,
            WoodsSheetPath = flat.WoodsSheetPath,
        };
        host.AddChild(props);
        var corners = iso.SurfaceCorners(cell);
        var inverse = new Transform2D(corners[1] - corners[0], corners[2] - corners[1],
            iso.SurfacePosition(cell)).AffineInverse();
        for (int seed = 0; seed < 32; seed++)
        {
            flat.Seed = props.Seed = seed;
            flat.Rebuild();
            props.Rebuild();
            var flatAnchors = flat.GetStampAnchors();
            var isoAnchors = props.GetStampAnchors();
            Check(flatAnchors.Length > 0 && isoAnchors.Count == flatAnchors.Length,
                "Projection changed accepted feature count");
            foreach (var anchor in isoAnchors)
            {
                Vector2 local = inverse * anchor + Vector2.One * 0.5f;
                Check(!patch.IsWater(local, false), "Isometric trunk scattered into fine shoreline water");
                bool matched = false;
                foreach (var flatAnchor in flatAnchors)
                    matched |= (flatAnchor / flat.TileSize - (Vector2)cell).DistanceTo(local) < 0.001f;
                Check(matched, "Projections chose different logical feature positions");
            }
        }
        host.Free();
    }

    private void VerifyWorld(int seed)
    {
        var host = new Node();
        AddChild(host);
        var cells = new GridCellDataComponent { Name = "Cells" };
        host.AddChild(cells);
        var origin = new Vector2I(-7, 11);
        var size = new Vector2I(32, 32);
        var generator = new TerrainGeneratorComponent
        {
            Name = "Generator", GenerateOnReady = false, CellDataPath = new NodePath("../Cells"),
            BoundsOrigin = origin, BoundsSize = size, Seed = seed, TopologySamplesPerCell = 8,
            LandmassScale = 0.42f, UseScaleRules = true, StartPositionCount = 0,
        };
        host.AddChild(generator);
        Check(generator.GenerateTerrain() == size.X * size.Y, "Generation did not fill the grid");
        GeneratedTerrainField field = generator.ResolveField();
        var cache = new TerrainCoastField.LiveCache();
        Vector2I mixed = new(int.MinValue, int.MinValue);
        int mixedCount = 0;
        foreach (var row in cells.GetCells())
        {
            Vector2I cell = row["cell"].AsVector2I();
            var patch = cells.WaterPatchAtCell(cell);
            Check(patch is not null, "Generated cell lost its water patch");
            if (patch!.Resolution > 1) { mixed = cell; mixedCount++; }
        }
        Check(mixedCount > 0, "Fixture has no mixed coastline cells");

        var features = new TerrainFeatureRendererComponent
        {
            CellDataPath = new NodePath("../Cells"), RefreshOnReady = false,
            BoundsOrigin = origin, BoundsSize = size, PositionJitter = 1f, Seed = seed,
            WoodsSheetPath = "res://addons/beep_game_builder_cs/textures/plants/forest_trees.png",
            SpriteAnchor = new Vector2(0.5f, 0.9f),
        };
        host.AddChild(features);
        // Exercise every dry coastal cell, not just where this seed happened to grow woods.
        for (int y = 0; y < size.Y; y++)
        for (int x = 0; x < size.X; x++)
            if (field.WaterSourceAtCell(new Vector2I(x, y)) == "")
                cells.SetMetadata(origin + new Vector2I(x, y), "terrain_feature", "woods");
        features.Rebuild();
        var waterAt = TerrainCoastField.CreateLiveWaterSampler(cells, origin, size);
        var liveWaterAt = TerrainCoastField.CreateLiveWaterQuery(cells, origin, size);
        Check(features.StampCount > 0, "No feature anchors tested");
        foreach (Vector2 anchor in features.GetStampAnchors())
        {
            Vector2 local = anchor / features.TileSize - (Vector2)origin;
            Check(new Rect2(Vector2.Zero, (Vector2)size).HasPoint(local) && !waterAt(local),
                $"Jittered feature anchor submerged at {local}");
        }

        foreach (int detail in new[] { 4, 8 })
        {
            using var coast = cache.Resolve(cells, origin, size, detail, 5).GetImage();
            int fineChecks = 0;
            for (int y = 0; y < coast.GetHeight(); y++)
            for (int x = 0; x < coast.GetWidth(); x++)
            {
                var at = new Vector2((x + 0.5f) / detail, (y + 0.5f) / detail);
                var cell = new Vector2I(Mathf.FloorToInt(at.X), Mathf.FloorToInt(at.Y));
                Vector2 local = at - (Vector2)cell - Vector2.One * 0.5f;
                bool core = local.Length() < 0.22f;
                bool expected = waterAt(at);
                Check(liveWaterAt(at) == expected, "Local streamed query changed the fine water contour");
                Check((coast.GetPixel(x, y).R > 0.5f) == expected, $"Coast mismatch seed={seed} detail={detail} at={at}");
                // At authored sample centres, interpolation must retain the fine
                // data outside the compact movement-centre correction.
                if (detail == 8 && !core)
                    Check(expected == field.IsWaterAtPosition(at), "Live patch changed an original fine sample");
                if (!core) fineChecks++;
            }
            GD.Print($"[terrain-water-surface] seed={seed} detail={detail} fine samples={fineChecks} mixed cells={mixedCount}");
        }

        var snapshotter = new GridWorldStateComponent
        {
            Name = "Snapshot", CellDataPath = new NodePath("../Cells"), ParticipatesInSave = false,
            CaptureGridObjects = false, CaptureJobs = false, CaptureNavigationBlocks = false,
            CapturePlacementOccupancy = false, CaptureRoads = false, CaptureSelection = false,
        };
        host.AddChild(snapshotter);
        var snapshot = snapshotter.CaptureState();
        var persisted = GD.BytesToVar(GD.VarToBytes(snapshot)).AsGodotDictionary();
        var originalTexture = cache.Resolve(cells, origin, size, 4, 5);
        using var originalImage = originalTexture.GetImage();
        byte[] originalPixels = originalImage.GetData();
        var originalPatch = cells.WaterPatchAtCell(mixed);
        cells.AddFlag(mixed, GridCellDataComponent.CellFlags.Cleared);
        Check(ReferenceEquals(originalPatch, cells.WaterPatchAtCell(mixed)), "Flags erased fine terrain");
        Check(ReferenceEquals(originalTexture, cache.Resolve(cells, origin, size, 4, 5)), "Flags rebuilt the coast cache");

        // Painting the existing kind is still an explicit whole-cell edit.
        cells.FillTerrain(new Rect2I(mixed, Vector2I.One), cells.GetTerrainKind(mixed));
        Check(cells.WaterPatchAtCell(mixed) is null, "Same-kind fill left the old fine shoreline");
        Check(!ReferenceEquals(originalTexture, cache.Resolve(cells, origin, size, 4, 5)), "Patch removal did not invalidate the coast cache");
        snapshotter.RestoreState(persisted);
        Check(cells.WaterPatchAtCell(mixed)?.Encode() == originalPatch!.Encode(), "Binary snapshot lost shoreline bits");
        using var restored = cache.Resolve(cells, origin, size, 4, 5).GetImage();
        Check(originalPixels.AsSpan().SequenceEqual(restored.GetData()), "Restored coast differs from saved coast");

        // Invalid fine data must not clear or partially replace an existing world.
        var bad = new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary { ["cell"] = origin, ["terrain"] = "water" },
            new Godot.Collections.Dictionary { ["cell"] = mixed, ["terrain"] = "water", ["water_surface"] = "invalid" }
        };
        bool rejected = false;
        try { cells.LoadCells(bad); }
        catch (FormatException) { rejected = true; }
        Check(rejected && cells.CellCount == size.X * size.Y, "Invalid patch was accepted or cleared the map");
        using var afterRejected = cache.Resolve(cells, origin, size, 4, 5).GetImage();
        Check(originalPixels.AsSpan().SequenceEqual(afterRejected.GetData()), "Invalid restore partially changed the map");

        // Region reload and SetTerrainKind use the same authoritative cell record.
        var savedCell = cells.GetCell(mixed);
        cells.SetTerrainKind(mixed, "water");
        Check(cells.WaterPatchAtCell(mixed) is null, "Water painting kept a generated patch");
        cells.LoadCells(new Godot.Collections.Array { savedCell }, clearExisting: false);
        Check(cells.WaterPatchAtCell(mixed)?.Encode() == originalPatch.Encode(), "Partial restore lost fine detail");
        Check(cells.CellCount == size.X * size.Y, "Partial restore removed unrelated cells");
        host.Free();
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
    }
}
