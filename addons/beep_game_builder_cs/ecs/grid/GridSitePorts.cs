using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Reads the three site facts off anything that answers them - a C#
    /// <see cref="IGridSite"/>, a GDScript node with matching member names, or a
    /// plain authored Dictionary. Same duck-typing rule as GridPorts and
    /// GridConstructionVisualPorts: ask by name, so no participant has to be a
    /// C# type.
    ///
    /// One call site reads "3x2, 40 wood, 3 turns" whether the answer comes from
    /// a typed GridBuildDefinition, a modder's GDScript definition, or a
    /// dictionary loaded from JSON.
    /// </summary>
    internal static class GridSitePorts
    {
        private static readonly StringName FootprintProperty = new("SiteFootprint");
        private static readonly StringName MaterialsProperty = new("SiteMaterials");
        private static readonly StringName TurnsProperty = new("SiteTurns");

        /// <summary>Whether the value answers all three site questions.</summary>
        public static bool AnswersSiteShape(Variant value)
        {
            if (value.Obj is IGridSite)
                return true;

            if (value.Obj is GodotObject obj && GodotObject.IsInstanceValid(obj))
                return obj.Get(FootprintProperty).VariantType != Variant.Type.Nil
                    && obj.Get(TurnsProperty).VariantType != Variant.Type.Nil;

            return GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary data)
                && (data.ContainsKey("SiteFootprint") || data.ContainsKey("site_footprint"));
        }

        /// <summary>The ground the site takes, never smaller than one cell.</summary>
        public static Vector2I Footprint(Variant value)
        {
            Vector2I footprint = value.Obj is IGridSite site
                ? site.SiteFootprint
                : ReadVector2I(value, FootprintProperty, "SiteFootprint", "site_footprint");

            return new Vector2I(Mathf.Max(1, footprint.X), Mathf.Max(1, footprint.Y));
        }

        /// <summary>The material that must physically arrive before work starts.</summary>
        public static Godot.Collections.Array Materials(Variant value)
        {
            if (value.Obj is IGridSite site)
                return site.SiteMaterials;

            if (value.Obj is GodotObject obj && GodotObject.IsInstanceValid(obj))
            {
                Variant read = obj.Get(MaterialsProperty);
                if (read.VariantType == Variant.Type.Array)
                    return read.AsGodotArray();
            }

            if (GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary data))
            {
                if (data.ContainsKey("SiteMaterials"))
                    return data["SiteMaterials"].AsGodotArray();
                if (data.ContainsKey("site_materials"))
                    return data["site_materials"].AsGodotArray();
            }

            return new Godot.Collections.Array();
        }

        /// <summary>
        /// Turns of work. Never negative; zero is a legitimate answer meaning the
        /// site needs no work at all.
        /// </summary>
        public static int Turns(Variant value)
        {
            int turns = value.Obj is IGridSite site
                ? site.SiteTurns
                : ReadInt(value, TurnsProperty, "SiteTurns", "site_turns");

            return Mathf.Max(0, turns);
        }

        /// <summary>The three facts as one line, for a tooltip or a build menu.</summary>
        public static string Describe(Variant value)
        {
            Vector2I footprint = Footprint(value);
            var parts = new System.Collections.Generic.List<string>();
            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(Materials(value)))
            {
                if (amount > 0 && !string.IsNullOrWhiteSpace(resourceId))
                    parts.Add($"{resourceId} {amount}");
            }

            int turns = Turns(value);
            string materials = parts.Count == 0 ? "no materials" : string.Join(", ", parts);
            return $"{footprint.X}x{footprint.Y}, {materials}, {turns} turn{(turns == 1 ? "" : "s")}";
        }

        private static Vector2I ReadVector2I(Variant value, StringName property, string pascal, string snake)
        {
            if (value.Obj is GodotObject obj && GodotObject.IsInstanceValid(obj))
            {
                Variant read = obj.Get(property);
                if (read.VariantType != Variant.Type.Nil)
                    return read.AsVector2I();
            }

            return GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary data)
                ? GridVariantReader.Vector2I(data, data.ContainsKey(pascal) ? pascal : snake, Vector2I.One)
                : Vector2I.One;
        }

        private static int ReadInt(Variant value, StringName property, string pascal, string snake)
        {
            if (value.Obj is GodotObject obj && GodotObject.IsInstanceValid(obj))
            {
                Variant read = obj.Get(property);
                if (read.VariantType != Variant.Type.Nil)
                    return read.AsInt32();
            }

            return GridVariantReader.TryDictionary(value, out Godot.Collections.Dictionary data)
                ? GridVariantReader.Int(data, data.ContainsKey(pascal) ? pascal : snake, 0)
                : 0;
        }
    }
}
