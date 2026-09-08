using Godot;

namespace Beep.ECS;

/// <summary>
/// The C# half of <c>shaders/water_common.gdshaderinc</c> - the ONE sea, bound from
/// one place.
///
/// The include exists because two shaders draw water and composite it differently -
/// the painted view mixes the sea into its own opaque ground pass, the tile and
/// isometric views float a transparent surface over real seabed geometry - while the
/// WATER ITSELF has to be identical, or one map grows two oceans. That contract has
/// been shared on the shader side since the include was written. It was not shared on
/// this side: three renderers each set the same uniform names by hand, and they had
/// drifted exactly as far as that always does.
///
/// Counted before this file existed: of the 22 shared uniforms C# writes, the tile
/// view wrote 13. It was missing every dial that controls the authored foam sheet
/// (foam_tiles_along, foam_tiles_across, foam_scroll, foam_pulse, foam_arrival_rate),
/// both swell dials, and both texture-repeat dials - while still setting
/// <c>use_foam_sheet</c> from its own FoamSheetPath. So a coastline tuned until it
/// surfed in the isometric view drew the same sheet on shader defaults in the tile
/// view of the same map. Nobody wrote that bug; there was simply somewhere for it to
/// be.
///
/// WHAT STAYS WITH THE CALLER, deliberately:
/// - <c>coast_map</c>. Every view binds the shared field, but each resolves it
///   through its own render cache and its own windowing, so the VALUE is genuinely
///   per view even though the uniform is shared.
/// - The uniforms <c>iso_water.gdshader</c> declares for itself - max_opacity,
///   clarity_tiles, lake_opacity, shore_opacity, cell_size, tile_batch,
///   flat_projection, tile_offset. They describe a transparent water SURFACE and mean
///   nothing to an opaque composite, so they belong to the two views that draw one.
/// - <c>sand_texture_tiles</c> and <c>ground_seam_blend</c>, which
///   <see cref="TerrainMaterialTiling"/> owns.
/// - tint_sand/tint_shallow/tint_deep, foam_tiles, wave_speed, wave_amount,
///   shallow_sand and foam_lines, which no renderer sets: they run on the values the
///   shader author chose, and adding exports for them here would be inventing dials
///   nothing asked for.
/// </summary>
internal static class TerrainWaterMaterial
{
    /// <summary>
    /// Every shared dial, all of them required.
    ///
    /// Positional on purpose. The defect this file closes was a view that supplied
    /// half the dials and left the rest to the shader, and a record the compiler will
    /// not let you half-fill cannot do that again - a new dial added here stops every
    /// view compiling until each says what it wants. Call sites pass them by name, so
    /// the length costs nothing at the point of use.
    /// </summary>
    internal readonly record struct Settings(
        Vector2I Size,
        Vector2I Origin,
        float CoastRange,
        float GroundTextureTiles,
        float WaterTextureTiles,
        float WaveIntensity,
        float FoamStrength,
        float ShallowTiles,
        float DeepTiles,
        float FoamTilesAlong,
        float FoamTilesAcross,
        float FoamScroll,
        float FoamPulse,
        float FoamArrivalRate,
        float SwellDirectionDegrees,
        float SwellDirectionality);

    /// <summary>
    /// Writes the shared block, held to the ranges the shader's own hint_range
    /// declares.
    ///
    /// The clamps are here rather than at each export because an export's
    /// PropertyHint.Range constrains the Inspector and nothing else: code assigning
    /// WaveIntensity = 40 reached the shader unchecked in two of the three views and
    /// was clamped in the third. Authored values are inside these ranges already, so
    /// no existing scene moves.
    ///
    /// Two pass through untouched. <c>coast_range</c> carries no hint and must agree
    /// with the range its caller BUILT the coast field with - clamping it here would
    /// describe a field that was never generated. <c>swell_direction_degrees</c> is a
    /// direction, where 370 is 10 rather than 360, so clamping it would bend the
    /// swell round to due west instead of wrapping it.
    /// </summary>
    internal static void Apply(ShaderMaterial material, in Settings settings)
    {
        material.SetShaderParameter("map_size", new Vector2(settings.Size.X, settings.Size.Y));
        material.SetShaderParameter("map_origin", new Vector2(settings.Origin.X, settings.Origin.Y));
        material.SetShaderParameter("coast_range", settings.CoastRange);

        material.SetShaderParameter("ground_texture_tiles", Mathf.Max(1.0f, settings.GroundTextureTiles));
        material.SetShaderParameter("water_texture_tiles", Mathf.Max(1.0f, settings.WaterTextureTiles));

        material.SetShaderParameter("wave_intensity", Mathf.Clamp(settings.WaveIntensity, 0.0f, 2.0f));
        material.SetShaderParameter("foam_strength", Mathf.Clamp(settings.FoamStrength, 0.0f, 1.0f));
        material.SetShaderParameter("shallow_tiles", Mathf.Clamp(settings.ShallowTiles, 0.0f, 8.0f));
        material.SetShaderParameter("deep_tiles", Mathf.Clamp(settings.DeepTiles, 0.5f, 12.0f));

        material.SetShaderParameter("foam_tiles_along", Mathf.Clamp(settings.FoamTilesAlong, 1.0f, 48.0f));
        material.SetShaderParameter("foam_tiles_across", Mathf.Clamp(settings.FoamTilesAcross, 0.3f, 8.0f));
        material.SetShaderParameter("foam_scroll", Mathf.Clamp(settings.FoamScroll, 0.0f, 4.0f));
        material.SetShaderParameter("foam_pulse", Mathf.Clamp(settings.FoamPulse, 0.0f, 1.0f));
        material.SetShaderParameter("foam_arrival_rate", Mathf.Clamp(settings.FoamArrivalRate, 0.0f, 4.0f));

        material.SetShaderParameter("swell_direction_degrees", settings.SwellDirectionDegrees);
        material.SetShaderParameter("swell_directionality", Mathf.Clamp(settings.SwellDirectionality, 0.0f, 1.0f));
    }

    /// <summary>
    /// Binds the authored surf sheet and the switch that selects it, together, so
    /// they cannot disagree.
    ///
    /// A path that is set and fails to load says so twice on purpose:
    /// <see cref="TerrainTextures"/> reports that the file did not load, and this
    /// reports what the map will draw instead. They are different facts, and only the
    /// second tells an author their surf is gone rather than merely late.
    /// </summary>
    internal static bool BindFoamSheet(ShaderMaterial material, string path, string owner)
    {
        bool loaded = TerrainTextures.Bind(material, "foam_sheet", path, owner);
        if (!loaded && !string.IsNullOrWhiteSpace(path))
            GD.PushWarning($"[{owner}] falling back to generated crests.");

        material.SetShaderParameter("use_foam_sheet", loaded);
        return loaded;
    }

    /// <summary>
    /// The three seabed textures the shared sea reads, plus the surf sheet. One call,
    /// so a view cannot bind two of them and forget the third.
    /// </summary>
    internal static void ApplyTextures(
        ShaderMaterial material, string owner,
        string shallowPath, string deepPath, string sandPath, string foamSheetPath)
    {
        TerrainTextures.Bind(material, "tex_shallow", shallowPath, owner);
        TerrainTextures.Bind(material, "tex_deep", deepPath, owner);
        TerrainTextures.Bind(material, "tex_sand", sandPath, owner);
        BindFoamSheet(material, foamSheetPath, owner);
    }
}
