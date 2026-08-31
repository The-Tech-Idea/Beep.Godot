using Godot;

namespace Beep.ECS
{
    /// <summary>
    /// Makes a generated node part of the SAVED scene, so the terrain engine can
    /// be used at design time to author a map - real TileMapLayers a developer
    /// keeps, edits by hand, and ships - rather than only at runtime.
    ///
    /// A node added with AddChild belongs to the tree but not to the scene FILE.
    /// Godot writes out a child only if its Owner is the scene root, and
    /// PackedScene.Pack applies the same rule, so a generated map without an
    /// owner looks perfectly correct in the viewport and then vanishes the
    /// moment the scene is reloaded. There is no error and nothing to notice
    /// until the work is already lost.
    ///
    /// This existed three times with two different answers - some sites used the
    /// creator's own Owner, one used the edited scene root - and the tile view's
    /// layers set NEITHER, which is why that view could never have been authored
    /// in the editor even once a trigger existed.
    /// </summary>
    public static class TerrainAuthoring
    {
        /// <summary>
        /// Gives a just-created node an owner, so it is saved with the scene.
        ///
        /// Call it AFTER AddChild: a node must already be in the tree, and an
        /// owner must be one of its ancestors.
        ///
        /// This deliberately does NOT check Engine.IsEditorHint. Ownership is
        /// only strictly needed at design time, but applying the same rule at
        /// runtime costs nothing, keeps one code path instead of two, and is
        /// what lets a guard prove the layers really would be saved - by packing
        /// a generated map and reading the result back, without needing to drive
        /// the editor.
        /// </summary>
        public static void Adopt(Node generated, Node creator)
        {
            if (!generated.IsInsideTree())
                return;

            // In the editor the scene being edited is the thing about to be
            // written to disk; at runtime the creator's own owner is the root of
            // the scene it was loaded from.
            Node? root = Engine.IsEditorHint()
                ? creator.GetTree()?.EditedSceneRoot
                : null;
            root ??= creator.Owner ?? creator.GetTree()?.CurrentScene;

            if (root is null || ReferenceEquals(root, generated))
                return;

            // Setting an owner that is not an ancestor throws; a renderer added
            // at runtime under no scene root is the ordinary way that happens.
            if (!root.IsAncestorOf(generated))
                return;

            generated.Owner = root;
        }
    }
}
