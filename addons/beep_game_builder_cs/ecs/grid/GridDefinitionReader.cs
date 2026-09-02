using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Reads definition data that may arrive as a typed Resource, a duck-typed
    /// Resource with matching property names, or a plain dictionary - accepting
    /// PascalCase and snake_case keys alike.
    ///
    /// Five definition resources (builds, crops, recipes, objectives, resource
    /// amounts) each carried a near-identical ~90-line private copy of these
    /// dual-key readers. One reader, so the accepted shapes cannot drift apart
    /// per definition type again.
    /// </summary>
    internal static class GridDefinitionReader
    {
        public static string ReadString(Godot.Collections.Dictionary data, string pascal, string snake, string fallback)
        {
            Variant value = ReadVariant(data, pascal, snake);
            return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
        }

        public static string ReadString(Resource resource, string pascal, string snake, string fallback)
        {
            Variant value = ReadVariant(resource, pascal, snake);
            return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
        }

        public static int ReadInt(Godot.Collections.Dictionary data, string pascal, string snake, int fallback)
            => GridVariantReader.Int(ReadVariant(data, pascal, snake), fallback);

        public static int ReadInt(Resource resource, string pascal, string snake, int fallback)
            => GridVariantReader.Int(ReadVariant(resource, pascal, snake), fallback);

        public static float ReadFloat(Godot.Collections.Dictionary data, string pascal, string snake, float fallback)
            => GridVariantReader.Float(ReadVariant(data, pascal, snake), fallback);

        public static float ReadFloat(Resource resource, string pascal, string snake, float fallback)
            => GridVariantReader.Float(ReadVariant(resource, pascal, snake), fallback);

        public static bool ReadBool(Godot.Collections.Dictionary data, string pascal, string snake, bool fallback)
            => GridVariantReader.Bool(ReadVariant(data, pascal, snake), fallback);

        public static bool ReadBool(Resource resource, string pascal, string snake, bool fallback)
            => GridVariantReader.Bool(ReadVariant(resource, pascal, snake), fallback);

        public static Vector2I ReadVector2I(Godot.Collections.Dictionary data, string pascal, string snake, Vector2I fallback)
            => GridVariantReader.Vector2I(ReadVariant(data, pascal, snake), fallback);

        public static Vector2I ReadVector2I(Resource resource, string pascal, string snake, Vector2I fallback)
            => GridVariantReader.Vector2I(ReadVariant(resource, pascal, snake), fallback);

        public static Godot.Collections.Array ReadArray(Godot.Collections.Dictionary data, string pascal, string snake)
        {
            Variant value = ReadVariant(data, pascal, snake);
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new Godot.Collections.Array();
        }

        public static Godot.Collections.Array ReadArray(Resource resource, string pascal, string snake)
        {
            Variant value = ReadVariant(resource, pascal, snake);
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new Godot.Collections.Array();
        }

        public static T? ReadObject<T>(Godot.Collections.Dictionary data, string pascal, string snake) where T : GodotObject
        {
            Variant value = ReadVariant(data, pascal, snake);
            return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
        }

        public static T? ReadObject<T>(Resource resource, string pascal, string snake) where T : GodotObject
        {
            Variant value = ReadVariant(resource, pascal, snake);
            return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
        }

        private static Variant ReadVariant(Godot.Collections.Dictionary data, string pascal, string snake)
        {
            if (data.ContainsKey(pascal))
                return data[pascal];
            return data.ContainsKey(snake) ? data[snake] : default;
        }

        private static Variant ReadVariant(Resource resource, string pascal, string snake)
        {
            Variant value = resource.Get(pascal);
            if (value.VariantType != Variant.Type.Nil)
                return value;
            return resource.Get(snake);
        }
    }
}
