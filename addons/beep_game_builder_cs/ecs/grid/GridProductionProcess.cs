using Godot;
using System;
using ProductionState = Beep.ECS.GridProductionComponent.ProductionState;

namespace Beep.ECS;

/// <summary>Production state and transactions without an actor, building scene or process callback.
/// Runs on the main thread because recipes and the shared wallet use Godot resources.</summary>
public sealed class GridProductionProcess
{
    public Func<GridResourceWalletComponent?> ResolveWallet { get; set; } = () => null;
    public Func<bool> CanContinue { get; set; } = () => true;
    public Godot.Collections.Array Recipes { get; set; } = new();
    public string ActiveRecipeId { get; set; } = "";
    public bool Loop { get; set; } = true;
    public bool ConsumeInputsOnStart { get; set; } = true;
    public ProductionState State { get; private set; }
    public string CurrentRecipeId { get; private set; } = "";
    public float RemainingTurns { get; private set; }
    public double PendingWorkTurns { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool IsAdvancing { get; private set; }
    private bool _inputsCommitted;
    private GridResourceWalletComponent? _wallet;
    public event Action<string>? Started;
    public event Action<string>? Completed;
    public event Action<string, string>? Rejected;
    public event Action<ProductionState>? StateChanged;

    public void RetainElapsed(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed <= 0) return;
        PendingWorkTurns += Math.Max(0, elapsed - RemainingTurns);
        RemainingTurns = (float)Math.Max(0, RemainingTurns - elapsed);
    }

    public void Advance(double turns)
    {
        if (IsTransitioning || IsAdvancing || State != ProductionState.Producing
            || !double.IsFinite(turns) || turns < 0) return;
        PendingWorkTurns += turns;
        IsAdvancing = true;
        try
        {
            for (int cycles = 0; cycles < 256 && State == ProductionState.Producing && CanContinue(); cycles++)
            {
                double remaining = float.IsFinite(RemainingTurns) ? Math.Max(0, RemainingTurns) : 0;
                double consumed = Math.Min(PendingWorkTurns, remaining);
                PendingWorkTurns -= consumed;
                RemainingTurns = (float)Math.Max(0, remaining - consumed);
                if (RemainingTurns > 0) break;
                if (!Complete() || State != ProductionState.Producing) { PendingWorkTurns = 0; break; }
                if (PendingWorkTurns <= 0) break;
            }
        }
        finally { IsAdvancing = false; }
    }

    public bool Start(string recipeId = "")
    {
        if (IsTransitioning) return false;
        IsTransitioning = true;
        try
        {
            _wallet = ResolveWallet();
            if (_wallet is null) return Reject(recipeId, "missing_resource_wallet");
            if (State != ProductionState.Idle)
                return Reject(string.IsNullOrWhiteSpace(recipeId) ? CurrentRecipeId : recipeId, "already_producing");
            var recipe = ResolveRecipe(recipeId);
            if (recipe is null) return Reject(recipeId, "missing_recipe");
            if (!recipe.HasOutputs()) return Reject(recipe.RecipeId, "missing_outputs");
            if (ConsumeInputsOnStart && !_wallet.Spend(recipe.Inputs)) return Reject(recipe.RecipeId, "missing_inputs");
            _inputsCommitted = ConsumeInputsOnStart;
            CurrentRecipeId = ActiveRecipeId = recipe.RecipeId;
            RemainingTurns = recipe.EffectiveDurationTurns;
            SetState(ProductionState.Producing);
            Started?.Invoke(recipe.RecipeId);
            return true;
        }
        finally { IsTransitioning = false; }
    }

    public void Pause()
    {
        if (!IsTransitioning && State == ProductionState.Producing) SetState(ProductionState.Paused);
    }

    public void Resume()
    {
        if (!IsTransitioning && State == ProductionState.Paused) SetState(ProductionState.Producing);
    }

    public void Cancel(bool refundInputs)
    {
        if (IsTransitioning) return;
        IsTransitioning = true;
        try
        {
            var recipe = FindRecipeData(CurrentRecipeId);
            bool refund = refundInputs && _inputsCommitted && recipe is not null;
            _inputsCommitted = false;
            PendingWorkTurns = 0;
            CurrentRecipeId = "";
            RemainingTurns = 0;
            SetState(ProductionState.Idle);
            if (refund && GodotObject.IsInstanceValid(_wallet)) _wallet!.Refund(recipe!.Inputs);
        }
        finally { IsTransitioning = false; }
    }

    public bool Complete()
    {
        if (IsTransitioning || State != ProductionState.Producing) return false;
        string recipeId = CurrentRecipeId;
        IsTransitioning = true;
        try
        {
            _wallet = ResolveWallet();
            var recipe = FindRecipeData(recipeId);
            if (_wallet is null || recipe is null) return false;
            if (!_inputsCommitted)
            {
                if (!_wallet.Spend(recipe.Inputs)) return false;
                _inputsCommitted = true;
            }
            foreach (var (resourceId, amount) in GridResourceAmount.Enumerate(recipe.Outputs))
                if (amount > 0 && !string.IsNullOrWhiteSpace(resourceId)) _wallet.AddAmount(resourceId, amount);
            _inputsCommitted = false;
            CurrentRecipeId = "";
            RemainingTurns = 0;
            SetState(ProductionState.Idle);
            Completed?.Invoke(recipeId);
        }
        finally { IsTransitioning = false; }
        if (Loop && State == ProductionState.Idle && CanContinue()) Start(recipeId);
        return true;
    }

    public Godot.Collections.Dictionary Capture(double elapsed = 0) => new()
    {
        ["state"] = (int)State, ["recipe_id"] = CurrentRecipeId,
        ["remaining_turns"] = (float)Math.Max(0, RemainingTurns - elapsed),
        ["inputs_committed"] = _inputsCommitted,
        ["pending_work_turns"] = PendingWorkTurns + Math.Max(0, elapsed - RemainingTurns)
    };

    public void Restore(Godot.Collections.Dictionary state)
    {
        if (IsTransitioning) return;
        int raw = GridVariantReader.Int(state, "state", 0);
        string recipe = GridVariantReader.String(state, "recipe_id", "");
        var restored = Enum.IsDefined(typeof(ProductionState), raw) ? (ProductionState)raw : ProductionState.Idle;
        double pending = state.TryGetValue("pending_work_turns", out Variant value)
            && value.VariantType is Variant.Type.Float or Variant.Type.Int ? value.AsDouble() : 0;
        PendingWorkTurns = double.IsFinite(pending) && pending > 0 ? pending : 0;
        if (restored == ProductionState.Idle || string.IsNullOrWhiteSpace(recipe))
        {
            _inputsCommitted = false;
            PendingWorkTurns = 0;
            CurrentRecipeId = "";
            RemainingTurns = 0;
            SetState(ProductionState.Idle);
            return;
        }
        _wallet = ResolveWallet();
        CurrentRecipeId = ActiveRecipeId = recipe;
        _inputsCommitted = GridVariantReader.Bool(state, "inputs_committed", false);
        float remaining = GridVariantReader.Float(state, "remaining_turns", 0);
        RemainingTurns = float.IsFinite(remaining) ? Math.Max(0, remaining) : 0;
        SetState(restored);
    }

    public GridProductionRecipe? FindRecipe(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        string normalized = Normalize(id);
        foreach (var recipe in GridProductionRecipe.Enumerate(Recipes))
            if (Normalize(recipe.RecipeId) == normalized) return recipe;
        return null;
    }

    public bool HasRecipe(string id) => FindRecipeData(id) is not null;
    public float RecipeDuration(string id) => FindRecipeData(id)?.EffectiveDurationTurns ?? 0;

    private GridProductionRecipeData? FindRecipeData(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        string normalized = Normalize(id);
        foreach (Variant entry in Recipes)
        {
            var recipe = GridProductionRecipeData.Read(entry);
            if (recipe is not null && Normalize(recipe.RecipeId) == normalized) return recipe;
        }
        return null;
    }

    private GridProductionRecipeData? ResolveRecipe(string id)
    {
        if (!string.IsNullOrWhiteSpace(id)) return FindRecipeData(id);
        if (!string.IsNullOrWhiteSpace(ActiveRecipeId)) return FindRecipeData(ActiveRecipeId);
        foreach (Variant entry in Recipes)
        {
            var recipe = GridProductionRecipeData.Read(entry);
            if (recipe is not null && !string.IsNullOrWhiteSpace(recipe.RecipeId)) return recipe;
        }
        return null;
    }

    private bool Reject(string recipe, string reason) { Rejected?.Invoke(recipe, reason); return false; }
    private void SetState(ProductionState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(state);
    }
    private static string Normalize(string value) => string.IsNullOrWhiteSpace(value)
        ? "" : value.Trim().ToLowerInvariant().Replace(' ', '_');
}
