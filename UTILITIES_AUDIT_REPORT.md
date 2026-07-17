# Utilities Audit Report

## Overview
Audit of 20 utility/helper classes to determine which should be converted to proper ECS components vs. remain as static utilities.

---

## Audit Results

### 🟢 FINE AS-IS (Static Utilities)

These are best left as static helpers - no state, no lifecycle needed:

#### 1. **BeepFileUtils.cs**
- **Purpose:** File I/O operations (read, write, delete, directory ops)
- **Why keep static:** Stateless, used throughout codebase
- **Status:** ✅ Keep as-is
- **Current use:** GameStateManagerComponent, SaveGameMenuComponent

#### 2. **BeepInputMapGenerator.cs**
- **Purpose:** Auto-generates Godot input map from configuration
- **Why keep static:** One-time setup operation, no runtime state
- **Status:** ✅ Keep as-is
- **Current use:** BeepGenreGenerator during project creation

#### 3. **BeepProjectDefaults.cs**
- **Purpose:** Sets project.godot configuration values
- **Why keep static:** Configuration-only, no game logic
- **Status:** ✅ Keep as-is
- **Current use:** BeepGenreGenerator during project creation

#### 4. **BeepProjectGenerator.cs**
- **Purpose:** Creates folder structure and base files
- **Why keep static:** Setup utility, no runtime state
- **Status:** ✅ Keep as-is
- **Current use:** BeepGenreGenerator during project creation

#### 5. **BeepWeightedTable.cs**
- **Purpose:** Weighted random selection utility
- **Why keep static:** Mathematical utility, stateless
- **Status:** ✅ Keep as-is (or make a static method cache)
- **Use cases:** Enemy spawning, loot drops, procedural generation

#### 6. **BeepCommandHistory.cs**
- **Purpose:** Undo/redo history tracking
- **Why keep static:** Shared across editor, not runtime
- **Status:** ✅ Keep as-is (editor-only)

#### 7. **BeepServiceLocator.cs**
- **Purpose:** Dependency injection / service registry
- **Why keep static:** Central registry needs to be singleton-like
- **Status:** ✅ Keep as-is (but see Note below)
- **Note:** Could optionally be integrated into GameApp

---

### 🟡 SHOULD CONVERT TO COMPONENTS

These have state/lifecycle and should be proper ECS components:

#### 1. **BeepStateMachine.cs**
- **Purpose:** Generic state machine for gameplay entities
- **Status:** ⚠️ Convert to component
- **Why:** Manages state transitions, needs lifecycle
- **Recommendation:** Create `StateMachineComponent : GameplayComponent`
- **Example use:** Enemy AI states (Idle, Chase, Attack, Flee)
- **Priority:** HIGH - widely used pattern

#### 2. **BeepKeybindManager.cs**
- **Purpose:** Runtime keybinding management
- **Status:** ⚠️ Convert to component
- **Why:** Manages input state, needs to be per-game instance
- **Recommendation:** Create `KeybindManagerComponent : GameplayComponent`
- **Example use:** Allow player to rebind keys during gameplay
- **Priority:** HIGH - player customization

#### 3. **BeepCoroutine.cs**
- **Purpose:** Coroutine/async task manager
- **Status:** ⚠️ Consider converting
- **Why:** Manages active tasks, needs cleanup lifecycle
- **Recommendation:** Create `TaskRunnerComponent : GameplayComponent`
- **Example use:** Timed events, async animations
- **Priority:** MEDIUM - useful pattern but workarounds exist

#### 4. **BeepDataBinder.cs**
- **Purpose:** Two-way data binding between UI and data
- **Status:** ⚠️ Enhance as component
- **Why:** Manages bindings, needs lifecycle for cleanup
- **Recommendation:** Enhance existing as `DataBinderComponent : UIComponent`
- **Example use:** HUD elements bound to player stats
- **Priority:** MEDIUM - UI convenience

---

### 🔴 NEEDS REVIEW / LOW PRIORITY

#### 1. **BeepProceduralAnim.cs**
- **Purpose:** Procedurally generate animations
- **Status:** ❓ Review usage
- **Recommendation:** Keep as utility, add to animation components
- **Priority:** LOW - niche feature

#### 2. **BeepEncryptionPathfinding.cs**
- **Purpose:** Pathfinding with encryption/obfuscation
- **Status:** ❓ Review/document purpose
- **Recommendation:** Keep as utility or create `PathfindingComponent`
- **Priority:** LOW - specialized use case

#### 3. **BeepAchievementDebug.cs**
- **Purpose:** Debug achievements during development
- **Status:** ✅ Keep as-is (editor-only)
- **Recommendation:** Pair with achievement system we built
- **Priority:** LOW - dev tool

#### 4. **BeepTreeView.cs, BeepDataGrid.cs, BeepDropdown.cs, BeepFormBuilder.cs**
- **Purpose:** UI widget helpers
- **Status:** ✅ Keep as-is
- **Recommendation:** These are part of the UI framework
- **Priority:** N/A - stable UI utilities

---

## Conversion Priority Matrix

### Phase 1 (HIGH - Do First)
- [ ] **BeepStateMachine.cs** → `StateMachineComponent`
  - Used in: Enemy AI, gameplay logic
  - Impact: High (multiple systems)
  - Effort: Medium (2-3 hours)

- [ ] **BeepKeybindManager.cs** → `KeybindManagerComponent`
  - Used in: Player input customization
  - Impact: High (player features)
  - Effort: Medium (2-3 hours)

### Phase 2 (MEDIUM - Do Next)
- [ ] **BeepCoroutine.cs** → `TaskRunnerComponent`
  - Used in: Async gameplay events
  - Impact: Medium (useful pattern)
  - Effort: Medium (2-3 hours)

- [ ] **BeepDataBinder.cs** → Enhance as `DataBinderComponent`
  - Used in: HUD/UI updates
  - Impact: Medium (UI convenience)
  - Effort: Small (1-2 hours)

### Phase 3 (LOW - Nice to Have)
- [ ] **BeepProceduralAnim.cs** → Review & document
- [ ] **BeepEncryptionPathfinding.cs** → Review & document
- [ ] **BeepAchievementDebug.cs** → Integrate with achievement system

---

## Recommended Action Plan

### Immediate (Next Session)

1. **Convert BeepStateMachine to StateMachineComponent**
   ```csharp
   public partial class StateMachineComponent : GameplayComponent
   {
       // Existing BeepStateMachine logic wrapped
       public void PushState(IGameState state) { }
       public void PopState() { }
       public void SwitchState(IGameState state) { }
       public IGameState? CurrentState { get; }
   }
   ```

2. **Convert BeepKeybindManager to KeybindManagerComponent**
   ```csharp
   public partial class KeybindManagerComponent : GameplayComponent
   {
       // Runtime keybinding management
       public void RebindKey(string actionName, InputEvent newEvent) { }
       public InputEvent? GetBindingFor(string actionName) { }
       public void SaveBindings() { }
       public void LoadBindings() { }
   }
   ```

### Next Priority

3. **Wrap BeepCoroutine as TaskRunnerComponent**
4. **Enhance BeepDataBinder as DataBinderComponent**
5. **Review specialized utilities** (Pathfinding, ProceduralAnim)

---

## Migration Strategy

For each utility being converted:

1. **Create component wrapper** around existing utility
2. **Add lifecycle hooks** (_Ready, _Process, _ExitTree)
3. **Wire into GameApp** if global state needed
4. **Add ISaveable** if state persistence needed
5. **Document example usage** in component docstring
6. **Update existing code** to use component versions

### Example: StateMachineComponent

```csharp
[Tool]
[GlobalClass]
public partial class StateMachineComponent : GameplayComponent
{
    // Wraps BeepStateMachine with ECS lifecycle
    
    private BeepStateMachine? _fsm;
    
    public override void _Ready()
    {
        base._Ready();
        _fsm = new BeepStateMachine();
    }
    
    public void PushState(IGameState state) => _fsm?.PushState(state);
    public void PopState() => _fsm?.PopState();
    public void SwitchState(IGameState state) => _fsm?.SwitchState(state);
    public IGameState? CurrentState => _fsm?.Current;
    
    public override void _ExitTree()
    {
        _fsm?.Clear();
    }
}
```

---

## Summary Table

| Utility | Current | Should Be | Priority | Effort |
|---------|---------|-----------|----------|--------|
| BeepFileUtils | Static | Static ✅ | - | - |
| BeepInputMapGenerator | Static | Static ✅ | - | - |
| BeepProjectDefaults | Static | Static ✅ | - | - |
| BeepProjectGenerator | Static | Static ✅ | - | - |
| BeepWeightedTable | Static | Static ✅ | - | - |
| BeepCommandHistory | Static | Static ✅ | - | - |
| BeepServiceLocator | Static | Static ✅ | - | - |
| **BeepStateMachine** | Util | **Component** | 🔴 HIGH | Medium |
| **BeepKeybindManager** | Util | **Component** | 🔴 HIGH | Medium |
| **BeepCoroutine** | Util | **Component** | 🟡 MEDIUM | Medium |
| **BeepDataBinder** | Util | **Component** | 🟡 MEDIUM | Small |
| BeepProceduralAnim | Util | Util (review) | 🟢 LOW | Small |
| BeepEncryptionPathfinding | Util | Util (review) | 🟢 LOW | Small |
| BeepTreeView | Util | Static ✅ | - | - |
| BeepDataGrid | Util | Static ✅ | - | - |
| BeepDropdown | Util | Static ✅ | - | - |
| BeepFormBuilder | Util | Static ✅ | - | - |
| BeepAchievementDebug | Util | Util ✅ | - | - |

---

## Recommendations Summary

✅ **Keep as static utilities:**
- File I/O (BeepFileUtils)
- Project generation (BeepProjectGenerator, BeepProjectDefaults, BeepInputMapGenerator)
- Dependency injection (BeepServiceLocator)
- Random selection (BeepWeightedTable)
- UI widgets (BeepTreeView, BeepDataGrid, BeepDropdown, BeepFormBuilder)
- Editor tools (BeepCommandHistory, BeepAchievementDebug)

⚠️ **Convert to ECS components (HIGH PRIORITY):**
- State machines (for AI, gameplay logic)
- Keybind management (for player customization)

⚠️ **Consider converting (MEDIUM PRIORITY):**
- Coroutine/task runner
- Data binding

❓ **Review/document (LOW PRIORITY):**
- Procedural animation
- Pathfinding
- Encryption utilities

---

**Status:** Ready for implementation  
**Estimated total effort:** 8-12 hours for high-priority items  
**Impact:** Standardizes all gameplay-related utilities as proper ECS components
