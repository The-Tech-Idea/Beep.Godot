using System.Collections.Generic;
using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Resolves a <see cref="GameItem"/> from its <see cref="GameItem.Id"/>. Saves persist ids,
    /// not resources (small, and survive an item being rebalanced), so load needs a way back from
    /// the id to the `.tres` — this is it.
    ///
    /// Mirrors <c>SkinCatalog</c>: a static, lazily-cached scan of a folder tree for `.tres`
    /// files, keyed by id. Point <see cref="ItemsRoot"/> at where the game authors its items
    /// (default <c>res://items</c>) before first access, or <see cref="Register"/> items built at
    /// runtime. This is developer content, so the default is a project path, not an addon path.
    /// </summary>
    public static class GameItemCatalog
    {
        /// <summary>Folder scanned (recursively) for GameItem `.tres`. Reassign before first use
        /// to relocate; changing it after items are cached has no effect until <see cref="Reload"/>.</summary>
        public static string ItemsRoot { get; set; } = "res://items";

        private static Dictionary<string, GameItem>? _byId;
        private static readonly object _lock = new();

        /// <summary>Every catalogued item, keyed by id. Lazy-loaded on first access.</summary>
        public static Dictionary<string, GameItem> All
        {
            get { lock (_lock) { _byId ??= Scan(); return _byId; } }
        }

        /// <summary>The GameItem for an id, or null if nothing is catalogued under it.</summary>
        public static GameItem? Resolve(string id)
            => !string.IsNullOrEmpty(id) && All.TryGetValue(id, out var item) ? item : null;

        /// <summary>Add or replace an item at runtime (generated items, tests, items outside
        /// <see cref="ItemsRoot"/>). Warns on an empty id — an unkeyed item can never be resolved.</summary>
        public static void Register(GameItem item)
        {
            if (item == null) return;
            if (string.IsNullOrEmpty(item.Id))
            {
                GD.PushWarning("[GameItemCatalog] Ignored an item with an empty Id — it could never be resolved on load. Set GameItem.Id.");
                return;
            }
            lock (_lock) { (_byId ??= Scan())[item.Id] = item; }
        }

        /// <summary>Rescan from disk (e.g. after authoring a new `.tres` in the editor).</summary>
        public static void Reload()
        {
            lock (_lock) { _byId = Scan(); }
        }

        private static Dictionary<string, GameItem> Scan()
        {
            var result = new Dictionary<string, GameItem>();
            if (!DirAccess.DirExistsAbsolute(ItemsRoot))
            {
                // Not an error: a project may ship no items yet. Left silent-but-visible via Print
                // rather than a warning, so an item-less game doesn't nag on every load.
                GD.Print($"[GameItemCatalog] No items folder at {ItemsRoot} — catalog is empty. " +
                         "Author GameItem .tres there, or set GameItemCatalog.ItemsRoot / call Register().");
                return result;
            }
            ScanDir(ItemsRoot, result);
            GD.Print($"[GameItemCatalog] Loaded {result.Count} item(s) from {ItemsRoot}.");
            return result;
        }

        private static void ScanDir(string path, Dictionary<string, GameItem> into)
        {
            using var dir = DirAccess.Open(path);
            if (dir == null) return;

            dir.ListDirBegin();
            for (string entry = dir.GetNext(); entry != ""; entry = dir.GetNext())
            {
                if (entry.StartsWith(".")) continue;
                string full = $"{path}/{entry}";

                if (DirAccess.DirExistsAbsolute(full)) { ScanDir(full, into); continue; }

                // Godot renames .tres to .tres.remap in exported builds; accept both.
                if (!entry.EndsWith(".tres") && !entry.EndsWith(".tres.remap")) continue;
                string resPath = entry.EndsWith(".remap") ? full[..^6] : full;

                if (ResourceLoader.Load(resPath) is not GameItem item) continue; // not an item resource
                if (string.IsNullOrEmpty(item.Id))
                {
                    GD.PushWarning($"[GameItemCatalog] {resPath} has an empty Id — skipped; it could never be resolved on load.");
                    continue;
                }
                if (into.TryGetValue(item.Id, out var prior) && prior != item)
                    GD.PushWarning($"[GameItemCatalog] Duplicate item Id '{item.Id}' — {resPath} overrides an earlier file. Ids must be unique.");
                into[item.Id] = item;
            }
            dir.ListDirEnd();
        }
    }
}
