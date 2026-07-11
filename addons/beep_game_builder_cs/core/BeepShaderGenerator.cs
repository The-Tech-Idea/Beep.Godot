using Godot;

public static class BeepShaderGenerator
{
    private const string CatalogPath = "res://addons/beep_game_builder_cs/catalogs/shader_presets.json";

    public static string CreateShaderById(string shaderId, bool overwrite = false)
    {
        // Try catalog first
        var data = BeepFileUtils.LoadJson(CatalogPath);
        if (data.TryGetValue("presets", out var presetsVar) && presetsVar.VariantType == Variant.Type.Array)
        {
            foreach (var p in presetsVar.AsGodotArray())
            {
                var d = p.AsGodotDictionary();
                if ((string)d["id"] == shaderId)
                {
                    var filename = (string)d["output_filename"];
                    var path = $"res://assets/shaders/{filename}.gdshader";
                    if (d.TryGetValue("file_reference", out var fileRef))
                    {
                        // Load from template file instead of inline code
                        var refPath = $"res://addons/beep_game_builder_cs/{fileRef}";
                        if (FileAccess.FileExists(refPath))
                        {
                            using var src = FileAccess.Open(refPath, FileAccess.ModeFlags.Read);
                            if (src != null)
                            {
                                BeepFileUtils.SafeWriteText(path, src.GetAsText(), overwrite);
                                return path;
                            }
                        }
                        BeepFileUtils.LogError($"Shader template not found: {refPath}");
                        return "";
                    }
                    BeepFileUtils.SafeWriteText(path, (string)d["code"], overwrite);
                    return path;
                }
            }
        }
        // Fallback: copy from template file
        var tplPath = $"res://addons/beep_game_builder_cs/templates/shaders/{shaderId}.gdshader.template";
        if (FileAccess.FileExists(tplPath))
        {
            var targetPath = $"res://assets/shaders/{shaderId}.gdshader";
            if (!overwrite && FileAccess.FileExists(targetPath)) return targetPath + " (exists)";
            using var src = FileAccess.Open(tplPath, FileAccess.ModeFlags.Read);
            if (src == null) return $"Error: cannot read {tplPath}";
            BeepFileUtils.SafeWriteText(targetPath, src.GetAsText(), overwrite);
            return targetPath;
        }
        BeepFileUtils.LogError($"Shader preset not found: {shaderId}"); return "";
    }

    public static void CreateAllShaders(bool overwrite = false)
    {
        var data = BeepFileUtils.LoadJson(CatalogPath);
        if (!data.TryGetValue("presets", out var presetsVar) || presetsVar.VariantType != Variant.Type.Array) return;
        foreach (var p in presetsVar.AsGodotArray())
            CreateShaderById((string)p.AsGodotDictionary()["id"], overwrite);
        BeepFileUtils.RefreshFilesystem();
    }
}
