/*
╔════════════════════════════════════════════════════════════════════════════╗
║                    ENHANCED GAMEAPP USAGE GUIDE                           ║
║              Comprehensive Game State & Session Management                ║
╚════════════════════════════════════════════════════════════════════════════╝

OVERVIEW:
GameApp is the global game state manager accessible from anywhere via:
  GameApp.Instance (C#) or get_node("/root/GameApp") (GDScript)

═══════════════════════════════════════════════════════════════════════════════

TIER 1: SESSION BASICS
═══════════════════════════════════════════════════════════════════════════════

1. GAME RUNNING STATE
───────────────────────
  GameApp.Instance.SetGameRunning(true);   // When game starts
  GameApp.Instance.SetGameRunning(false);  // When game ends

  // Check if game is active
  if (GameApp.Instance.IsGameRunning) { ... }

  Signals:
    - GameRunningChanged(bool running)

2. PAUSE/RESUME
───────────────
  GameApp.Instance.SetPaused(true);       // Pause game
  GameApp.Instance.SetPaused(false);      // Resume game

  // Automatically sets GetTree().Paused for physics/animations

  Signals:
    - GamePaused()
    - GameResumed()

3. SESSION TIMING
──────────────────
  // Automatically tracked when game runs
  double elapsed = GameApp.Instance.CurrentSessionElapsed;  // Seconds
  double total = GameApp.Instance.SessionPlaytimeSeconds;   // This session
  int minutes = GameApp.Instance.TotalPlaytimeMinutes;      // All sessions

4. DIFFICULTY LEVEL
─────────────────────
  GameApp.Instance.SetDifficulty(GameApp.Difficulty.Hard);

  // Score is automatically scaled
  GameApp.Instance.AddSessionScore(100);  // Becomes 150 on Hard

  Multipliers:
    - Easy: 0.5x (50 points)
    - Normal: 1.0x (100 points)
    - Hard: 1.5x (150 points)
    - Nightmare: 2.0x (200 points)

  Signals:
    - DifficultyChanged(int difficulty)

═══════════════════════════════════════════════════════════════════════════════

TIER 2: STATISTICS & PROGRESSION
═══════════════════════════════════════════════════════════════════════════════

1. SCORE TRACKING
──────────────────
  GameApp.Instance.AddSessionScore(50);
  GameApp.Instance.SessionScore;           // Current session score
  GameApp.Instance.BestScore;               // All-time best

  Signals:
    - SessionScoreChanged(int total)

2. LEVEL PROGRESSION
──────────────────────
  GameApp.Instance.SetLevel(5);             // Current level
  GameApp.Instance.CurrentLevel;            // Get current
  GameApp.Instance.MaxLevelReached;         // Best reached
  GameApp.Instance.IsLevelCompleted(5);     // Check if done
  GameApp.Instance.TotalLevelsCompleted;    // Count of completed

  Signals:
    - LevelChanged(int level)
    - LevelCompleted(int level)

3. STATISTICS TRACKING
───────────────────────
  // Automatically tracked
  GameApp.Instance.GamesPlayedTotal;        // Total games started
  GameApp.Instance.GamesWonTotal;           // Games won
  GameApp.Instance.GamesLostTotal;          // Games lost
  GameApp.Instance.WinRate;                 // Percentage (0-100)

  // Record end of game
  GameApp.Instance.RecordGameEnd(true);     // Won
  GameApp.Instance.RecordGameEnd(false);    // Lost

  Signals:
    - SessionEnded(bool won)

═══════════════════════════════════════════════════════════════════════════════

TIER 3: ACHIEVEMENTS & CHECKPOINTS
═══════════════════════════════════════════════════════════════════════════════

1. ACHIEVEMENTS
──────────────────
  GameApp.Instance.UnlockAchievement("first_level_complete");
  GameApp.Instance.HasAchievement("first_level_complete");
  GameApp.Instance.GetUnlockedAchievements();  // List<string>

  Signals:
    - AchievementUnlocked(string achievementId)

2. CHECKPOINTS/QUICKSAVE
─────────────────────────
  GameApp.Instance.SetCheckpoint(3);        // Level 3 as checkpoint
  GameApp.Instance.LastCheckpointLevel;     // Get last checkpoint
  GameApp.Instance.HasQuicksave;            // bool

  Signals:
    - CheckpointReached(int level)

═══════════════════════════════════════════════════════════════════════════════

TIER 4: PERFORMANCE & DEBUG
═══════════════════════════════════════════════════════════════════════════════

1. FPS MONITORING
──────────────────
  GameApp.Instance.CurrentFPS;              // Real-time FPS
  GameApp.Instance.CurrentPerformanceMode;  // Low/Normal/High

2. DEV MODE
──────────────
  GameApp.Instance.ToggleDevMode();

  if (GameApp.Instance.DevModeEnabled)
  {
      GameApp.Instance.InfiniteLives = true;      // Cheat: no death
      GameApp.Instance.SkipTutorial = true;       // Skip intro
      GameApp.Instance.AddSessionScore(9999);     // Instant win
  }

  Signals:
    - DevModeToggled(bool enabled)

═══════════════════════════════════════════════════════════════════════════════

PRACTICAL EXAMPLE: COMPLETE GAME FLOW
═══════════════════════════════════════════════════════════════════════════════

// In MainMenu.cs - User starts new game
GameApp.Instance.ResetSession();
GameApp.Instance.SetDifficulty(GameApp.Difficulty.Normal);
GameApp.Instance.SetGameRunning(true);
// → GameStarted signal fires
// → SessionStarted signal fires
// → IsGameRunning = true (enables Save button)

// In GameFlowComponent - Player gains points
GameApp.Instance.AddSessionScore(100);
// → SessionScoreChanged signal (update UI)
// → Multiplied by difficulty (hard = 150 points)

// In PauseMenu - Player pauses
GameApp.Instance.SetPaused(true);
// → GamePaused signal fires
// → GetTree().Paused = true (stops physics/animations)
// → Save button still available

// In LevelComplete handler - Level done
GameApp.Instance.SetLevel(2);
GameApp.Instance.SetCheckpoint(2);
GameApp.Instance.UnlockAchievement("level_2_complete");
// → LevelChanged signal
// → LevelCompleted signal
// → CheckpointReached signal
// → AchievementUnlocked signal

// When game ends
GameApp.Instance.RecordGameEnd(true);
GameApp.Instance.SetGameRunning(false);
// → SessionEnded(true) signal
// → Statistics updated
// → IsGameRunning = false (Save button disabled)

═══════════════════════════════════════════════════════════════════════════════

KEY FEATURES SUMMARY:

✅ Pause/Resume tracking (separate from running state)
✅ Automatic session timing & playtime tracking
✅ Difficulty levels with score multipliers
✅ Statistics (games played, win rate, best score)
✅ Achievement/unlock system
✅ Checkpoint/quicksave management
✅ Performance monitoring (FPS)
✅ Dev mode with cheats
✅ Comprehensive signal system
✅ Cross-game compatibility (all genres)

═══════════════════════════════════════════════════════════════════════════════
*/
