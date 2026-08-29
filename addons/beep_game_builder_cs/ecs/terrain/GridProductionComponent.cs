using Godot;
using System;

namespace Beep.ECS
{
    /// <summary>
    /// Attach to a building Node to run simple resource production cycles. It
    /// consumes input resources from GridResourceWalletComponent, waits for the
    /// recipe duration, then adds outputs back to the wallet.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GridProductionComponent : Node
    {
        public enum ProductionState
        {
            Idle,
            Producing,
            Paused
        }

        [Signal] public delegate void ProductionStartedEventHandler(string recipeId);
        [Signal] public delegate void ProductionCompletedEventHandler(string recipeId);
        [Signal] public delegate void ProductionRejectedEventHandler(string recipeId, string reason);
        [Signal] public delegate void ProductionStateChangedEventHandler(int state);

        [Export] public NodePath ResourceWalletPath { get; set; } = new("");
        [Export] public Godot.Collections.Array Recipes { get; set; } = new();
        [Export] public string ActiveRecipeId { get; set; } = "";
        [Export] public bool AutoStart { get; set; } = false;
        [Export] public bool Loop { get; set; } = true;
        [Export] public bool ConsumeInputsOnStart { get; set; } = true;

        public ProductionState State { get; private set; } = ProductionState.Idle;
        public float RemainingSeconds { get; private set; }
        public string CurrentRecipeId { get; private set; } = "";
        public float Progress01
        {
            get
            {
                GridProductionRecipe? recipe = FindRecipe(CurrentRecipeId);
                if (recipe == null)
                    return 0f;
                return Mathf.Clamp(1f - EffectiveRemainingSeconds / recipe.EffectiveDurationSeconds, 0f, 1f);
            }
        }
        public float EffectiveRemainingSeconds => float.IsFinite(RemainingSeconds) && RemainingSeconds > 0f ? RemainingSeconds : 0f;

        private GridResourceWalletComponent? _wallet;

        public override void _Ready()
        {
            ResolveReferences();
            SetProcess(!Engine.IsEditorHint());
            if (!Engine.IsEditorHint() && AutoStart)
                StartProduction(ActiveRecipeId);
            UpdateConfigurationWarnings();
        }

        public override string[] _GetConfigurationWarnings()
        {
            if (ResourceWalletPath.IsEmpty)
                return new[] { "ResourceWalletPath should point to a GridResourceWalletComponent." };
            return Array.Empty<string>();
        }

        public override void _Process(double delta)
        {
            if (!IsProcessing() || Engine.IsEditorHint())
                return;

            Tick(delta);
        }

        public void Tick(double delta)
        {
            if (State != ProductionState.Producing)
                return;

            float step = DeltaSeconds(delta);
            if (step <= 0f)
                return;

            RemainingSeconds = Mathf.Max(0f, EffectiveRemainingSeconds - step);
            if (RemainingSeconds <= 0f)
                CompleteProduction();
        }

        public bool StartProduction(string recipeId = "")
        {
            ResolveReferences();
            if (_wallet == null)
                return Reject(recipeId, "missing_resource_wallet");

            if (State != ProductionState.Idle)
                return Reject(string.IsNullOrWhiteSpace(recipeId) ? CurrentRecipeId : recipeId, "already_producing");

            GridProductionRecipe? recipe = ResolveRecipe(recipeId);
            if (recipe == null)
                return Reject(recipeId, "missing_recipe");

            if (!recipe.HasOutputs())
                return Reject(recipe.RecipeId, "missing_outputs");

            if (ConsumeInputsOnStart && !_wallet.Spend(recipe.Inputs))
                return Reject(recipe.RecipeId, "missing_inputs");

            CurrentRecipeId = recipe.RecipeId;
            ActiveRecipeId = recipe.RecipeId;
            RemainingSeconds = recipe.EffectiveDurationSeconds;
            SetState(ProductionState.Producing);
            EmitSignal(SignalName.ProductionStarted, recipe.RecipeId);
            return true;
        }

        public void PauseProduction()
        {
            if (State == ProductionState.Producing)
                SetState(ProductionState.Paused);
        }

        public void ResumeProduction()
        {
            if (State == ProductionState.Paused)
                SetState(ProductionState.Producing);
        }

        public void CancelProduction(bool refundInputs = false)
        {
            GridProductionRecipe? recipe = FindRecipe(CurrentRecipeId);
            if (refundInputs && ConsumeInputsOnStart && recipe != null)
                _wallet?.Refund(recipe.Inputs);

            CurrentRecipeId = "";
            RemainingSeconds = 0f;
            SetState(ProductionState.Idle);
        }

        public bool CompleteProduction()
        {
            ResolveReferences();
            GridProductionRecipe? recipe = FindRecipe(CurrentRecipeId);
            if (_wallet == null || recipe == null)
            {
                CancelProduction();
                return false;
            }

            foreach ((string resourceId, int amount) in GridResourceAmount.Enumerate(recipe.Outputs))
            {
                if (amount <= 0 || string.IsNullOrWhiteSpace(resourceId))
                    continue;
                _wallet.AddAmount(resourceId, amount);
            }

            string completedRecipe = recipe.RecipeId;
            CurrentRecipeId = "";
            RemainingSeconds = 0f;
            SetState(ProductionState.Idle);
            EmitSignal(SignalName.ProductionCompleted, completedRecipe);

            if (Loop)
                StartProduction(completedRecipe);

            return true;
        }

        public GridProductionRecipe? FindRecipe(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
                return null;

            string normalized = Normalize(recipeId);
            foreach (GridProductionRecipe recipe in GridProductionRecipe.Enumerate(Recipes))
                if (recipe != null && Normalize(recipe.RecipeId) == normalized)
                    return recipe;

            return null;
        }

        private GridProductionRecipe? ResolveRecipe(string recipeId)
        {
            if (!string.IsNullOrWhiteSpace(recipeId))
                return FindRecipe(recipeId);

            if (!string.IsNullOrWhiteSpace(ActiveRecipeId))
                return FindRecipe(ActiveRecipeId);

            foreach (GridProductionRecipe recipe in GridProductionRecipe.Enumerate(Recipes))
                if (recipe != null)
                    return recipe;

            return null;
        }

        private bool Reject(string recipeId, string reason)
        {
            EmitSignal(SignalName.ProductionRejected, recipeId, reason);
            return false;
        }

        private void SetState(ProductionState state)
        {
            if (State == state)
                return;

            State = state;
            EmitSignal(SignalName.ProductionStateChanged, (int)state);
        }

        private void ResolveReferences()
        {
            if (_wallet == null || !GodotObject.IsInstanceValid(_wallet))
                _wallet = !ResourceWalletPath.IsEmpty
                    ? GetNodeOrNull<GridResourceWalletComponent>(ResourceWalletPath)
                    : IsInsideTree() ? EntityComponent.FindComponent<GridResourceWalletComponent>(GetTree()?.CurrentScene) : null;
        }

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_');

        private static float DeltaSeconds(double delta)
            => double.IsFinite(delta) && delta > 0.0 ? (float)Mathf.Min(delta, 86400.0) : 0f;

    }
}
