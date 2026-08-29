using Beep.ECS;
using Beep.ECS.UI;
using Beep.GameBuilder;
using Godot;

[GlobalClass]
public partial class CoreGameplaySmoke : Node
{
    public string Failure { get; private set; } = string.Empty;

    public bool Run()
    {
        Failure = string.Empty;
        return VerifyCooldown()
            && VerifyStatusEffects()
            && VerifyHealth()
            && VerifyCameraZoom()
            && VerifyCombatComponents()
            && VerifyMovementComponents()
            && VerifyAlgorithmComponentBounds()
            && VerifySpawnPickupPlatformBounds()
            && VerifyRuntimeManagerBounds()
            && VerifyLevelLoaderLooseLevelEntries()
            && VerifyGenreStateComponentBounds()
            && VerifySurvivalWeatherAndFeedbackComponents();
    }

    private bool VerifyCooldown()
    {
        var cooldown = new CooldownComponent
        {
            Name = "Cooldown",
            CooldownDuration = -5f
        };
        AddChild(cooldown);

        cooldown.Trigger();
        bool invalidDuration = cooldown.IsReady
            && Mathf.IsEqualApprox(cooldown.Remaining, 0f)
            && Mathf.IsEqualApprox(cooldown.Progress, 1f)
            && Mathf.IsEqualApprox(cooldown.EffectiveDuration, 0f);

        cooldown.CooldownDuration = 2f;
        cooldown.Trigger();
        cooldown._Process(1.0);
        bool progressClamped = !cooldown.IsReady
            && cooldown.Progress > 0.49f
            && cooldown.Progress < 0.51f;

        cooldown.CooldownDuration = float.NaN;
        cooldown.Trigger();
        cooldown._Process(double.NaN);
        bool nonFiniteDurationBounded = cooldown.IsReady
            && Mathf.IsEqualApprox(cooldown.Remaining, 0f)
            && Mathf.IsEqualApprox(cooldown.Progress, 1f)
            && Mathf.IsEqualApprox(cooldown.EffectiveDuration, 0f);

        cooldown.QueueFree();

        return Expect(invalidDuration, "CooldownComponent did not clamp invalid durations.")
            && Expect(progressClamped, "CooldownComponent progress was not bounded against effective duration.")
            && Expect(nonFiniteDurationBounded, "CooldownComponent accepted non-finite duration or frame time.");
    }

    private bool VerifyStatusEffects()
    {
        var status = new StatusEffectComponent { Name = "StatusEffects" };
        AddChild(status);

        status.ApplyEffect("  Speed Up ", 1f, tickInterval: 0f, maxStacks: 0);
        bool normalized = status.HasEffect("speed_up")
            && status.ActiveEffects.Count == 1
            && status.ActiveEffects[0].TickInterval >= 0.01f
            && status.ActiveEffects[0].MaxStacks == 1;

        status.ApplyEffect("speed_up", 1f, stackBehavior: StatusEffectComponent.StackBehavior.Stack, maxStacks: 1);
        bool stackCapped = status.GetActiveEffectCount("speed_up") == 1;

        status.ApplyEffect("   ", 1f);
        bool blankIgnored = status.ActiveEffects.Count == 1;

        status.ApplyEffect("perm", -1f);
        status.ApplyEffect("perm", 2f, stackBehavior: StatusEffectComponent.StackBehavior.Extend);
        bool permanentStayedPermanent = status.GetEffectDuration("perm") < 0f;

        status.ApplyEffect("bad", float.NaN, tickInterval: float.NaN);
        var badEffect = status.ActiveEffects[status.ActiveEffects.Count - 1];
        bool nonFiniteEffectNormalized = badEffect.Id == "bad"
            && Mathf.IsEqualApprox(badEffect.Duration, 0f)
            && Mathf.IsEqualApprox(badEffect.TotalDuration, 0f)
            && badEffect.TickInterval >= 0.01f
            && Mathf.IsEqualApprox(status.GetEffectProgress("bad"), 0f);

        status.QueueFree();

        return Expect(normalized, "StatusEffectComponent did not normalize ids or clamp invalid tick/max-stack values.")
            && Expect(stackCapped, "StatusEffectComponent did not enforce maxStacks for stack behavior.")
            && Expect(blankIgnored, "StatusEffectComponent accepted a blank effect id.")
            && Expect(permanentStayedPermanent, "StatusEffectComponent turned a permanent effect finite when extended.")
            && Expect(nonFiniteEffectNormalized, "StatusEffectComponent accepted non-finite duration or tick interval.");
    }

    private bool VerifyHealth()
    {
        var health = new HealthComponent
        {
            Name = "Health",
            MaxHealth = 0f,
            CurrentHealth = 999f
        };
        AddChild(health);

        var state = new GameStateData();
        health.Save(state);
        bool normalizedSave = Mathf.IsEqualApprox(state.Combat.MaxHealth, 1f)
            && Mathf.IsEqualApprox(state.Combat.Health, 1f)
            && Mathf.IsEqualApprox(health.HealthPercent, 1f);

        state.Combat.MaxHealth = -20f;
        state.Combat.Health = 50f;
        health.Load(state);
        bool normalizedLoad = Mathf.IsEqualApprox(health.MaxHealth, 1f)
            && Mathf.IsEqualApprox(health.CurrentHealth, 1f);

        health.MaxHealth = 100f;
        health.CurrentHealth = 50f;
        health.TakeDamage(new GameDamage(-25f, DamageType.True));
        bool negativeDamageIgnored = Mathf.IsEqualApprox(health.CurrentHealth, 50f);

        health.TakeDamage(new GameDamage(float.NaN, DamageType.True));
        bool nonFiniteDamageIgnored = Mathf.IsEqualApprox(health.CurrentHealth, 50f);

        health.Heal(-10f);
        bool negativeHealIgnored = Mathf.IsEqualApprox(health.CurrentHealth, 50f);

        health.Heal(float.NaN);
        bool nonFiniteHealIgnored = Mathf.IsEqualApprox(health.CurrentHealth, 50f);

        health.Heal(200f);
        bool healClamped = Mathf.IsEqualApprox(health.CurrentHealth, 100f);

        health.MaxHealth = float.NaN;
        health.CurrentHealth = float.NaN;
        health.Save(state);
        bool nonFiniteHealthNormalized = Mathf.IsEqualApprox(state.Combat.MaxHealth, 1f)
            && Mathf.IsEqualApprox(state.Combat.Health, 1f)
            && Mathf.IsEqualApprox(health.HealthPercent, 1f);

        state.Combat.MaxHealth = float.NaN;
        state.Combat.Health = float.NaN;
        health.Load(state);
        bool nonFiniteLoadNormalized = Mathf.IsEqualApprox(health.MaxHealth, 1f)
            && Mathf.IsEqualApprox(health.CurrentHealth, 1f);

        health.QueueFree();

        return Expect(normalizedSave, "HealthComponent did not normalize invalid authored health on save.")
            && Expect(normalizedLoad, "HealthComponent did not normalize invalid loaded health.")
            && Expect(negativeDamageIgnored, "HealthComponent accepted negative true damage as healing.")
            && Expect(nonFiniteDamageIgnored, "HealthComponent accepted non-finite damage.")
            && Expect(negativeHealIgnored, "HealthComponent accepted negative healing.")
            && Expect(nonFiniteHealIgnored, "HealthComponent accepted non-finite healing.")
            && Expect(healClamped, "HealthComponent healing exceeded max health.")
            && Expect(nonFiniteHealthNormalized, "HealthComponent did not normalize non-finite authored health.")
            && Expect(nonFiniteLoadNormalized, "HealthComponent did not normalize non-finite loaded health.");
    }

    private bool VerifyCameraZoom()
    {
        var camera = new Camera2D
        {
            Name = "Camera",
            Zoom = Vector2.One
        };
        AddChild(camera);

        var zoom = new CameraZoomComponent
        {
            Name = "Zoom",
            MinZoom = new Vector2(2f, 2f),
            MaxZoom = new Vector2(0.5f, 0.5f),
            ZoomStep = new Vector2(-0.2f, -0.3f),
            SmoothSpeed = 999f,
            DefaultZoom = 9f
        };
        camera.AddChild(zoom);

        zoom.ZoomIn();
        zoom._Process(1.0);
        bool invertedBoundsAndStep = camera.Zoom.X < 1f
            && camera.Zoom.Y < 1f
            && camera.Zoom.X >= 0.5f
            && camera.Zoom.Y >= 0.5f;

        zoom.ResetZoom();
        zoom._Process(1.0);
        bool resetClamped = camera.Zoom.IsEqualApprox(new Vector2(2f, 2f));

        zoom.MinZoom = new Vector2(float.NaN, 0.25f);
        zoom.MaxZoom = new Vector2(float.PositiveInfinity, float.NaN);
        zoom.ZoomStep = new Vector2(float.NaN, float.NegativeInfinity);
        zoom.SmoothSpeed = float.NaN;
        zoom.DefaultZoom = float.NaN;
        camera.Zoom = new Vector2(float.NaN, float.PositiveInfinity);
        zoom.SetZoom(float.NaN);
        zoom._Process(double.NaN);
        bool nonFiniteZoomBounded = IsFinite(camera.Zoom)
            && Mathf.IsEqualApprox(zoom.EffectiveSmoothSpeed, 0f)
            && camera.Zoom.X >= 0.001f
            && camera.Zoom.Y >= 0.001f;

        camera.QueueFree();

        return Expect(invertedBoundsAndStep, "CameraZoomComponent did not normalize inverted bounds or negative steps.")
            && Expect(resetClamped, "CameraZoomComponent did not clamp ResetZoom to effective bounds.")
            && Expect(nonFiniteZoomBounded, "CameraZoomComponent accepted non-finite zoom tuning or frame time.");
    }

    private bool VerifyCombatComponents()
    {
        return VerifyAttack()
            && VerifyProjectile()
            && VerifyStatsFiniteValues()
            && VerifyDeathConsumableAndImpactFeedbackBounds()
            && VerifyHazard()
            && VerifyAggro()
            && VerifyAiController()
            && VerifyKnockback();
    }

    private bool VerifyAttack()
    {
        var pool = new Node2D { Name = "ProjectilePool" };
        var shooter = new CharacterBody2D { Name = "Shooter" };
        var attack = new AttackComponent
        {
            Name = "Attack",
            Cooldown = -1f,
            Range = -20f,
            ProjectileSpeed = -400f,
            IsRanged = true
        };
        var badProjectileScene = new PackedScene();
        badProjectileScene.Pack(new Node { Name = "NotAProjectileBody" });
        attack.ProjectileScene = badProjectileScene;

        int attacks = 0;
        attack.Attacked += (_, _) => attacks++;
        shooter.AddChild(attack);
        pool.AddChild(shooter);
        AddChild(pool);

        attack.Attack(shooter.GlobalPosition);
        bool invalidProjectileRejected = attacks == 0
            && Mathf.IsEqualApprox(attack.CooldownRemaining, 0f)
            && Mathf.IsEqualApprox(attack.EffectiveCooldown, 0f)
            && Mathf.IsEqualApprox(attack.EffectiveRange, 0f)
            && Mathf.IsEqualApprox(attack.EffectiveProjectileSpeed, 0f);

        attack.Cooldown = float.NaN;
        attack.Range = float.NaN;
        attack.ProjectileSpeed = float.NaN;
        attack._Process(double.NaN);
        bool nonFiniteAuthoredValuesBounded = Mathf.IsEqualApprox(attack.CooldownRemaining, 0f)
            && Mathf.IsEqualApprox(attack.EffectiveCooldown, 0f)
            && Mathf.IsEqualApprox(attack.EffectiveRange, 0f)
            && Mathf.IsEqualApprox(attack.EffectiveProjectileSpeed, 0f);

        pool.QueueFree();
        return Expect(invalidProjectileRejected, "AttackComponent accepted invalid cooldown/range/projectile scene state.")
            && Expect(nonFiniteAuthoredValuesBounded, "AttackComponent accepted non-finite cooldown/range/projectile values.");
    }

    private bool VerifyProjectile()
    {
        var area = new Area2D { Name = "ProjectileArea" };
        var modifier = new ProjectileModifierComponent { Name = "Modifier", IsActive = false };
        var projectile = new ProjectileComponent
        {
            Name = "Projectile",
            Speed = -100f,
            MaxLifetime = 5f
        };
        area.AddChild(modifier);
        area.AddChild(projectile);
        AddChild(area);

        projectile.Launch(Vector2.Right);
        projectile._Process(1.0);
        bool inactiveModifierDidNotFreeze = area.Position.IsEqualApprox(Vector2.Zero);

        projectile.Speed = 50f;
        projectile.Launch(Vector2.Right);
        projectile._Process(1.0);
        bool movesWhenSpeedValid = area.Position.X > 49f && area.Position.X < 51f;

        projectile.Speed = float.NaN;
        projectile.MaxLifetime = float.NaN;
        projectile.Damage = float.NaN;
        projectile.GravityStrength = float.NaN;
        projectile.ArcHeight = float.NaN;
        projectile.Launch(new Vector2(float.NaN, 1f));
        projectile._Process(double.NaN);
        bool nonFiniteProjectileValuesBounded = IsFinite(area.Position)
            && Mathf.IsEqualApprox(projectile.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(projectile.EffectiveMaxLifetime, 0f)
            && Mathf.IsEqualApprox(projectile.EffectiveDamage, 0f)
            && Mathf.IsEqualApprox(projectile.EffectiveGravityStrength, 0f)
            && Mathf.IsEqualApprox(projectile.EffectiveArcGravity, 980f)
            && Mathf.IsEqualApprox(projectile.EffectiveArcHeight, 1f);

        var modifierBody = new Node2D { Name = "ModifierBody" };
        var activeModifier = new ProjectileModifierComponent
        {
            Name = "ActiveModifier",
            Speed = float.NaN,
            HomingStrength = float.NaN,
            MaxBounces = -2
        };
        modifierBody.AddChild(activeModifier);
        AddChild(modifierBody);
        activeModifier.SetLaunch(new Vector2(float.NaN, float.PositiveInfinity), float.NaN);
        activeModifier._PhysicsProcess(double.NaN);
        bool modifierFiniteSafe = IsFinite(modifierBody.GlobalPosition)
            && Mathf.IsEqualApprox(activeModifier.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(activeModifier.EffectiveHomingStrength, 0f)
            && activeModifier.EffectiveMaxBounces == 0;

        area.QueueFree();
        modifierBody.QueueFree();
        return Expect(inactiveModifierDidNotFreeze, "ProjectileComponent did not clamp negative speed.")
            && Expect(movesWhenSpeedValid, "ProjectileComponent delegated movement to an inactive modifier.")
            && Expect(nonFiniteProjectileValuesBounded, "ProjectileComponent accepted non-finite speed/lifetime/damage/gravity values.")
            && Expect(modifierFiniteSafe, "ProjectileModifierComponent accepted non-finite launch or homing values.");
    }

    private bool VerifyStatsFiniteValues()
    {
        var stat = new Stat { Id = "move_speed", BaseValue = float.NaN };
        stat.AddModifier(new StatModifier { Stat = "move_speed", Op = StatOp.Add, Amount = float.NaN });
        bool badModifierIgnored = stat.Modifiers.Count == 0 && Mathf.IsEqualApprox(stat.Value, 0f);

        stat.AddModifier(new StatModifier { Stat = "move_speed", Op = StatOp.Multiply, Amount = float.PositiveInfinity });
        bool badMultiplierIgnored = stat.Modifiers.Count == 0 && Mathf.IsEqualApprox(stat.Value, 0f);

        stat.BaseValue = 10f;
        var expiring = new StatModifier { Stat = "move_speed", Op = StatOp.Add, Amount = 5f, Duration = float.NaN };
        stat.AddModifier(expiring);
        stat.TickDurations(1f);
        bool nonFiniteDurationExpired = stat.Modifiers.Count == 0 && Mathf.IsEqualApprox(stat.Value, 10f);

        var weapon = new GameWeapon
        {
            Damage = float.NaN,
            Range = float.PositiveInfinity,
            Cooldown = float.NegativeInfinity,
            AmmoPerUse = -3
        };
        bool weaponBounded = Mathf.IsEqualApprox(weapon.EffectiveDamage, 0f)
            && Mathf.IsEqualApprox(weapon.EffectiveRange, 0f)
            && Mathf.IsEqualApprox(weapon.EffectiveCooldown, 0f)
            && weapon.EffectiveAmmoPerUse == 0;

        var armor = new GameArmor
        {
            Defense = float.NaN,
            Fire = float.PositiveInfinity,
            Ice = -4f
        };
        bool armorBounded = Mathf.IsEqualApprox(armor.EffectiveDefense, 0f)
            && Mathf.IsEqualApprox(armor.ResistFor(DamageType.Fire), 1f)
            && Mathf.IsEqualApprox(armor.ResistFor(DamageType.Ice), 0f);

        var shield = new GameShield
        {
            Defense = float.NaN,
            BlockChance = 4f,
            Poison = float.NaN
        };
        bool shieldBounded = Mathf.IsEqualApprox(shield.EffectiveDefense, 0f)
            && Mathf.IsEqualApprox(shield.EffectiveBlockChance, 1f)
            && Mathf.IsEqualApprox(shield.ResistFor(DamageType.Poison), 1f);

        var equipment = new GameEquipment { SocketCount = 99 };
        bool equipmentBounded = equipment.EffectiveSocketCount == 16;

        var starterEquipment = new GameEquipment { Id = "smoke_starter_sword", Slot = EquipSlot.MainHand };
        var savedEquipment = new GameEquipment { Id = "smoke_saved_hat", Slot = EquipSlot.Head };
        GameItemCatalog.Register(starterEquipment);
        GameItemCatalog.Register(savedEquipment);
        var equipmentComponent = new EquipmentComponent { Name = "Equipment" };
        AddChild(equipmentComponent);
        equipmentComponent.Equip(starterEquipment);
        var badEquipmentState = new GameStateData();
        badEquipmentState.GameData["equipment"] = new Resource();
        equipmentComponent.Load(badEquipmentState);
        bool equipmentLoadGuarded = equipmentComponent.Get(EquipSlot.MainHand) == starterEquipment;

        var savedEquipmentState = new GameStateData();
        savedEquipmentState.GameData["equipment"] = new Godot.Collections.Dictionary
        {
            [EquipSlot.Head.ToString()] = "smoke_saved_hat",
            ["BadSlot"] = "smoke_starter_sword"
        };
        equipmentComponent.Load(savedEquipmentState);
        equipmentLoadGuarded = equipmentLoadGuarded
            && equipmentComponent.Get(EquipSlot.MainHand) == null
            && equipmentComponent.Get(EquipSlot.Head) == savedEquipment;

        var resistance = new ResistanceComponent
        {
            Physical = float.NaN,
            Fire = -2f,
            Ice = float.PositiveInfinity
        };
        bool resistanceBounded = Mathf.IsEqualApprox(resistance.EffectivePhysical, 1f)
            && Mathf.IsEqualApprox(resistance.EffectiveFire, 0f)
            && Mathf.IsEqualApprox(resistance.EffectiveIce, 1f)
            && Mathf.IsEqualApprox(resistance.ApplyResistance(float.NaN, DamageType.Physical), 0f)
            && Mathf.IsEqualApprox(resistance.ApplyResistance(10f, DamageType.Fire), 0f);
        equipmentComponent.QueueFree();

        return Expect(badModifierIgnored, "Stat accepted a non-finite additive modifier or base value.")
            && Expect(badMultiplierIgnored, "Stat accepted a non-finite multiplier.")
            && Expect(nonFiniteDurationExpired, "Stat kept a modifier with non-finite duration.")
            && Expect(weaponBounded, "GameWeapon accepted invalid damage/range/cooldown/ammo values.")
            && Expect(armorBounded, "GameArmor accepted invalid defense or resistance multipliers.")
            && Expect(shieldBounded, "GameShield accepted invalid defense, block chance, or resistance multipliers.")
            && Expect(equipmentBounded, "GameEquipment accepted an invalid socket count.")
            && Expect(equipmentLoadGuarded, "EquipmentComponent did not ignore malformed saved equipment data.")
            && Expect(resistanceBounded, "ResistanceComponent accepted invalid damage or multiplier values.");
    }

    private bool VerifyDeathConsumableAndImpactFeedbackBounds()
    {
        var body = new CharacterBody2D { Name = "FeedbackBody" };
        var health = new HealthComponent { Name = "Health", MaxHealth = 100f, CurrentHealth = 50f };
        var status = new StatusEffectComponent { Name = "Status" };
        var consumable = new ConsumableUseComponent
        {
            Name = "ConsumableUse",
            DefaultEffectDuration = float.NaN
        };
        body.AddChild(health);
        body.AddChild(status);
        body.AddChild(consumable);
        AddChild(body);

        var badConsumable = new GameConsumable
        {
            Id = "bad_heal",
            HealAmount = float.NaN,
            StatusEffectId = "focus",
            Duration = float.NaN
        };
        bool consumed = consumable.Use(badConsumable);
        bool consumableBounded = consumed
            && Mathf.IsEqualApprox(consumable.EffectiveDefaultEffectDuration, 0f)
            && Mathf.IsEqualApprox(health.CurrentHealth, 50f);

        var gameOver = new GameOverOnDeathComponent { LivesToLose = -4 };
        bool gameOverBounded = gameOver.EffectiveLivesToLose == 0;

        var hitStop = new HitStopComponent
        {
            FreezeDuration = float.NaN,
            MinDamageThreshold = float.NegativeInfinity
        };
        bool hitStopBounded = Mathf.IsEqualApprox(hitStop.EffectiveFreezeDuration, 0f)
            && Mathf.IsEqualApprox(hitStop.EffectiveMinDamageThreshold, 0f);

        var flash = new FlashComponent
        {
            FlashColor = new Color(float.NaN, 1f, 1f, 1f),
            FlashDuration = float.NaN,
            FlashCount = -2
        };
        bool flashBounded = flash.EffectiveFlashColor.IsEqualApprox(Colors.White)
            && Mathf.IsEqualApprox(flash.EffectiveFlashDuration, 0.1f)
            && flash.EffectiveFlashCount == 0;

        var hitSound = new HitSoundComponent
        {
            MinDamage = float.NaN,
            VolumeDb = float.PositiveInfinity,
            PitchVariation = -4f,
            Bus = "   "
        };
        bool hitSoundBounded = Mathf.IsEqualApprox(hitSound.EffectiveMinDamage, 0f)
            && Mathf.IsEqualApprox(hitSound.EffectiveVolumeDb, -4f)
            && Mathf.IsEqualApprox(hitSound.EffectivePitchVariation, 0.99f)
            && hitSound.EffectiveBus == "Master";

        var spark = new HitSparkComponent
        {
            MinDamage = float.NaN,
            SparkColor = new Color(1f, float.NaN, 1f, 1f)
        };
        bool sparkBounded = Mathf.IsEqualApprox(spark.EffectiveMinDamage, 0f)
            && spark.EffectiveSparkColor.IsEqualApprox(new Color(1f, 0.8f, 0.2f, 1f));

        body.QueueFree();
        gameOver.QueueFree();
        hitStop.QueueFree();
        flash.QueueFree();
        hitSound.QueueFree();
        spark.QueueFree();

        return Expect(consumableBounded, "ConsumableUseComponent accepted non-finite heal/default duration values.")
            && Expect(gameOverBounded, "GameOverOnDeathComponent accepted a negative lives-to-lose value.")
            && Expect(hitStopBounded, "HitStopComponent accepted invalid freeze duration or damage threshold.")
            && Expect(flashBounded, "FlashComponent accepted invalid color, duration, or count values.")
            && Expect(hitSoundBounded, "HitSoundComponent accepted invalid damage, pitch, volume, or bus values.")
            && Expect(sparkBounded, "HitSparkComponent accepted invalid damage threshold or color values.");
    }

    private bool VerifyHazard()
    {
        var area = new Area2D { Name = "HazardArea" };
        var hazard = new HazardProbe
        {
            Name = "Hazard",
            Damage = -50f,
            TickInterval = 0f,
            HazardHalfThickness = -12f
        };
        area.AddChild(hazard);
        AddChild(area);

        var body = new CharacterBody2D { Name = "Victim" };
        var health = new HealthComponent { Name = "Health", MaxHealth = 100f, CurrentHealth = 100f };
        body.AddChild(health);
        AddChild(body);

        hazard.Enter(body);
        bool negativeDamageIgnored = Mathf.IsEqualApprox(health.CurrentHealth, 100f)
            && Mathf.IsEqualApprox(hazard.EffectiveDamage, 0f)
            && hazard.EffectiveTickInterval >= 0.01f
            && Mathf.IsEqualApprox(hazard.EffectiveHazardHalfThickness, 0f);

        hazard.Damage = float.NaN;
        hazard.TickInterval = float.NaN;
        hazard.HazardHeight = float.NaN;
        hazard.HazardHalfThickness = float.NaN;
        hazard._Process(double.NaN);
        bool nonFiniteHazardValuesBounded = Mathf.IsEqualApprox(health.CurrentHealth, 100f)
            && Mathf.IsEqualApprox(hazard.EffectiveDamage, 0f)
            && hazard.EffectiveTickInterval >= 0.01f
            && Mathf.IsEqualApprox(hazard.EffectiveHazardHeight, 0f)
            && Mathf.IsEqualApprox(hazard.EffectiveHazardHalfThickness, 0f);

        hazard.Damage = 10f;
        hazard.Enter(body);
        float afterEntry = health.CurrentHealth;
        hazard._Process(0.005);
        bool shortTickDidNotHit = Mathf.IsEqualApprox(health.CurrentHealth, afterEntry);
        hazard._Process(0.005);
        bool effectiveTickHit = health.CurrentHealth < afterEntry;

        area.QueueFree();
        body.QueueFree();
        return Expect(negativeDamageIgnored, "HazardComponent accepted negative damage or invalid timing/thickness values.")
            && Expect(nonFiniteHazardValuesBounded, "HazardComponent accepted non-finite damage/timing/height values.")
            && Expect(shortTickDidNotHit, "HazardComponent repeated damage before the effective tick interval.")
            && Expect(effectiveTickHit, "HazardComponent did not repeat damage after the effective tick interval.");
    }

    private bool VerifyAggro()
    {
        var body = new Node2D { Name = "AggroBody" };
        var aggro = new AggroComponent
        {
            Name = "Aggro",
            DeaggroRange = 500f,
            ThreatDecayRate = 2f
        };
        body.AddChild(aggro);
        AddChild(body);

        var target = new Node2D { Name = "Threat" };
        AddChild(target);

        aggro.AddThreat(target, -10f);
        bool negativeThreatIgnored = aggro.ThreatTable.Count == 0;

        aggro.AddThreat(target, float.NaN);
        bool nonFiniteThreatIgnored = aggro.ThreatTable.Count == 0;

        aggro.AddThreat(target, 10f);
        bool processDidNotMutateDuringEnumeration = true;
        try
        {
            aggro._Process(1.0);
        }
        catch
        {
            processDidNotMutateDuringEnumeration = false;
        }

        bool decayed = aggro.ThreatTable.TryGetValue(target, out float threat)
            && threat > 7.9f
            && threat < 8.1f;

        aggro.DeaggroRange = float.NaN;
        aggro.ThreatDecayRate = float.NaN;
        aggro._Process(double.NaN);
        bool nonFiniteAggroValuesBounded = aggro.ThreatTable.TryGetValue(target, out float stableThreat)
            && stableThreat > 7.9f
            && stableThreat < 8.1f
            && Mathf.IsEqualApprox(aggro.EffectiveDeaggroRange, 0f)
            && Mathf.IsEqualApprox(aggro.EffectiveThreatDecayRate, 0f);

        body.QueueFree();
        target.QueueFree();
        return Expect(negativeThreatIgnored, "AggroComponent accepted negative threat.")
            && Expect(nonFiniteThreatIgnored, "AggroComponent accepted non-finite threat.")
            && Expect(processDidNotMutateDuringEnumeration, "AggroComponent mutated its threat table while enumerating.")
            && Expect(decayed, "AggroComponent did not decay threat using the effective decay rate.")
            && Expect(nonFiniteAggroValuesBounded, "AggroComponent accepted non-finite deaggro or decay values.");
    }

    private bool VerifyAiController()
    {
        var body = new CharacterBody2D { Name = "AiBody" };
        var status = new StatusEffectComponent { Name = "Status" };
        var ai = new AIController
        {
            Name = "AI",
            Mode = AIController.AIMode.Wander,
            Speed = -100f,
            WanderChangeRate = 99f
        };
        body.AddChild(status);
        body.AddChild(ai);
        AddChild(body);

        status.ApplyEffect("stun", 1f);
        body.Velocity = new Vector2(100f, 0f);
        ai._PhysicsProcess(1.0);
        bool stunStopsMovement = body.Velocity.IsEqualApprox(Vector2.Zero);

        ai.Speed = float.NaN;
        ai.DetectionRange = float.NaN;
        ai.AttackRange = float.NaN;
        ai.WanderChangeRate = float.NaN;
        bool nonFiniteAiValuesBounded = Mathf.IsEqualApprox(ai.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(ai.EffectiveDetectionRange, 0f)
            && Mathf.IsEqualApprox(ai.EffectiveAttackRange, 0f)
            && Mathf.IsEqualApprox(ai.EffectiveWanderChangeRate, 0f);

        var patrolBody = new CharacterBody2D { Name = "PatrolBody" };
        var patrol = new AIController
        {
            Name = "PatrolAI",
            Mode = AIController.AIMode.Patrol,
            Speed = 100f,
            Waypoints = new[] { new NodePath("MissingWaypoint") }
        };
        patrolBody.AddChild(patrol);
        AddChild(patrolBody);
        patrolBody.Velocity = new Vector2(100f, 0f);
        patrol._PhysicsProcess(1.0);
        bool invalidWaypointStops = patrolBody.Velocity.IsEqualApprox(Vector2.Zero);

        body.QueueFree();
        patrolBody.QueueFree();
        return Expect(stunStopsMovement, "AIController kept moving while stunned or accepted negative speed.")
            && Expect(nonFiniteAiValuesBounded, "AIController accepted non-finite movement/detection/wander values.")
            && Expect(invalidWaypointStops, "AIController kept stale movement when a patrol waypoint was invalid.");
    }

    private bool VerifyKnockback()
    {
        var body = new CharacterBody2D { Name = "KnockbackBody" };
        var knockback = new KnockbackComponent
        {
            Name = "Knockback",
            Strength = -200f,
            Friction = -100f,
            Duration = -1f,
            MaxKnockbackMagnitude = -50f
        };
        body.AddChild(knockback);
        AddChild(body);

        knockback.ApplyKnockback(new Vector2(-10f, 0f));
        knockback._PhysicsProcess(1.0);
        bool invalidValuesIgnored = body.Velocity.IsEqualApprox(Vector2.Zero)
            && Mathf.IsEqualApprox(knockback.EffectiveStrength, 0f)
            && Mathf.IsEqualApprox(knockback.EffectiveFriction, 0f)
            && Mathf.IsEqualApprox(knockback.EffectiveDuration, 0f)
            && Mathf.IsEqualApprox(knockback.EffectiveMaxKnockbackMagnitude, 0f);

        knockback.Strength = float.NaN;
        knockback.Friction = float.NaN;
        knockback.Duration = float.NaN;
        knockback.MaxKnockbackMagnitude = float.NaN;
        knockback.ApplyKnockback(new Vector2(float.NaN, 0f));
        knockback._PhysicsProcess(double.NaN);
        bool nonFiniteKnockbackValuesBounded = body.Velocity.IsEqualApprox(Vector2.Zero)
            && Mathf.IsEqualApprox(knockback.EffectiveStrength, 0f)
            && Mathf.IsEqualApprox(knockback.EffectiveFriction, 0f)
            && Mathf.IsEqualApprox(knockback.EffectiveDuration, 0f)
            && Mathf.IsEqualApprox(knockback.EffectiveMaxKnockbackMagnitude, 0f);

        body.QueueFree();
        return Expect(invalidValuesIgnored, "KnockbackComponent accepted negative force/timing bounds.")
            && Expect(nonFiniteKnockbackValuesBounded, "KnockbackComponent accepted non-finite force/timing bounds.");
    }

    private bool VerifyMovementComponents()
    {
        return VerifyMovementComponent()
            && VerifyControllerEffectiveValues()
            && VerifyJumpAndAbilityEffectiveValues();
    }

    private bool VerifyMovementComponent()
    {
        var body = new CharacterBody2D { Name = "MovementBody" };
        var movement = new MovementComponent
        {
            Name = "Movement",
            Speed = -10f,
            Acceleration = -20f,
            Friction = -30f
        };
        body.AddChild(movement);
        AddChild(body);

        movement.Move(Vector2.Right, 1.0);
        bool invalidValuesBounded = movement.Velocity.IsEqualApprox(Vector2.Zero)
            && Mathf.IsEqualApprox(movement.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(movement.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(movement.EffectiveFriction, 0f);

        movement.Speed = 40f;
        movement.Acceleration = 100f;
        movement.Move(new Vector2(3f, 4f), 1.0);
        bool directionNormalized = movement.Velocity.Length() <= 40.01f;

        movement.Speed = float.NaN;
        movement.Acceleration = float.PositiveInfinity;
        movement.Friction = float.NaN;
        movement.Velocity = new Vector2(float.NaN, 2f);
        movement.Move(new Vector2(float.NaN, 1f), double.NaN);
        bool nonFiniteMovementBounded = movement.Velocity.IsEqualApprox(Vector2.Zero)
            && movement.DesiredDirection.IsEqualApprox(Vector2.Zero)
            && Mathf.IsEqualApprox(movement.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(movement.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(movement.EffectiveFriction, 0f);

        body.QueueFree();
        return Expect(invalidValuesBounded, "MovementComponent accepted negative speed/acceleration/friction.")
            && Expect(directionNormalized, "MovementComponent scaled speed by an unnormalized direction.")
            && Expect(nonFiniteMovementBounded, "MovementComponent accepted non-finite speed/acceleration/friction/vector values.");
    }

    private bool VerifyControllerEffectiveValues()
    {
        var topDown = new TopDownController { Speed = -1f, Acceleration = -2f, Friction = -3f };
        var platformer = new PlatformerController
        {
            Speed = -1f,
            Gravity = -2f,
            JumpVelocity = 450f,
            Acceleration = -3f,
            Friction = -4f,
            CoyoteTime = -5f,
            JumpBufferTime = -6f
        };
        var shooter = new ShooterController
        {
            MoveSpeed = -1f,
            FireRate = 0f,
            ProjectileDamage = -2f,
            ProjectileSpeed = -3f
        };

        bool topDownBounded = Mathf.IsEqualApprox(topDown.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(topDown.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(topDown.EffectiveFriction, 0f);
        bool platformerBounded = Mathf.IsEqualApprox(platformer.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveGravity, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveJumpVelocity, -450f)
            && Mathf.IsEqualApprox(platformer.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveFriction, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveCoyoteTime, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveJumpBufferTime, 0f);
        bool shooterBounded = Mathf.IsEqualApprox(shooter.EffectiveMoveSpeed, 0f)
            && shooter.EffectiveFireRate >= 0.01f
            && Mathf.IsEqualApprox(shooter.EffectiveProjectileDamage, 0f)
            && Mathf.IsEqualApprox(shooter.EffectiveProjectileSpeed, 0f);

        topDown.Speed = float.NaN;
        topDown.Acceleration = float.PositiveInfinity;
        topDown.Friction = float.NaN;
        platformer.Speed = float.NaN;
        platformer.Gravity = float.NaN;
        platformer.JumpVelocity = float.NaN;
        platformer.Acceleration = float.NaN;
        platformer.Friction = float.NaN;
        platformer.CoyoteTime = float.NaN;
        platformer.JumpBufferTime = float.NaN;
        shooter.MoveSpeed = float.NaN;
        shooter.FireRate = float.NaN;
        shooter.ProjectileDamage = float.NaN;
        shooter.ProjectileSpeed = float.NaN;
        bool controllerNonFiniteBounded = Mathf.IsEqualApprox(topDown.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(topDown.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(topDown.EffectiveFriction, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveSpeed, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveGravity, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveJumpVelocity, -450f)
            && Mathf.IsEqualApprox(platformer.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveFriction, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveCoyoteTime, 0f)
            && Mathf.IsEqualApprox(platformer.EffectiveJumpBufferTime, 0f)
            && Mathf.IsEqualApprox(shooter.EffectiveMoveSpeed, 0f)
            && Mathf.IsEqualApprox(shooter.EffectiveFireRate, 0.01f)
            && Mathf.IsEqualApprox(shooter.EffectiveProjectileDamage, 0f)
            && Mathf.IsEqualApprox(shooter.EffectiveProjectileSpeed, 0f);

        topDown.QueueFree();
        platformer.QueueFree();
        shooter.QueueFree();
        return Expect(topDownBounded, "TopDownController did not bound movement tuning.")
            && Expect(platformerBounded, "PlatformerController did not bound movement/jump tuning.")
            && Expect(shooterBounded, "ShooterController did not bound movement/fire/projectile tuning.")
            && Expect(controllerNonFiniteBounded, "Controllers accepted non-finite movement/fire/projectile tuning.");
    }

    private bool VerifyJumpAndAbilityEffectiveValues()
    {
        var body = new CharacterBody2D { Name = "AbilityBody" };
        var jump = new JumpComponent
        {
            Name = "Jump",
            JumpForce = 300f,
            MaxJumps = -2,
            VariableJumpMultiplier = 2f,
            VariableJumpCutDuration = 0f,
            CoyoteTime = -1f,
            JumpBufferTime = -1f,
            ApexHangMultiplier = -1f,
            ApexThreshold = 0f
        };
        body.AddChild(jump);
        AddChild(body);

        jump.ForceJump(250f);
        bool jumpBounded = Mathf.IsEqualApprox(body.Velocity.Y, -250f)
            && Mathf.IsEqualApprox(jump.EffectiveJumpForce, -300f)
            && jump.EffectiveMaxJumps == 0
            && Mathf.IsEqualApprox(jump.EffectiveVariableJumpMultiplier, 1f)
            && jump.EffectiveVariableJumpCutDuration > 0f
            && Mathf.IsEqualApprox(jump.EffectiveCoyoteTime, 0f)
            && Mathf.IsEqualApprox(jump.EffectiveJumpBufferTime, 0f)
            && Mathf.IsEqualApprox(jump.EffectiveApexHangMultiplier, 0f)
            && jump.EffectiveApexThreshold > 0f;

        var dash = new DashComponent { DashSpeed = -1f, DashDuration = -2f, DashCooldown = -3f, StaminaCost = -4f };
        var slide = new SlideComponent { SlideSpeed = -1f, SlideDuration = -2f, SlideDeceleration = -3f, HeightMultiplier = 4f };
        var wall = new WallJumpComponent { RayDistance = -1f, WallSlideSpeed = -2f, WallStickTime = -3f, WallJumpForceX = -4f, WallJumpForceY = 500f, WallJumpLockTime = -5f };
        var glide = new GlideComponent { GlideFallSpeed = -1f, GlideAirSpeed = -2f, GlideAccel = -3f };
        var hover = new HoverComponent { HoverGravity = -1f, MaxHoverTime = -2f, HoverCooldown = -3f };
        var fly = new FlyComponent { MaxSpeed = -1f, Acceleration = -2f, Friction = -3f, TurnSpeed = -4f, BoostMultiplier = -5f, BoostDuration = -6f, MaxBankAngle = -7f, BankSpeed = -8f };

        bool abilityValuesBounded = Mathf.IsEqualApprox(dash.EffectiveDashSpeed, 0f)
            && Mathf.IsEqualApprox(dash.EffectiveDashDuration, 0f)
            && Mathf.IsEqualApprox(dash.EffectiveDashCooldown, 0f)
            && Mathf.IsEqualApprox(dash.EffectiveStaminaCost, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveSlideSpeed, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveSlideDuration, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveSlideDeceleration, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveHeightMultiplier, 1f)
            && Mathf.IsEqualApprox(wall.EffectiveRayDistance, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallSlideSpeed, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallStickTime, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallJumpForceX, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallJumpForceY, -500f)
            && Mathf.IsEqualApprox(wall.EffectiveWallJumpLockTime, 0f)
            && Mathf.IsEqualApprox(glide.EffectiveGlideFallSpeed, 0f)
            && Mathf.IsEqualApprox(glide.EffectiveGlideAirSpeed, 0f)
            && Mathf.IsEqualApprox(glide.EffectiveGlideAccel, 0f)
            && Mathf.IsEqualApprox(hover.EffectiveHoverGravity, 0f)
            && Mathf.IsEqualApprox(hover.EffectiveMaxHoverTime, 0f)
            && Mathf.IsEqualApprox(hover.EffectiveHoverCooldown, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveMaxSpeed, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveFriction, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveTurnSpeed, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveBoostMultiplier, 1f)
            && Mathf.IsEqualApprox(fly.EffectiveBoostDuration, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveMaxBankAngle, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveBankSpeed, 0f);

        jump.JumpForce = float.NaN;
        jump.VariableJumpMultiplier = float.NaN;
        jump.VariableJumpCutDuration = float.NaN;
        jump.CoyoteTime = float.NaN;
        jump.JumpBufferTime = float.NaN;
        jump.ApexHangMultiplier = float.NaN;
        jump.ApexThreshold = float.NaN;
        jump.ForceJump(float.NaN);
        dash.DashSpeed = float.NaN;
        dash.DashDuration = float.NaN;
        dash.DashCooldown = float.NaN;
        dash.StaminaCost = float.NaN;
        slide.SlideSpeed = float.NaN;
        slide.SlideDuration = float.NaN;
        slide.SlideDeceleration = float.NaN;
        slide.HeightMultiplier = float.NaN;
        wall.RayDistance = float.NaN;
        wall.WallSlideSpeed = float.NaN;
        wall.WallStickTime = float.NaN;
        wall.WallJumpForceX = float.NaN;
        wall.WallJumpForceY = float.NaN;
        wall.WallJumpLockTime = float.NaN;
        glide.GlideFallSpeed = float.NaN;
        glide.GlideAirSpeed = float.NaN;
        glide.GlideAccel = float.NaN;
        hover.HoverGravity = float.NaN;
        hover.MaxHoverTime = float.NaN;
        hover.HoverCooldown = float.NaN;
        fly.MaxSpeed = float.NaN;
        fly.Acceleration = float.NaN;
        fly.Friction = float.NaN;
        fly.TurnSpeed = float.NaN;
        fly.BoostMultiplier = float.NaN;
        fly.BoostDuration = float.NaN;
        fly.MaxBankAngle = float.NaN;
        fly.BankSpeed = float.NaN;
        fly.ApplyExternalForce(new Vector2(float.NaN, 0f));
        bool abilityNonFiniteBounded = IsFinite(body.Velocity)
            && Mathf.IsEqualApprox(jump.EffectiveJumpForce, -450f)
            && Mathf.IsEqualApprox(jump.EffectiveVariableJumpMultiplier, 1f)
            && jump.EffectiveVariableJumpCutDuration > 0f
            && Mathf.IsEqualApprox(jump.EffectiveCoyoteTime, 0f)
            && Mathf.IsEqualApprox(jump.EffectiveJumpBufferTime, 0f)
            && Mathf.IsEqualApprox(jump.EffectiveApexHangMultiplier, 1f)
            && jump.EffectiveApexThreshold > 0f
            && Mathf.IsEqualApprox(dash.EffectiveDashSpeed, 0f)
            && Mathf.IsEqualApprox(dash.EffectiveDashDuration, 0f)
            && Mathf.IsEqualApprox(dash.EffectiveDashCooldown, 0f)
            && Mathf.IsEqualApprox(dash.EffectiveStaminaCost, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveSlideSpeed, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveSlideDuration, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveSlideDeceleration, 0f)
            && Mathf.IsEqualApprox(slide.EffectiveHeightMultiplier, 1f)
            && Mathf.IsEqualApprox(wall.EffectiveRayDistance, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallSlideSpeed, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallStickTime, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallJumpForceX, 0f)
            && Mathf.IsEqualApprox(wall.EffectiveWallJumpForceY, -400f)
            && Mathf.IsEqualApprox(wall.EffectiveWallJumpLockTime, 0f)
            && Mathf.IsEqualApprox(glide.EffectiveGlideFallSpeed, 0f)
            && Mathf.IsEqualApprox(glide.EffectiveGlideAirSpeed, 0f)
            && Mathf.IsEqualApprox(glide.EffectiveGlideAccel, 0f)
            && Mathf.IsEqualApprox(hover.EffectiveHoverGravity, 0f)
            && Mathf.IsEqualApprox(hover.EffectiveMaxHoverTime, 0f)
            && Mathf.IsEqualApprox(hover.EffectiveHoverCooldown, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveMaxSpeed, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveAcceleration, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveFriction, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveTurnSpeed, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveBoostMultiplier, 1f)
            && Mathf.IsEqualApprox(fly.EffectiveBoostDuration, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveMaxBankAngle, 0f)
            && Mathf.IsEqualApprox(fly.EffectiveBankSpeed, 0f);

        body.QueueFree();
        dash.QueueFree();
        slide.QueueFree();
        wall.QueueFree();
        glide.QueueFree();
        hover.QueueFree();
        fly.QueueFree();
        return Expect(jumpBounded, "JumpComponent did not bound jump/apex tuning or normalize forced jump direction.")
            && Expect(abilityValuesBounded, "Movement ability components did not bound invalid exported values.")
            && Expect(abilityNonFiniteBounded, "Movement ability components accepted non-finite exported values.");
    }

    private bool VerifyAlgorithmComponentBounds()
    {
        var player = new CharacterBody2D { Name = "AdaptivePlayer" };
        player.AddToGroup("smoke_adaptive_players");
        player.AddChild(new HealthComponent { Name = "Health", MaxHealth = 100f, CurrentHealth = 100f });
        AddChild(player);

        var adaptive = new AdaptiveDifficultyComponent
        {
            Name = "AdaptiveDifficulty",
            PlayerGroup = "smoke_adaptive_players",
            BaseDifficulty = float.NaN,
            AdaptSpeed = float.NaN,
            StruggleHealthThreshold = float.NaN,
            DeathPenalty = float.NaN,
            DeathMemorySeconds = float.NaN
        };
        AddChild(adaptive);
        adaptive._Process(double.NaN);
        bool adaptiveBounded = Mathf.IsEqualApprox(adaptive.EffectiveBaseDifficulty, 0.5f)
            && Mathf.IsEqualApprox(adaptive.EffectiveAdaptSpeed, 0f)
            && Mathf.IsEqualApprox(adaptive.EffectiveStruggleHealthThreshold, 0.35f)
            && Mathf.IsEqualApprox(adaptive.EffectiveDeathPenalty, 0.15f)
            && Mathf.IsEqualApprox(adaptive.EffectiveDeathMemorySeconds, 0f)
            && Mathf.IsEqualApprox(adaptive.GetSpawnIntervalScale(), 1.05f)
            && Mathf.IsEqualApprox(adaptive.GetDropMultiplier(), 1.125f);

        Vector2 nanVector = new(float.NaN, 1f);
        float wanderAngle = float.NaN;
        bool steeringBounded = SteeringBehavior.Seek(nanVector, Vector2.Right, 20f).IsEqualApprox(Vector2.Zero)
            && SteeringBehavior.Flee(Vector2.Zero, nanVector, 20f).IsEqualApprox(Vector2.Zero)
            && SteeringBehavior.Arrive(Vector2.Zero, Vector2.Right, float.NaN, float.NaN).IsEqualApprox(Vector2.Zero)
            && SteeringBehavior.Avoid(Vector2.Zero, Vector2.Right, float.NaN, float.NaN).IsEqualApprox(Vector2.Zero)
            && IsFinite(SteeringBehavior.Wander(nanVector, ref wanderAngle, 20f, float.NaN, float.NaN, float.NaN))
            && float.IsFinite(wanderAngle)
            && SteeringBehavior.Limit(nanVector, 20f).IsEqualApprox(Vector2.Zero);

        var flockBody = new CharacterBody2D { Name = "FlockBody" };
        var flock = new FlockingComponent
        {
            Name = "Flock",
            FlockGroup = "smoke_flock",
            MaxSpeed = float.NaN,
            NeighborRadius = float.NaN,
            SeparationRadius = float.NaN,
            SeparationWeight = float.NaN,
            AlignmentWeight = float.NaN,
            CohesionWeight = float.NaN,
            SteerLerp = float.NaN
        };
        flockBody.AddToGroup("smoke_flock");
        flockBody.AddChild(flock);
        AddChild(flockBody);
        flock._PhysicsProcess(double.NaN);
        bool flockBounded = IsFinite(flockBody.Velocity)
            && Mathf.IsEqualApprox(flock.EffectiveMaxSpeed, 0f)
            && Mathf.IsEqualApprox(flock.EffectiveNeighborRadius, 0f)
            && Mathf.IsEqualApprox(flock.EffectiveSeparationRadius, 0f)
            && Mathf.IsEqualApprox(flock.EffectiveSeparationWeight, 0f)
            && Mathf.IsEqualApprox(flock.EffectiveAlignmentWeight, 0f)
            && Mathf.IsEqualApprox(flock.EffectiveCohesionWeight, 0f)
            && Mathf.IsEqualApprox(flock.EffectiveSteerLerp, 0.15f);

        var ballBody = new CharacterBody2D { Name = "BallBody" };
        ballBody.AddChild(new Sprite2D { Name = "Sprite" });
        var ball = new BallComponent
        {
            Name = "Ball",
            RollFriction = float.NaN,
            Restitution = float.NaN,
            BounceGroundRetention = float.NaN,
            SettleThreshold = float.NaN,
            Gravity = float.NaN,
            ClaimRadius = float.NaN,
            DribbleOffset = float.NaN,
            ReclaimDelay = float.NaN
        };
        ballBody.AddChild(ball);
        AddChild(ballBody);
        ball.Kick(nanVector, float.NaN, float.NaN);
        ball._PhysicsProcess(double.NaN);
        bool ballBounded = IsFinite(ballBody.Velocity)
            && IsFinite(ballBody.GlobalPosition)
            && Mathf.IsEqualApprox(ball.EffectiveRollFriction, 0f)
            && Mathf.IsEqualApprox(ball.EffectiveRestitution, 0.6f)
            && Mathf.IsEqualApprox(ball.EffectiveBounceGroundRetention, 0.8f)
            && Mathf.IsEqualApprox(ball.EffectiveSettleThreshold, 0f)
            && Mathf.IsEqualApprox(ball.EffectiveGravity, 0f)
            && Mathf.IsEqualApprox(ball.EffectiveClaimRadius, 0f)
            && Mathf.IsEqualApprox(ball.EffectiveDribbleOffset, 0f)
            && Mathf.IsEqualApprox(ball.EffectiveReclaimDelay, 0f);

        var heightBody = new Node2D { Name = "HeightBody" };
        heightBody.AddChild(new Sprite2D { Name = "Sprite" });
        var height = new HeightComponent
        {
            Name = "Height",
            Height = float.NaN,
            HalfThickness = float.NaN,
            ZIndexPerPixel = float.NaN,
            ShadowFadeHeight = float.NaN,
            ShadowColor = new Color(float.NaN, 0f, 0f, float.NaN)
        };
        heightBody.AddChild(height);
        AddChild(heightBody);
        height.SetHeight(float.NaN);
        bool heightBounded = Mathf.IsEqualApprox(height.EffectiveHeight, 0f)
            && Mathf.IsEqualApprox(height.EffectiveHalfThickness, 0f)
            && Mathf.IsEqualApprox(height.EffectiveZIndexPerPixel, 0f)
            && height.EffectiveShadowFadeHeight >= 0.0001f
            && !height.IsAirborne
            && height.HeightOverlaps(float.NaN, float.NaN);

        adaptive.QueueFree();
        player.QueueFree();
        flockBody.QueueFree();
        ballBody.QueueFree();
        heightBody.QueueFree();

        return Expect(adaptiveBounded, "AdaptiveDifficultyComponent accepted non-finite difficulty tuning.")
            && Expect(steeringBounded, "SteeringBehavior returned non-finite vectors for invalid math inputs.")
            && Expect(flockBounded, "FlockingComponent accepted non-finite speed/radius/weight tuning.")
            && Expect(ballBounded, "BallComponent accepted non-finite motion/possession tuning.")
            && Expect(heightBounded, "HeightComponent accepted non-finite height, overlap, or shadow tuning.");
    }

    private bool VerifySpawnPickupPlatformBounds()
    {
        var world = new Node2D { Name = "SpawnWorld" };
        var spawnerHost = new Node2D { Name = "SpawnerHost" };
        spawnerHost.GlobalPosition = new Vector2(float.NaN, 4f);
        var spawnRoot = new Node2D { Name = "SpawnedEntity" };
        var scene = new PackedScene();
        scene.Pack(spawnRoot);
        var spawner = new SpawnerComponent
        {
            Name = "Spawner",
            SpawnScene = scene,
            SpawnInterval = float.NaN,
            MaxSpawned = 1,
            SpawnOffset = new Vector2(float.NaN, 6f),
            SpawnRandomRange = new Vector2(float.NaN, -8f),
            SpawnGroup = ""
        };
        spawnerHost.AddChild(spawner);
        world.AddChild(spawnerHost);
        AddChild(world);
        var spawned = spawner.Spawn();
        bool spawnerBounded = spawned is Node2D spawned2D
            && IsFinite(spawned2D.GlobalPosition)
            && Mathf.IsEqualApprox(spawner.EffectiveSpawnInterval, 3f)
            && spawner.EffectiveMaxSpawned == 1
            && spawner.EffectiveSpawnOffset.IsEqualApprox(new Vector2(0f, 6f))
            && spawner.EffectiveSpawnRandomRange.IsEqualApprox(new Vector2(0f, 8f));

        var pickupArea = new Area2D { Name = "PickupArea", Position = new Vector2(float.NaN, 2f), Rotation = float.NaN };
        var pickup = new PickupComponent
        {
            Name = "Pickup",
            Quantity = -5,
            FloatAmplitude = float.NaN,
            FloatSpeed = float.NaN,
            RespawnSeconds = float.NaN,
            ScoreValue = -10
        };
        pickupArea.AddChild(pickup);
        AddChild(pickupArea);
        pickup._Process(double.NaN);
        bool pickupBounded = pickup.EffectiveQuantity == 1
            && Mathf.IsEqualApprox(pickup.EffectiveFloatAmplitude, 0f)
            && Mathf.IsEqualApprox(pickup.EffectiveFloatSpeed, 0f)
            && Mathf.IsEqualApprox(pickup.EffectiveRespawnSeconds, 0f)
            && pickup.EffectiveScoreValue == 0
            && IsFinite(pickupArea.Position)
            && float.IsFinite(pickupArea.Rotation);

        var platformBody = new AnimatableBody2D { Name = "PlatformBody", GlobalPosition = Vector2.Zero };
        var platform = new MovingPlatformComponent
        {
            Name = "Platform",
            Speed = float.NaN,
            PauseDuration = float.NaN,
            AutoStart = true
        };
        platform.AddChild(new Marker2D { Name = "Waypoint", Position = new Vector2(64f, 0f) });
        platformBody.AddChild(platform);
        AddChild(platformBody);
        platform._PhysicsProcess(double.NaN);
        bool platformBounded = IsFinite(platformBody.GlobalPosition)
            && Mathf.IsEqualApprox(platform.EffectiveSpeed, 0f)
            && platform.EffectivePauseDuration >= 0.0;

        world.QueueFree();
        pickupArea.QueueFree();
        platformBody.QueueFree();

        return Expect(spawnerBounded, "SpawnerComponent accepted non-finite spawn timing, offsets, or random range.")
            && Expect(pickupBounded, "PickupComponent accepted non-finite float/respawn values or invalid counts.")
            && Expect(platformBounded, "MovingPlatformComponent accepted non-finite speed, pause, or frame-time values.");
    }

    private bool VerifyRuntimeManagerBounds()
    {
        var turret = new TurretComponent
        {
            FireRate = float.NaN,
            ProjectileDamage = float.NaN,
            ProjectileSpeed = float.NaN,
            Range = float.NaN,
            RotationSpeed = float.NaN
        };
        bool turretBounded = Mathf.IsEqualApprox(turret.EffectiveFireRate, 1f)
            && Mathf.IsEqualApprox(turret.EffectiveProjectileDamage, 0f)
            && Mathf.IsEqualApprox(turret.EffectiveProjectileSpeed, 0f)
            && Mathf.IsEqualApprox(turret.EffectiveRange, 0f)
            && Mathf.IsEqualApprox(turret.EffectiveRotationSpeed, 0f);

        var work = new WorkComponent
        {
            WorkSpeed = float.NaN,
            AvailableWork = float.NaN,
            TotalWorkRequired = float.NaN,
            OutputQuantity = -5
        };
        work.StartWork(float.NaN);
        work.Tick(double.NaN);
        bool workBounded = work.IsWorking
            && Mathf.IsEqualApprox(work.EffectiveAvailableWork, 100f)
            && Mathf.IsEqualApprox(work.EffectiveWorkSpeed, 0f)
            && Mathf.IsEqualApprox(work.EffectiveTotalWorkRequired, 100f)
            && work.EffectiveOutputQuantity == 1
            && work.Progress >= 0f
            && work.Progress <= 1f;

        var save = new GameStateManagerComponent
        {
            MaxSaveSlots = -8,
            AutosaveIntervalSeconds = float.NaN
        };
        bool saveManagerBounded = save.EffectiveMaxSaveSlots == 1
            && Mathf.IsEqualApprox(save.EffectiveAutosaveIntervalSeconds, 300f);

        var app = new GameApp
        {
            DifficultyMultiplier = float.NaN,
            SessionScore = -20,
            SessionPlaytimeSeconds = double.NaN,
            IsGameRunning = true
        };
        app._Process(double.NaN);
        app.AddSessionScore(10);
        bool appBounded = Mathf.IsEqualApprox(app.EffectiveDifficultyMultiplier, 1f)
            && app.SessionScore == 0
            && app.SessionPlaytimeSeconds >= 0.0
            && double.IsFinite(app.SessionPlaytimeSeconds);

        var inventory = new InventoryComponent
        {
            MaxSlots = -20,
            Columns = -3,
            SlotSize = new Vector2I(-5, 0),
            HoverDelay = float.NaN
        };
        inventory.Resize(-4);
        bool inventoryBounded = inventory.EffectiveMaxSlots == 1
            && inventory.EffectiveColumns == 1
            && inventory.EffectiveSlotSize == new Vector2I(1, 1)
            && Mathf.IsEqualApprox(inventory.EffectiveHoverDelay, 0f)
            && inventory.EffectiveSlotCount == 1
            && inventory.Slots.Length == 1
            && inventory.FreeSlots == 1
            && !inventory.IsFull;

        var boot = new BootComponent { MinBootTime = double.NaN };
        bool bootBounded = Mathf.IsEqualApprox((float)boot.EffectiveMinBootTime, 0f);

        var followParent = new Node2D { Name = "FollowParent", GlobalPosition = Vector2.Zero };
        var followTarget = new Node2D { Name = "FollowTarget", GlobalPosition = new Vector2(100, 0) };
        var follow = new FollowTargetComponent
        {
            Name = "Follow",
            FollowSpeed = 99f,
            MaxDistance = float.NaN,
            Offset = new Vector2(0f, 5f),
            LookAtTarget = true
        };
        followParent.AddChild(follow);
        AddChild(followParent);
        AddChild(followTarget);
        follow.SetTarget(followTarget);
        follow._Process(1.0);
        bool followClamped = followParent.GlobalPosition.IsEqualApprox(new Vector2(100, 5))
            && Mathf.IsEqualApprox(follow.EffectiveMaxDistance, 0f)
            && Mathf.IsEqualApprox(follow.EffectiveFollowSpeed, 99f);
        follow.FollowSpeed = float.NaN;
        follow.Offset = new Vector2(float.NaN, 5f);
        followTarget.GlobalPosition = new Vector2(200, 0);
        follow._Process(double.NaN);
        bool followInvalidIgnored = followParent.GlobalPosition.IsEqualApprox(new Vector2(100, 5))
            && Mathf.IsEqualApprox(follow.EffectiveFollowSpeed, 0f);

        var shakeCamera = new Camera2D { Name = "ShakeCamera" };
        var shake = new ScreenShakeComponent
        {
            Name = "Shake",
            DefaultIntensity = float.NaN,
            DefaultDuration = float.NaN,
            MaxTrauma = float.NaN
        };
        shakeCamera.AddChild(shake);
        AddChild(shakeCamera);
        bool shakeBounds = Mathf.IsEqualApprox(shake.EffectiveDefaultIntensity, 0f)
            && Mathf.IsEqualApprox(shake.EffectiveDefaultDuration, 0.01f)
            && Mathf.IsEqualApprox(shake.EffectiveMaxTrauma, 100f);
        shake.DefaultIntensity = 40f;
        shake.Shake(float.NaN, float.NaN);
        shake._Process(double.NaN);
        shake._Process(0.02);
        bool shakeFinite = float.IsFinite(shakeCamera.Offset.X)
            && float.IsFinite(shakeCamera.Offset.Y);

        var particle = new ParticleComponent { Offset = new Vector2(float.NaN, 8f) };
        bool particleBounded = particle.EffectiveOffset == Vector2.Zero;

        var trail = new TrailComponent
        {
            MaxPoints = -10,
            Width = float.NaN,
            TrailColor = new Color(float.NaN, 1f, 1f, 1f)
        };
        bool trailBounded = trail.EffectiveMaxPoints == 1
            && Mathf.IsEqualApprox(trail.EffectiveWidth, 1f)
            && trail.EffectiveTrailColor.IsEqualApprox(new Color(1, 1, 1, 0.5f));

        var respawn = new RespawnComponent { RespawnDelay = float.NaN };
        var despawn = new DespawnOnDeathComponent { DespawnDelay = float.PositiveInfinity };
        var flow = new GameFlowComponent { NavigateDelay = float.NegativeInfinity };
        var drops = new DropTableComponent
        {
            MinDrops = -5,
            MaxDrops = -1,
            DropChance = float.NaN,
            DifficultyWeightMultiplier = float.NaN,
            ScatterRadius = float.NaN,
            DropLifetimeSeconds = float.PositiveInfinity,
            MaxPlacementAttempts = -10,
            MinimumSpacing = float.NaN
        };
        bool lifecycleTimersBounded = Mathf.IsEqualApprox(respawn.EffectiveRespawnDelay, 0f)
            && Mathf.IsEqualApprox(despawn.EffectiveDespawnDelay, 0f)
            && Mathf.IsEqualApprox(flow.EffectiveNavigateDelay, 0f)
            && drops.EffectiveMinDrops == 0
            && drops.EffectiveMaxDrops == 0
            && Mathf.IsEqualApprox(drops.EffectiveDropChance, 0f)
            && Mathf.IsEqualApprox(drops.EffectiveDifficultyWeightMultiplier, 1f)
            && Mathf.IsEqualApprox(drops.EffectiveScatterRadius, 0f)
            && Mathf.IsEqualApprox(drops.EffectiveDropLifetimeSeconds, 0f)
            && drops.EffectiveMaxPlacementAttempts == 1
            && Mathf.IsEqualApprox(drops.EffectiveMinimumSpacing, 0f);

        var stackItem = new GameItem
        {
            Id = "stack",
            DisplayName = "Stack",
            MaxStack = -10,
            MaxDurability = float.NaN
        };
        inventory.Resize(3);
        bool inventoryItemBounded = stackItem.EffectiveMaxStack == 1
            && Mathf.IsEqualApprox(stackItem.EffectiveMaxDurability, 0f)
            && !inventory.AddItem(stackItem, 0)
            && inventory.AddItem(stackItem, 2)
            && inventory.UsedSlots == 2
            && inventory.CountItem("stack") == 2
            && !inventory.CanFit(stackItem, 0)
            && !inventory.HasItem("stack", 0)
            && !inventory.SplitStack(0, 0)
            && !inventory.RemoveItem("stack", 0)
            && !inventory.RemoveAt(0, 0);

        var outputItem = new GameItem { Id = "output", DisplayName = "Output", MaxStack = 2 };
        var crafting = new CraftingComponent();
        var badRecipe = new CraftingRecipe
        {
            OutputItem = outputItem,
            OutputCount = -4,
            InputItems = new[] { new CraftingIngredient { Item = stackItem, Count = -3 } }
        };
        var goodRecipe = new CraftingRecipe
        {
            OutputItem = outputItem,
            OutputCount = 1,
            InputItems = new[] { new CraftingIngredient { Item = stackItem, Count = -3 } }
        };
        bool badCraftRejected = !crafting.CanCraft(badRecipe, inventory)
            && !crafting.Craft(badRecipe, inventory)
            && inventory.CountItem("stack") == 2
            && inventory.CountItem("output") == 0
            && badRecipe.EffectiveOutputCount == 0
            && Mathf.IsEqualApprox(badRecipe.EffectiveCraftTime, 0f)
            && badRecipe.InputItems[0].EffectiveCount == 1;
        bool goodCraftPrepared = crafting.CanCraft(goodRecipe, inventory);
        bool goodCraftSucceeded = crafting.Craft(goodRecipe, inventory);
        bool goodCraftInventory = inventory.CountItem("stack") == 1
            && inventory.CountItem("output") == 1;

        var objectPool = new ObjectPoolComponent
        {
            PreloadCount = -5,
            MaxSize = -1
        };
        bool objectPoolBounded = objectPool.EffectivePreloadCount == 0
            && objectPool.EffectiveMaxSize == 0
            && objectPool.Get() == null;

        turret.QueueFree();
        work.QueueFree();
        save.QueueFree();
        app.QueueFree();
        inventory.QueueFree();
        boot.QueueFree();
        followParent.QueueFree();
        followTarget.QueueFree();
        shakeCamera.QueueFree();
        particle.QueueFree();
        trail.QueueFree();
        respawn.QueueFree();
        despawn.QueueFree();
        flow.QueueFree();
        drops.QueueFree();
        crafting.QueueFree();
        objectPool.QueueFree();

        return Expect(turretBounded, "TurretComponent accepted non-finite fire/damage/range/rotation tuning.")
            && Expect(workBounded, "WorkComponent accepted non-finite work timing/progress values.")
            && Expect(saveManagerBounded, "GameStateManagerComponent accepted invalid save slot/autosave tuning.")
            && Expect(appBounded, "GameApp accepted non-finite session timing or score multiplier values.")
            && Expect(bootBounded, "BootComponent accepted non-finite minimum boot time.")
            && Expect(followClamped, "FollowTargetComponent did not clamp interpolation or sanitize offset/max distance.")
            && Expect(followInvalidIgnored, "FollowTargetComponent accepted non-finite follow speed or frame time.")
            && Expect(shakeBounds, "ScreenShakeComponent did not bound invalid default intensity/duration/trauma.")
            && Expect(shakeFinite, "ScreenShakeComponent let invalid timing create a non-finite camera offset.")
            && Expect(inventoryBounded, "InventoryComponent accepted invalid capacity, columns, slot size, or hover delay.")
            && Expect(inventoryItemBounded, "InventoryComponent accepted invalid item stack, durability, or quantity values.")
            && Expect(badCraftRejected, "CraftingComponent accepted an invalid recipe or consumed inputs on failed craft.")
            && Expect(goodCraftPrepared, "CraftingComponent rejected a craftable recipe with effective ingredient counts.")
            && Expect(goodCraftSucceeded, "CraftingComponent failed to craft a valid recipe with output capacity.")
            && Expect(goodCraftInventory, "CraftingComponent did not apply effective input/output counts to inventory.")
            && Expect(objectPoolBounded, "ObjectPoolComponent accepted invalid preload or max-size values.")
            && Expect(particleBounded, "ParticleComponent accepted non-finite follow offset.")
            && Expect(trailBounded, "TrailComponent accepted invalid point count, width, or color.")
            && Expect(lifecycleTimersBounded, "Lifecycle/drop components accepted invalid timer, chance, placement, or drop-count values.");
    }

    private bool VerifyLevelLoaderLooseLevelEntries()
    {
        var root = new Node { Name = "LevelLoaderLooseEntriesSmokeRoot" };
        AddChild(root);

        var container = new Node { Name = "LevelContainer" };
        root.AddChild(container);

        var levelRoot = new Node2D { Name = "PackedLevel" };
        levelRoot.AddChild(new Marker2D { Name = "PlayerSpawn", GlobalPosition = new Vector2(24f, 32f) });
        var packed = new PackedScene();
        Error packedResult = packed.Pack(levelRoot);

        var player = new Node2D { Name = "Player" };
        root.AddChild(player);

        var loader = new LevelLoaderComponent
        {
            Name = "Loader",
            LevelContainerPath = new NodePath("../LevelContainer"),
            PlayerPath = new NodePath("../Player"),
            FirstLevelIndex = 1
        };
        loader.Levels.Add(packed);
        loader.Levels.Add(new Resource());
        root.AddChild(loader);

        int loadedLevel = -1;
        int failedLevel = -1;
        string failedReason = "";
        loader.LevelLoaded += level => loadedLevel = level;
        loader.LevelLoadFailed += (level, reason) =>
        {
            failedLevel = level;
            failedReason = reason;
        };

        loader.LoadLevel(1);
        bool validLoaded = packedResult == Error.Ok
            && loadedLevel == 1
            && loader.CurrentLevel == 1
            && container.GetChildCount() == 1
            && container.GetChild(0).Name == "PackedLevel";

        loader.LoadLevel(2);
        bool invalidRejected = failedLevel == 2
            && failedReason == "invalid level scene"
            && loader.CurrentLevel == 1
            && container.GetChildCount() == 1;

        root.QueueFree();
        levelRoot.QueueFree();

        return Expect(validLoaded, "LevelLoaderComponent did not load a valid loose-array PackedScene entry.")
            && Expect(invalidRejected, "LevelLoaderComponent did not reject an invalid loose-array level entry without disrupting the current level.");
    }

    private bool VerifySurvivalWeatherAndFeedbackComponents()
    {
        return VerifyHungerStamina()
            && VerifySurvivalVitals()
            && VerifyTemperature()
            && VerifyAtmosphereComponentBounds()
            && VerifyWindAudioAndFeedbackValues()
            && VerifyUiVisualEffectBounds()
            && VerifyCropGrowthBounds();
    }

    private bool VerifyGenreStateComponentBounds()
    {
        var race = new RaceStateComponent
        {
            TotalLaps = -3,
            RivalCount = 1000,
            MaxSpeed = float.NaN
        };
        AddChild(race);
        race.Speed = float.NaN;
        race.LapProgress = float.NaN;
        race._Process(double.NaN);
        bool raceAuthoredBounded = race.EffectiveTotalLaps == 1
            && race.EffectiveRivalCount == 64
            && Mathf.IsEqualApprox(race.EffectiveMaxSpeed, 0f)
            && Mathf.IsEqualApprox(race.TotalTime, 0f)
            && Mathf.IsEqualApprox(race.LapProgress, 0f)
            && Mathf.IsEqualApprox(race.SpeedFraction, 0f);

        var state = new GameStateData();
        state.GameData["racing.lap"] = -10;
        state.GameData["racing.total_time"] = double.NaN;
        state.GameData["racing.best_lap"] = double.NaN;
        race.Load(state);
        bool raceLoadBounded = race.Lap == 1
            && Mathf.IsEqualApprox(race.TotalTime, 0f)
            && Mathf.IsEqualApprox(race.BestLap, -1f);

        var raceMalformed = new GameStateData();
        raceMalformed.GameData["racing.lap"] = new Resource();
        raceMalformed.GameData["racing.total_time"] = "12.5";
        raceMalformed.GameData["racing.best_lap"] = "bad";
        raceMalformed.GameData["racing.finished"] = new Resource();
        race.Load(raceMalformed);
        bool raceMalformedLoadBounded = race.Lap == 1
            && Mathf.IsEqualApprox(race.TotalTime, 12.5f)
            && Mathf.IsEqualApprox(race.BestLap, -1f)
            && !race.Finished;

        var shooter = new ShooterCombatComponent
        {
            MagazineSize = -30,
            MaxReserve = -1,
            ReloadSeconds = float.NaN,
            BaseEnemiesPerWave = -5,
            EnemiesAddedPerWave = -7,
            WaveResupplyMagazines = float.NaN,
            LowThreshold = float.NaN
        };
        AddChild(shooter);
        bool shooterAuthoredBounded = shooter.EffectiveMagazineSize == 1
            && shooter.EffectiveMaxReserve == 0
            && Mathf.IsEqualApprox(shooter.EffectiveReloadSeconds, 0f)
            && shooter.EffectiveBaseEnemiesPerWave == 1
            && shooter.EffectiveEnemiesAddedPerWave == 0
            && Mathf.IsEqualApprox(shooter.EffectiveWaveResupplyMagazines, 0f)
            && shooter.Magazine == 1
            && shooter.Reserve == 0
            && shooter.EnemiesInWave == 1;

        shooter._Process(double.NaN);
        bool shooterProcessBounded = Mathf.IsEqualApprox(shooter.ReloadProgress, 0f)
            && shooter.MagazineFraction >= 0f
            && shooter.MagazineFraction <= 1f;

        state.GameData["shooter.wave"] = -10;
        state.GameData["shooter.magazine"] = 999;
        state.GameData["shooter.reserve"] = 999;
        state.GameData["shooter.enemies_left"] = 999;
        shooter.Load(state);
        bool shooterLoadBounded = shooter.Wave == 1
            && shooter.Magazine == 1
            && shooter.Reserve == 0
            && shooter.EnemiesRemaining == 1;

        var shooterMalformed = new GameStateData();
        shooterMalformed.GameData["shooter.wave"] = "2";
        shooterMalformed.GameData["shooter.magazine"] = new Resource();
        shooterMalformed.GameData["shooter.reserve"] = double.NaN;
        shooterMalformed.GameData["shooter.enemies_left"] = "bad";
        shooter.Load(shooterMalformed);
        bool shooterMalformedLoadBounded = shooter.Wave == 2
            && shooter.Magazine == 1
            && shooter.Reserve == 0
            && shooter.EnemiesRemaining == 1;

        var fsm = new StateMachineComponent { InitialState = "idle" };
        AddChild(fsm);
        fsm.AddState("idle");
        fsm.Start("idle");
        fsm._Process(double.NaN);
        bool stateTimerBounded = Mathf.IsEqualApprox(fsm.CurrentStateTime, 0f);

        state.GameData["state_machine_current"] = "idle";
        state.GameData["state_machine_time"] = double.NaN;
        fsm.Load(state);
        bool stateLoadBounded = Mathf.IsEqualApprox(fsm.CurrentStateTime, 0f);

        var malformedFsmState = new GameStateData();
        malformedFsmState.GameData["state_machine_current"] = "idle";
        malformedFsmState.GameData["state_machine_time"] = new Resource();
        fsm.Load(malformedFsmState);
        bool stateMalformedLoadBounded = Mathf.IsEqualApprox(fsm.CurrentStateTime, 0f);

        malformedFsmState.GameData["state_machine_time"] = "1.25";
        fsm.Load(malformedFsmState);
        stateMalformedLoadBounded = stateMalformedLoadBounded
            && Mathf.IsEqualApprox(fsm.CurrentStateTime, 1.25f);

        var rpg = new RpgPartyComponent
        {
            BaseMaxHealth = -10,
            BaseMaxMana = -20,
            HealthPerLevel = -3,
            ManaPerLevel = -4,
            BaseXpToLevel = -5,
            XpCurve = float.NaN,
            ManaRegenPerSecond = float.NaN,
            LowThreshold = float.NaN
        };
        AddChild(rpg);
        bool rpgAuthoredBounded = rpg.MaxHealth == 1
            && rpg.MaxMana == 1
            && rpg.XpToNextLevel == 1
            && Mathf.IsEqualApprox(rpg.EffectiveManaRegenPerSecond, 0f)
            && Mathf.IsEqualApprox(rpg.EffectiveLowThreshold, 0.3f)
            && rpg.HealthFraction >= 0f
            && rpg.HealthFraction <= 1f
            && rpg.ManaFraction >= 0f
            && rpg.ManaFraction <= 1f;

        rpg.SpendMana(1);
        rpg._Process(double.NaN);
        bool rpgInvalidRegenIgnored = rpg.Mana == 0;

        state.GameData["rpg.level"] = -4;
        state.GameData["rpg.xp"] = -100;
        state.GameData["rpg.health"] = double.NaN;
        state.GameData["rpg.mana"] = double.PositiveInfinity;
        rpg.Load(state);
        bool rpgLoadBounded = rpg.Level == 1
            && rpg.Xp == 0
            && rpg.Health >= 0
            && rpg.Health <= rpg.MaxHealth
            && rpg.Mana >= 0
            && rpg.Mana <= rpg.MaxMana;

        var rpgQuestState = new GameStateData();
        rpgQuestState.GameData["rpg.quests"] = new Godot.Collections.Dictionary
        {
            ["find_wrench"] = new Godot.Collections.Array { "Find Wrench", 2, 5 },
            ["bad_row"] = "bad"
        };
        rpgQuestState.GameData["rpg.quest_active"] = "find_wrench";
        rpg.Load(rpgQuestState);
        bool rpgQuestLoadBounded = rpg.ActiveQuest != null
            && rpg.ActiveQuest.Id == "find_wrench"
            && rpg.ActiveQuest.Progress == 2
            && rpg.ActiveQuest.Goal == 5
            && !rpg.IsQuestComplete("bad_row");

        rpgQuestState.GameData["rpg.quests"] = "bad";
        rpg.Load(rpgQuestState);
        rpgQuestLoadBounded = rpgQuestLoadBounded && rpg.ActiveQuest == null;

        var leveling = new LevelingComponent
        {
            Level = -3,
            MaxLevel = -1,
            BaseXp = float.NaN,
            XpGrowthMultiplier = float.PositiveInfinity,
            StatPointsPerLevel = -8
        };
        AddChild(leveling);
        leveling.AddXp(float.NaN);
        bool levelingBounded = leveling.EffectiveMaxLevel == 1
            && leveling.EffectiveLevel == 1
            && Mathf.IsEqualApprox(leveling.EffectiveBaseXp, 100f)
            && Mathf.IsEqualApprox(leveling.EffectiveXpGrowthMultiplier, 1f)
            && leveling.EffectiveStatPointsPerLevel == 0
            && Mathf.IsEqualApprox(leveling.CurrentXp, 0f);

        var cards = new CardDeckComponent
        {
            MaxHealth = -10,
            StartingGold = -20,
            EnergyPerTurn = -3,
            HandSize = -4,
            StartingDeck = new[] { "strike", " ", "", "defend" }
        };
        AddChild(cards);
        bool cardsBounded = cards.EffectiveMaxHealth == 1
            && cards.EffectiveStartingGold == 0
            && cards.EffectiveEnergyPerTurn == 0
            && cards.EffectiveHandSize == 0
            && cards.Health == 1
            && cards.Gold == 0
            && cards.Energy == 0
            && cards.TotalCards == 2
            && !cards.PlayCard("", 1)
            && !cards.PlayCard("strike", 0);

        var cardState = new GameStateData();
        cardState.GameData["cardgame.health"] = double.NaN;
        cardState.GameData["cardgame.gold"] = "-9";
        cardState.GameData["cardgame.energy"] = "4";
        cardState.GameData["cardgame.turn"] = -7;
        cardState.GameData["cardgame.deck"] = new Godot.Collections.Dictionary();
        cardState.GameData["cardgame.hand"] = new Godot.Collections.Array { "strike", " ", "" };
        cardState.GameData["cardgame.discard"] = new Resource();
        cards.Load(cardState);
        bool cardsLoadBounded = cards.Health == 1
            && cards.Gold == 0
            && cards.Energy == 0
            && cards.Turn == 1
            && cards.DeckCount == 0
            && cards.HandCount == 1
            && cards.DiscardCount == 0;

        var strategy = new StrategyEmpireComponent
        {
            StartingGold = -20,
            StartingFood = -10,
            StartingWood = -5,
            GoldPerTurn = -1,
            FoodPerTurn = -2,
            WoodPerTurn = -3,
            GoldUpkeepPerUnit = -4,
            FoodPerUnit = -5,
            StarvationLossPerTurn = -6
        };
        AddChild(strategy);
        bool strategyBounded = strategy.EffectiveStartingGold == 0
            && strategy.EffectiveStartingFood == 0
            && strategy.EffectiveStartingWood == 0
            && strategy.EffectiveGoldPerTurn == 0
            && strategy.EffectiveFoodPerTurn == 0
            && strategy.EffectiveWoodPerTurn == 0
            && strategy.EffectiveGoldUpkeepPerUnit == 0
            && strategy.EffectiveFoodPerUnit == 0
            && strategy.EffectiveStarvationLossPerTurn == 0
            && strategy.CanAfford(-5, -5, -5)
            && strategy.Spend(-5, -5, -5)
            && strategy.Gold == 0
            && strategy.Food == 0
            && strategy.Wood == 0;

        var strategyMalformed = new GameStateData();
        strategyMalformed.GameData["strategy.turn"] = "3";
        strategyMalformed.GameData["strategy.gold"] = new Resource();
        strategyMalformed.GameData["strategy.food"] = "12";
        strategyMalformed.GameData["strategy.wood"] = double.NaN;
        strategyMalformed.GameData["strategy.units"] = "bad";
        strategy.Load(strategyMalformed);
        bool strategyMalformedLoadBounded = strategy.Turn == 3
            && strategy.Gold == 0
            && strategy.Food == 12
            && strategy.Wood == 0
            && strategy.Units == 0;

        var puzzle = new PuzzleLevelComponent
        {
            TargetScore = -100,
            MoveBudget = -20,
            TwoStarMultiple = float.NaN,
            ThreeStarMultiple = 0.5f,
            LowMovesThreshold = -2
        };
        AddChild(puzzle);
        bool puzzleBounded = puzzle.EffectiveTargetScore == 1
            && puzzle.EffectiveMoveBudget == 0
            && Mathf.IsEqualApprox(puzzle.EffectiveTwoStarMultiple, 1.5f)
            && Mathf.IsEqualApprox(puzzle.EffectiveThreeStarMultiple, 1.5f)
            && puzzle.EffectiveLowMovesThreshold == 0
            && puzzle.MovesLeft == 0
            && Mathf.IsEqualApprox(puzzle.TargetFraction, 0f)
            && Mathf.IsEqualApprox(puzzle.MovesFraction, 0f);

        var puzzleMalformed = new GameStateData();
        puzzleMalformed.GameData["puzzle.target"] = "50";
        puzzleMalformed.GameData["puzzle.budget"] = "7";
        puzzleMalformed.GameData["puzzle.score"] = new Resource();
        puzzleMalformed.GameData["puzzle.moves_left"] = double.NaN;
        puzzleMalformed.GameData["puzzle.won"] = new Resource();
        puzzleMalformed.GameData["puzzle.lost"] = "true";
        puzzle.Load(puzzleMalformed);
        bool puzzleMalformedLoadBounded = puzzle.TargetScore == 50
            && puzzle.MoveBudget == 7
            && puzzle.Score == 0
            && puzzle.MovesLeft == 7
            && !puzzle.Won
            && puzzle.Lost;

        race.QueueFree();
        shooter.QueueFree();
        fsm.QueueFree();
        rpg.QueueFree();
        leveling.QueueFree();
        cards.QueueFree();
        strategy.QueueFree();
        puzzle.QueueFree();

        return Expect(raceAuthoredBounded, "RaceStateComponent did not bound authored race tuning or invalid frame time.")
            && Expect(raceLoadBounded, "RaceStateComponent did not normalize invalid saved lap timing.")
            && Expect(raceMalformedLoadBounded, "RaceStateComponent did not ignore malformed saved race values.")
            && Expect(shooterAuthoredBounded, "ShooterCombatComponent did not bound authored ammo/wave tuning.")
            && Expect(shooterProcessBounded, "ShooterCombatComponent accepted invalid reload frame time.")
            && Expect(shooterLoadBounded, "ShooterCombatComponent did not normalize invalid saved ammo/wave state.")
            && Expect(shooterMalformedLoadBounded, "ShooterCombatComponent did not ignore malformed saved ammo/wave values.")
            && Expect(stateTimerBounded, "StateMachineComponent accepted invalid frame time.")
            && Expect(stateLoadBounded, "StateMachineComponent did not normalize invalid saved state time.")
            && Expect(stateMalformedLoadBounded, "StateMachineComponent did not ignore malformed saved state time values.")
            && Expect(rpgAuthoredBounded, "RpgPartyComponent did not bound invalid authored stat tuning.")
            && Expect(rpgInvalidRegenIgnored, "RpgPartyComponent accepted invalid mana regeneration frame time.")
            && Expect(rpgLoadBounded, "RpgPartyComponent did not normalize invalid saved stats.")
            && Expect(rpgQuestLoadBounded, "RpgPartyComponent did not ignore malformed saved quest data.")
            && Expect(levelingBounded, "LevelingComponent accepted invalid authored XP or level values.")
            && Expect(cardsBounded, "CardDeckComponent accepted invalid deck health, energy, hand, or card play values.")
            && Expect(cardsLoadBounded, "CardDeckComponent did not normalize malformed saved card piles and counters.")
            && Expect(strategyBounded, "StrategyEmpireComponent accepted invalid resource or upkeep values.")
            && Expect(strategyMalformedLoadBounded, "StrategyEmpireComponent did not ignore malformed saved resource values.")
            && Expect(puzzleBounded, "PuzzleLevelComponent accepted invalid target, move, or star tuning.")
            && Expect(puzzleMalformedLoadBounded, "PuzzleLevelComponent did not ignore malformed saved puzzle values.")
            && VerifyCityEconomyBounds();
    }

    private bool VerifyCityEconomyBounds()
    {
        var city = new CityEconomyComponent
        {
            Name = "CityEconomy",
            StartingTreasury = -100,
            SecondsPerMonth = float.NaN,
            TaxPerResident = float.NaN
        };
        AddChild(city);
        city._Process(double.NaN);

        bool authoredBounded = city.EffectiveStartingTreasury == 0
            && Mathf.IsEqualApprox(city.EffectiveSecondsPerMonth, 6f)
            && Mathf.IsEqualApprox(city.EffectiveTaxPerResident, 0f)
            && city.Treasury >= 0
            && city.Month == 0;

        var state = new GameStateData();
        state.GameData["citybuilder.treasury"] = -100;
        state.GameData["citybuilder.population"] = -20;
        state.GameData["citybuilder.happiness"] = 200;
        state.GameData["citybuilder.day"] = -12;
        state.GameData["citybuilder.speed"] = 99;
        var buildings = new Godot.Collections.Dictionary
        {
            ["house"] = -3,
            ["unknown"] = 7
        };
        state.GameData["citybuilder.buildings"] = buildings;
        city.Load(state);
        city.Save(state);

        var savedBuildings = state.GameData["citybuilder.buildings"].AsGodotDictionary();
        bool loadSaveBounded = city.Treasury == 0
            && city.Population == 0
            && city.Happiness >= 0
            && city.Happiness <= 100
            && city.Month == 0
            && city.Speed == 3
            && city.CountOf("house") == 0
            && !savedBuildings.ContainsKey("unknown")
            && !savedBuildings.ContainsKey("house");

        var malformed = new GameStateData();
        malformed.GameData["citybuilder.treasury"] = "123";
        malformed.GameData["citybuilder.population"] = double.NaN;
        malformed.GameData["citybuilder.happiness"] = "bad";
        malformed.GameData["citybuilder.day"] = double.PositiveInfinity;
        malformed.GameData["citybuilder.speed"] = "2";
        malformed.GameData["citybuilder.buildings"] = "bad";
        city.Load(malformed);
        city.Save(malformed);

        var malformedBuildings = malformed.GameData["citybuilder.buildings"].AsGodotDictionary();
        bool malformedLoadBounded = city.Treasury == 123
            && city.Population == 0
            && city.Happiness == 70
            && city.Month == 0
            && city.Speed == 2
            && malformedBuildings.Count == 0;

        city.QueueFree();

        return Expect(authoredBounded, "CityEconomyComponent accepted non-finite or negative authored economy tuning.")
            && Expect(loadSaveBounded, "CityEconomyComponent did not normalize invalid saved economy state.")
            && Expect(malformedLoadBounded, "CityEconomyComponent did not ignore malformed saved building data.");
    }

    private bool VerifyHungerStamina()
    {
        var body = new CharacterBody2D { Name = "HungerBody" };
        var hunger = new HungerStaminaComponent
        {
            Name = "HungerStamina",
            CurrentHunger = 50f,
            CurrentThirst = 50f,
            CurrentStamina = 50f,
            HungerDepletePerSecond = -5f,
            ThirstDepletePerSecond = -5f,
            StaminaDepleteWhenMoving = -5f,
            HungerRecoverPerSecond = -5f,
            ThirstRecoverPerSecond = -5f,
            StaminaRecoverPerSecond = -5f,
            MovementThreshold = -10f,
            HungerCriticalLevel = 500f,
            ThirstCriticalLevel = -50f,
            StaminaCriticalLevel = -50f,
            ColdHungerMultiplier = -2f,
            OverheatThirstMultiplier = -2f
        };
        body.AddChild(hunger);
        AddChild(body);

        hunger._Process(1.0);
        bool invalidRatesBounded = Mathf.IsEqualApprox(hunger.CurrentHunger, 50f)
            && Mathf.IsEqualApprox(hunger.CurrentThirst, 50f)
            && Mathf.IsEqualApprox(hunger.CurrentStamina, 50f)
            && Mathf.IsEqualApprox(hunger.EffectiveHungerDepletePerSecond, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveMovementThreshold, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveHungerCriticalLevel, 100f)
            && Mathf.IsEqualApprox(hunger.EffectiveThirstCriticalLevel, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveStaminaCriticalLevel, 0f);

        hunger.ConsumeFood(-10f);
        hunger.DrinkWater(-10f);
        hunger.Rest(-10f);
        bool negativeRestoresIgnored = Mathf.IsEqualApprox(hunger.CurrentHunger, 50f)
            && Mathf.IsEqualApprox(hunger.CurrentThirst, 50f)
            && Mathf.IsEqualApprox(hunger.CurrentStamina, 50f);

        hunger.CurrentHunger = float.NaN;
        hunger.CurrentThirst = float.NaN;
        hunger.CurrentStamina = float.NaN;
        hunger.HungerDepletePerSecond = float.NaN;
        hunger.ThirstDepletePerSecond = float.NaN;
        hunger.StaminaDepleteWhenMoving = float.NaN;
        hunger.HungerRecoverPerSecond = float.NaN;
        hunger.ThirstRecoverPerSecond = float.NaN;
        hunger.StaminaRecoverPerSecond = float.NaN;
        hunger.MovementThreshold = float.NaN;
        hunger.HungerCriticalLevel = float.NaN;
        hunger.ThirstCriticalLevel = float.NaN;
        hunger.StaminaCriticalLevel = float.NaN;
        hunger.ColdHungerMultiplier = float.NaN;
        hunger.OverheatThirstMultiplier = float.NaN;
        body.Velocity = new Vector2(float.NaN, 0f);
        hunger._Process(double.NaN);
        bool nonFiniteValuesBounded = Mathf.IsEqualApprox(hunger.CurrentHunger, 100f)
            && Mathf.IsEqualApprox(hunger.CurrentThirst, 100f)
            && Mathf.IsEqualApprox(hunger.CurrentStamina, 100f)
            && Mathf.IsEqualApprox(hunger.EffectiveHungerDepletePerSecond, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveThirstDepletePerSecond, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveStaminaDepleteWhenMoving, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveHungerRecoverPerSecond, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveThirstRecoverPerSecond, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveStaminaRecoverPerSecond, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveMovementThreshold, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveHungerCriticalLevel, 20f)
            && Mathf.IsEqualApprox(hunger.EffectiveThirstCriticalLevel, 15f)
            && Mathf.IsEqualApprox(hunger.EffectiveStaminaCriticalLevel, 10f)
            && Mathf.IsEqualApprox(hunger.EffectiveColdHungerMultiplier, 0f)
            && Mathf.IsEqualApprox(hunger.EffectiveOverheatThirstMultiplier, 0f);

        hunger.ConsumeFood(float.NaN);
        hunger.DrinkWater(float.NaN);
        hunger.Rest(float.NaN);
        bool nonFiniteRestoresIgnored = Mathf.IsEqualApprox(hunger.CurrentHunger, 100f)
            && Mathf.IsEqualApprox(hunger.CurrentThirst, 100f)
            && Mathf.IsEqualApprox(hunger.CurrentStamina, 100f)
            && !hunger.TryConsumeStamina(float.NaN);

        hunger.CurrentStamina = 5f;
        hunger.StaminaCriticalLevel = 10f;
        bool exhaustedBlocksSpend = !hunger.TryConsumeStamina(1f);

        body.QueueFree();
        return Expect(invalidRatesBounded, "HungerStaminaComponent did not bound invalid exported rates/thresholds.")
            && Expect(negativeRestoresIgnored, "HungerStaminaComponent accepted negative restore inputs.")
            && Expect(nonFiniteValuesBounded, "HungerStaminaComponent accepted non-finite state/rate/threshold values.")
            && Expect(nonFiniteRestoresIgnored, "HungerStaminaComponent accepted non-finite restore/spend inputs.")
            && Expect(exhaustedBlocksSpend, "HungerStaminaComponent allowed stamina spending while exhausted.");
    }

    private bool VerifySurvivalVitals()
    {
        var vitals = new SurvivalVitalsComponent
        {
            Name = "SurvivalVitals",
            MaxHealth = -10f,
            MaxHunger = -10f,
            MaxThirst = -10f,
            MaxStamina = -10f,
            SecondsToStarve = -100f,
            ThirstRateMultiplier = -2f,
            StarvationDamagePerSecond = -3f,
            RegenPerSecond = -4f,
            StaminaDrainPerSecond = -5f,
            StaminaRecoverPerSecond = -6f,
            LowThreshold = -1f
        };
        AddChild(vitals);

        bool readyNormalized = Mathf.IsEqualApprox(vitals.Health, 1f)
            && Mathf.IsEqualApprox(vitals.Hunger, 1f)
            && Mathf.IsEqualApprox(vitals.Thirst, 1f)
            && Mathf.IsEqualApprox(vitals.Stamina, 1f)
            && Mathf.IsEqualApprox(vitals.HealthFraction, 1f)
            && Mathf.IsEqualApprox(vitals.EffectiveLowThreshold, 0.01f);

        vitals._Process(1.0);
        bool invalidRatesDidNotReverse = Mathf.IsEqualApprox(vitals.Health, 1f)
            && Mathf.IsEqualApprox(vitals.Hunger, 1f)
            && Mathf.IsEqualApprox(vitals.Thirst, 1f)
            && Mathf.IsEqualApprox(vitals.Stamina, 1f);

        vitals.Eat(-10f);
        vitals.Drink(-10f);
        vitals.Heal(-10f);
        bool negativeInputsIgnored = Mathf.IsEqualApprox(vitals.Health, 1f)
            && Mathf.IsEqualApprox(vitals.Hunger, 1f)
            && Mathf.IsEqualApprox(vitals.Thirst, 1f);

        var state = new GameStateData();
        state.GameData["survival.health"] = 500f;
        state.GameData["survival.hunger"] = 500f;
        state.GameData["survival.thirst"] = 500f;
        state.GameData["survival.stamina"] = 500f;
        vitals.Load(state);
        bool loadClamped = Mathf.IsEqualApprox(vitals.Health, 1f)
            && Mathf.IsEqualApprox(vitals.Hunger, 1f)
            && Mathf.IsEqualApprox(vitals.Thirst, 1f)
            && Mathf.IsEqualApprox(vitals.Stamina, 1f);

        vitals.MaxHealth = float.NaN;
        vitals.MaxHunger = float.NaN;
        vitals.MaxThirst = float.NaN;
        vitals.MaxStamina = float.NaN;
        vitals.SecondsToStarve = float.NaN;
        vitals.ThirstRateMultiplier = float.NaN;
        vitals.StarvationDamagePerSecond = float.NaN;
        vitals.RegenPerSecond = float.NaN;
        vitals.StaminaDrainPerSecond = float.NaN;
        vitals.StaminaRecoverPerSecond = float.NaN;
        vitals.LowThreshold = float.NaN;
        vitals._Process(double.NaN);
        bool nonFiniteVitalsBounded = Mathf.IsEqualApprox(vitals.EffectiveMaxHealth, 1f)
            && Mathf.IsEqualApprox(vitals.EffectiveMaxHunger, 1f)
            && Mathf.IsEqualApprox(vitals.EffectiveMaxThirst, 1f)
            && Mathf.IsEqualApprox(vitals.EffectiveMaxStamina, 1f)
            && Mathf.IsEqualApprox(vitals.EffectiveSecondsToStarve, 0f)
            && Mathf.IsEqualApprox(vitals.EffectiveThirstRateMultiplier, 0f)
            && Mathf.IsEqualApprox(vitals.EffectiveStarvationDamagePerSecond, 0f)
            && Mathf.IsEqualApprox(vitals.EffectiveRegenPerSecond, 0f)
            && Mathf.IsEqualApprox(vitals.EffectiveStaminaDrainPerSecond, 0f)
            && Mathf.IsEqualApprox(vitals.EffectiveStaminaRecoverPerSecond, 0f)
            && Mathf.IsEqualApprox(vitals.EffectiveLowThreshold, 0.25f)
            && Mathf.IsEqualApprox(vitals.HealthFraction, 1f)
            && Mathf.IsEqualApprox(vitals.HungerFraction, 1f)
            && Mathf.IsEqualApprox(vitals.ThirstFraction, 1f)
            && Mathf.IsEqualApprox(vitals.StaminaFraction, 1f);

        vitals.Eat(float.NaN);
        vitals.Drink(float.NaN);
        vitals.Heal(float.NaN);
        bool nonFiniteInputsIgnored = Mathf.IsEqualApprox(vitals.Health, 1f)
            && Mathf.IsEqualApprox(vitals.Hunger, 1f)
            && Mathf.IsEqualApprox(vitals.Thirst, 1f);

        state.GameData["survival.health"] = double.NaN;
        state.GameData["survival.hunger"] = double.NaN;
        state.GameData["survival.thirst"] = double.NaN;
        state.GameData["survival.stamina"] = double.NaN;
        vitals.Load(state);
        bool nonFiniteLoadClamped = Mathf.IsEqualApprox(vitals.Health, 1f)
            && Mathf.IsEqualApprox(vitals.Hunger, 1f)
            && Mathf.IsEqualApprox(vitals.Thirst, 1f)
            && Mathf.IsEqualApprox(vitals.Stamina, 1f);

        var malformedVitals = new GameStateData();
        malformedVitals.GameData["survival.health"] = "0.5";
        malformedVitals.GameData["survival.hunger"] = "bad";
        malformedVitals.GameData["survival.thirst"] = new Resource();
        malformedVitals.GameData["survival.stamina"] = "0.25";
        vitals.Load(malformedVitals);
        bool malformedLoadClamped = Mathf.IsEqualApprox(vitals.Health, 0.5f)
            && Mathf.IsEqualApprox(vitals.Hunger, 1f)
            && Mathf.IsEqualApprox(vitals.Thirst, 1f)
            && Mathf.IsEqualApprox(vitals.Stamina, 0.25f);

        vitals.QueueFree();
        return Expect(readyNormalized, "SurvivalVitalsComponent did not normalize invalid maximum values on ready.")
            && Expect(invalidRatesDidNotReverse, "SurvivalVitalsComponent accepted negative drain/recovery rates.")
            && Expect(negativeInputsIgnored, "SurvivalVitalsComponent accepted negative gameplay inputs.")
            && Expect(loadClamped, "SurvivalVitalsComponent did not clamp loaded values to effective maxima.")
            && Expect(nonFiniteVitalsBounded, "SurvivalVitalsComponent accepted non-finite max/rate/threshold values.")
            && Expect(nonFiniteInputsIgnored, "SurvivalVitalsComponent accepted non-finite gameplay inputs.")
            && Expect(nonFiniteLoadClamped, "SurvivalVitalsComponent did not clamp non-finite loaded values.")
            && Expect(malformedLoadClamped, "SurvivalVitalsComponent did not ignore malformed saved vital values.");
    }

    private bool VerifyTemperature()
    {
        var body = new CharacterBody2D { Name = "TemperatureBody" };
        var health = new HealthComponent { Name = "Health" };
        var stats = new StatsComponent { Name = "Stats" };
        var temp = new TemperatureComponent
        {
            Name = "Temperature",
            MinTemp = 50f,
            MaxTemp = -10f,
            CurrentTemp = 200f,
            AmbientTemp = float.NaN,
            FrozenThreshold = 45f,
            ColdThreshold = 10f,
            OverheatThreshold = 0f,
            HeatStrokeThreshold = -5f,
            FrozenDamagePerSec = -1f,
            ColdDamagePerSec = -1f,
            HeatStrokeDamagePerSec = -1f,
            FrozenSpeedPenalty = -1f,
            ColdSpeedPenalty = 2f,
            OverheatSpeedPenalty = -1f,
            HeatStrokeSpeedPenalty = 2f,
            TemperatureRecoveryRate = -1f
        };
        body.AddChild(health);
        body.AddChild(stats);
        body.AddChild(temp);
        AddChild(body);

        bool boundsAndThresholdsSorted = Mathf.IsEqualApprox(temp.EffectiveMinTemp, -10f)
            && Mathf.IsEqualApprox(temp.EffectiveMaxTemp, 50f)
            && Mathf.IsEqualApprox(temp.EffectiveFrozenThreshold, -5f)
            && Mathf.IsEqualApprox(temp.EffectiveColdThreshold, 0f)
            && Mathf.IsEqualApprox(temp.EffectiveOverheatThreshold, 10f)
            && Mathf.IsEqualApprox(temp.EffectiveHeatStrokeThreshold, 45f);

        temp.ApplyTemperatureChange(float.NaN);
        bool nonFiniteIgnored = Mathf.IsEqualApprox(temp.CurrentTemp, 50f);
        bool penaltiesBounded = Mathf.IsEqualApprox(temp.EffectiveFrozenDamagePerSec, 0f)
            && Mathf.IsEqualApprox(temp.EffectiveHeatStrokeDamagePerSec, 0f)
            && Mathf.IsEqualApprox(temp.EffectiveFrozenSpeedPenalty, 0f)
            && Mathf.IsEqualApprox(temp.EffectiveColdSpeedPenalty, 1f)
            && Mathf.IsEqualApprox(temp.EffectiveTemperatureRecoveryRate, 0f);

        temp.FrozenDamagePerSec = float.NaN;
        temp.ColdDamagePerSec = float.NaN;
        temp.HeatStrokeDamagePerSec = float.NaN;
        temp.FrozenSpeedPenalty = float.NaN;
        temp.ColdSpeedPenalty = float.NaN;
        temp.OverheatSpeedPenalty = float.NaN;
        temp.HeatStrokeSpeedPenalty = float.NaN;
        temp.TemperatureRecoveryRate = float.NaN;
        temp.WinterTempOffset = float.NaN;
        temp.SummerTempOffset = float.NaN;
        temp.SnowTempOffset = float.NaN;
        temp.StormTempOffset = float.NaN;
        temp.RainTempOffset = float.NaN;
        temp.SandstormTempOffset = float.NaN;
        temp.NightTempOffset = float.NaN;
        temp.CurrentTemp = float.NaN;
        temp._Process(double.NaN);
        bool nonFiniteTemperatureBounded = float.IsFinite(temp.CurrentTemp)
            && Mathf.IsEqualApprox(temp.EffectiveFrozenDamagePerSec, 0f)
            && Mathf.IsEqualApprox(temp.EffectiveColdDamagePerSec, 0f)
            && Mathf.IsEqualApprox(temp.EffectiveHeatStrokeDamagePerSec, 0f)
            && Mathf.IsEqualApprox(temp.EffectiveFrozenSpeedPenalty, 0.5f)
            && Mathf.IsEqualApprox(temp.EffectiveColdSpeedPenalty, 0.8f)
            && Mathf.IsEqualApprox(temp.EffectiveOverheatSpeedPenalty, 0.9f)
            && Mathf.IsEqualApprox(temp.EffectiveHeatStrokeSpeedPenalty, 0.5f)
            && Mathf.IsEqualApprox(temp.EffectiveTemperatureRecoveryRate, 0f);

        body.QueueFree();
        return Expect(boundsAndThresholdsSorted, "TemperatureComponent did not normalize inverted temperature bounds/thresholds.")
            && Expect(nonFiniteIgnored, "TemperatureComponent accepted a non-finite external temperature change.")
            && Expect(penaltiesBounded, "TemperatureComponent did not bound damage, speed penalty, or recovery tuning.")
            && Expect(nonFiniteTemperatureBounded, "TemperatureComponent accepted non-finite damage, penalty, offset, or frame-time values.");
    }

    private bool VerifyWindAudioAndFeedbackValues()
    {
        var wind = new WindFieldComponent
        {
            PhysicsWindScale = -10f,
            CharacterPushAccel = -20f,
            MaxCharacterWindSpeed = -30f
        };
        bool windBounded = Mathf.IsEqualApprox(wind.EffectivePhysicsWindScale, 0f)
            && Mathf.IsEqualApprox(wind.EffectiveCharacterPushAccel, 0f)
            && Mathf.IsEqualApprox(wind.EffectiveMaxCharacterWindSpeed, 0f);

        wind.PhysicsWindScale = float.NaN;
        wind.CharacterPushAccel = float.NaN;
        wind.MaxCharacterWindSpeed = float.NaN;
        bool nonFiniteWindBounded = Mathf.IsEqualApprox(wind.EffectivePhysicsWindScale, 0f)
            && Mathf.IsEqualApprox(wind.EffectiveCharacterPushAccel, 0f)
            && Mathf.IsEqualApprox(wind.EffectiveMaxCharacterWindSpeed, 0f);

        var audio = new AudioComponent { PitchScale = -2f, Bus = "   " };
        bool audioBounded = audio.EffectivePitchScale > 0f && audio.EffectiveBus == "Master";

        var footstep = new FootstepComponent
        {
            MinSpeed = -1f,
            StepInterval = -1f,
            PitchVariation = 2f,
            Bus = "   "
        };
        bool footstepBounded = Mathf.IsEqualApprox(footstep.EffectiveMinSpeed, 0f)
            && footstep.EffectiveStepInterval >= 0.05f
            && footstep.EffectivePitchVariation <= 0.99f
            && footstep.EffectiveBus == "Master";

        var autoHeal = new AutoHealComponent
        {
            HealPerSecond = -1f,
            HealDelay = -1f,
            MaxHealPerSecond = -1f
        };
        bool autoHealBounded = Mathf.IsEqualApprox(autoHeal.EffectiveHealPerSecond, 0f)
            && Mathf.IsEqualApprox(autoHeal.EffectiveHealDelay, 0f)
            && Mathf.IsEqualApprox(autoHeal.EffectiveMaxHealPerSecond, 0f);

        var bar = new HealthBarComponent
        {
            Size = new Vector2(-4f, 0f),
            HideDelay = -1f
        };
        bool barBounded = bar.EffectiveSize.X >= 1f
            && bar.EffectiveSize.Y >= 1f
            && Mathf.IsEqualApprox(bar.EffectiveHideDelay, 0f);

        var floating = new FloatingTextComponent
        {
            FloatSpeed = -1f,
            Duration = -1f,
            FontSize = -1,
            CritFontSize = 0,
            RandomOffset = -1f
        };
        bool floatingBounded = Mathf.IsEqualApprox(floating.EffectiveFloatSpeed, 0f)
            && floating.EffectiveDuration >= 0.05f
            && floating.EffectiveFontSize == 1
            && floating.EffectiveCritFontSize == 1
            && Mathf.IsEqualApprox(floating.EffectiveRandomOffset, 0f);

        wind.QueueFree();
        audio.QueueFree();
        footstep.QueueFree();
        autoHeal.QueueFree();
        bar.QueueFree();
        floating.QueueFree();
        return Expect(windBounded, "WindFieldComponent did not bound invalid wind tuning.")
            && Expect(nonFiniteWindBounded, "WindFieldComponent accepted non-finite wind tuning.")
            && Expect(audioBounded, "AudioComponent did not bound invalid pitch/bus tuning.")
            && Expect(footstepBounded, "FootstepComponent did not bound invalid speed/timing/pitch tuning.")
            && Expect(autoHealBounded, "AutoHealComponent did not bound invalid healing tuning.")
            && Expect(barBounded, "HealthBarComponent did not bound invalid UI size/timing values.")
            && Expect(floatingBounded, "FloatingTextComponent did not bound invalid text animation values.");
    }

    private bool VerifyAtmosphereComponentBounds()
    {
        var weather = new WeatherSystemComponent
        {
            CycleInterval = -1.0,
            ParticleCount = -20,
            LightningMinInterval = 9.0,
            LightningMaxInterval = -2.0,
            LightningShakeIntensity = -1f,
            WindChangeSpeed = -1f,
            MaxWindMagnitude = -1f,
            GustStrength = 2f,
            CloudCoverage = float.NaN,
            CloudDriftSpeed = -1f,
            CloudShadowStrength = -1f,
            CloudTextureScale = -1f,
            CloudParallax = 2f,
            CloudShadowParallax = -1f,
            TransitionDuration = float.NaN,
            TargetIntensity = float.NaN,
            IntensityLerpSpeed = float.NaN
        };
        bool weatherBounded = weather.EffectiveCycleInterval >= 0.1
            && weather.EffectiveParticleCount == 1
            && Mathf.IsEqualApprox((float)weather.EffectiveLightningMinInterval, 0f)
            && Mathf.IsEqualApprox((float)weather.EffectiveLightningMaxInterval, 9f)
            && Mathf.IsEqualApprox(weather.EffectiveLightningShakeIntensity, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveWindChangeSpeed, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveMaxWindMagnitude, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveGustStrength, 1f)
            && Mathf.IsEqualApprox(weather.EffectiveCloudCoverage, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveCloudDriftSpeed, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveCloudShadowStrength, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveCloudTextureScale, 0.1f)
            && Mathf.IsEqualApprox(weather.EffectiveCloudParallax, 1f)
            && Mathf.IsEqualApprox(weather.EffectiveCloudShadowParallax, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveTransitionDuration, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveTargetIntensity, 0f)
            && Mathf.IsEqualApprox(weather.EffectiveIntensityLerpSpeed, 1.5f);

        var forecastDay = new WeatherData
        {
            WeatherType = " ",
            Intensity = float.NaN,
            Temperature = float.PositiveInfinity,
            WindSpeed = -4f
        };
        var forecast = new WeatherForecast
        {
            DaysForward = System.Array.Empty<WeatherData>(),
            PerlinNoiseScale = float.NaN,
            TemperatureVariance = float.PositiveInfinity,
            BaseTemperature = float.NaN
        };
        forecast.GenerateForecast(0);
        bool forecastBounded = forecastDay.EffectiveWeatherType == "Clear"
            && Mathf.IsEqualApprox(forecastDay.EffectiveIntensity, 0f)
            && Mathf.IsEqualApprox(forecastDay.EffectiveTemperature, 20f)
            && Mathf.IsEqualApprox(forecastDay.EffectiveWindSpeed, 0f)
            && forecast.EffectiveForecastDayCount == 1
            && forecast.DaysForward.Length == 1
            && forecast.DaysForward[0] != null
            && Mathf.IsEqualApprox(forecast.EffectivePerlinNoiseScale, 0.1f)
            && Mathf.IsEqualApprox(forecast.EffectiveTemperatureVariance, 0f)
            && Mathf.IsEqualApprox(forecast.EffectiveBaseTemperature, 20f)
            && float.IsFinite(forecast.DaysForward[0].Temperature);

        var weatherHud = new WeatherHUDComponent { PollInterval = double.NaN };
        bool weatherHudBounded = Mathf.IsEqualApprox((float)weatherHud.EffectivePollInterval, 0.25f);
        weatherHud.PollInterval = -4.0;
        weatherHudBounded = weatherHudBounded
            && Mathf.IsEqualApprox((float)weatherHud.EffectivePollInterval, 0.05f);

        var forecastUi = new WeatherForecastUI
        {
            ItemSize = new Vector2(float.NaN, -1f),
            ItemSpacing = float.NaN,
            SlideSeconds = float.NaN
        };
        bool forecastUiBounded = forecastUi.EffectiveItemSize.IsEqualApprox(new Vector2(72f, 32f))
            && Mathf.IsEqualApprox(forecastUi.EffectiveItemSpacing, 8f)
            && Mathf.IsEqualApprox(forecastUi.EffectiveSlideSeconds, 0f);

        var weatherAudio = new WeatherAudioController
        {
            BusName = "   ",
            CrossFadeDuration = -1f,
            ThunderDelayMin = 7.0,
            ThunderDelayMax = -1.0
        };
        bool weatherAudioBounded = weatherAudio.EffectiveBusName == "Weather"
            && Mathf.IsEqualApprox(weatherAudio.EffectiveCrossFadeDuration, 0f)
            && Mathf.IsEqualApprox((float)weatherAudio.EffectiveThunderDelayMin, 0f)
            && Mathf.IsEqualApprox((float)weatherAudio.EffectiveThunderDelayMax, 7f);

        var ambientAudio = new AmbientAudioComponent
        {
            Bus = "   ",
            CrossfadeDuration = -1f,
            ThunderDelayMin = 5.0,
            ThunderDelayMax = -1.0
        };
        bool ambientAudioBounded = ambientAudio.EffectiveBus == "Master"
            && Mathf.IsEqualApprox(ambientAudio.EffectiveCrossfadeDuration, 0f)
            && Mathf.IsEqualApprox((float)ambientAudio.EffectiveThunderDelayMin, 0f)
            && Mathf.IsEqualApprox((float)ambientAudio.EffectiveThunderDelayMax, 5f);

        var fog = new DynamicFogLayer
        {
            MaxDensity = float.NaN,
            AnimationSpeed = new Vector2(float.NaN, 2f)
        };
        bool fogBounded = Mathf.IsEqualApprox(fog.EffectiveMaxDensity, 0f)
            && fog.EffectiveAnimationSpeed.IsEqualApprox(new Vector2(0f, 2f));

        var cloud = new CloudSpriteLayer
        {
            Count = -1,
            DriftSpeed = float.NaN,
            Opacity = float.NaN,
            Field = new Vector2(-640f, float.NaN),
            BandTop = 0.8f,
            BandBottom = 0.2f,
            FarScale = 3f,
            NearScale = -1f
        };
        bool cloudBounded = cloud.EffectiveCount == 0
            && Mathf.IsEqualApprox(cloud.EffectiveDriftSpeed, 0f)
            && Mathf.IsEqualApprox(cloud.EffectiveOpacity, 0f)
            && cloud.EffectiveField.IsEqualApprox(new Vector2(640f, 720f))
            && Mathf.IsEqualApprox(cloud.EffectiveBandTop, 0.2f)
            && Mathf.IsEqualApprox(cloud.EffectiveBandBottom, 0.8f)
            && Mathf.IsEqualApprox(cloud.EffectiveFarScale, 0.01f)
            && Mathf.IsEqualApprox(cloud.EffectiveNearScale, 3f);

        var spriteWeather = new WeatherSpriteLayer
        {
            Field = new Vector2(-1f, float.NaN),
            Intensity = float.NaN,
            MaxSprites = -1,
            CameraZoom = Vector2.Zero
        };
        bool spriteWeatherBounded = spriteWeather.EffectiveField.IsEqualApprox(new Vector2(1f, 720f))
            && Mathf.IsEqualApprox(spriteWeather.EffectiveIntensity, 0f)
            && spriteWeather.EffectiveMaxSprites == 0
            && spriteWeather.EffectiveCameraZoom.X > 0f
            && spriteWeather.EffectiveCameraZoom.Y > 0f;

        var bolt = new LightningBoltComponent
        {
            Segments = -1,
            Displacement = float.NaN,
            BranchChance = 2f,
            Lifetime = 0f
        };
        bool boltBounded = bolt.EffectiveSegments == 1
            && Mathf.IsEqualApprox(bolt.EffectiveDisplacement, 0f)
            && Mathf.IsEqualApprox(bolt.EffectiveBranchChance, 1f)
            && bolt.EffectiveLifetime > 0f;

        var seasonal = new SeasonalComponent
        {
            DaysPerSeason = double.NaN,
            SeasonTintStrength = float.NaN,
            SpringWindSpeed = -1f,
            SummerWindSpeed = -1f,
            FallWindSpeed = -1f,
            WinterWindSpeed = -1f,
            FoliageWindStrength = -1f,
            TransitionDuration = float.NaN
        };
        bool seasonalBounded = seasonal.EffectiveDaysPerSeason == 1
            && Mathf.IsEqualApprox(seasonal.EffectiveSeasonTintStrength, 0f)
            && Mathf.IsEqualApprox(seasonal.EffectiveSpringWindSpeed, 0f)
            && Mathf.IsEqualApprox(seasonal.EffectiveFoliageWindStrength, 0f)
            && Mathf.IsEqualApprox(seasonal.EffectiveTransitionDuration, 0f);

        var dayNight = new DayNightCycleComponent { DayLengthSeconds = float.NaN };
        bool dayNightBounded = dayNight.EffectiveDayLengthSeconds >= 0.001f;

        var ambient = new AmbientController { EaseSpeed = float.NaN };
        bool ambientBounded = Mathf.IsEqualApprox(ambient.EffectiveEaseSpeed, 0f);

        weather.QueueFree();
        weatherHud.QueueFree();
        forecastUi.QueueFree();
        weatherAudio.QueueFree();
        ambientAudio.QueueFree();
        fog.QueueFree();
        cloud.QueueFree();
        spriteWeather.QueueFree();
        bolt.QueueFree();
        seasonal.QueueFree();
        dayNight.QueueFree();
        ambient.QueueFree();

        return Expect(weatherBounded, "WeatherSystemComponent did not bound invalid weather tuning.")
            && Expect(forecastBounded, "WeatherForecast accepted invalid day arrays or non-finite generated weather values.")
            && Expect(weatherHudBounded, "WeatherHUDComponent accepted invalid poll timing.")
            && Expect(forecastUiBounded, "WeatherForecastUI accepted invalid item layout or slide timing.")
            && Expect(weatherAudioBounded, "WeatherAudioController did not bound invalid bus/fade/thunder tuning.")
            && Expect(ambientAudioBounded, "AmbientAudioComponent did not bound invalid bus/fade/thunder tuning.")
            && Expect(fogBounded, "DynamicFogLayer did not bound invalid density or animation values.")
            && Expect(cloudBounded, "CloudSpriteLayer did not bound invalid count/field/opacity/scale values.")
            && Expect(spriteWeatherBounded, "WeatherSpriteLayer did not bound invalid field/intensity/zoom values.")
            && Expect(boltBounded, "LightningBoltComponent did not bound invalid geometry/lifetime values.")
            && Expect(seasonalBounded, "SeasonalComponent did not bound invalid seasonal tuning.")
            && Expect(dayNightBounded, "DayNightCycleComponent did not bound invalid day length.")
            && Expect(ambientBounded, "AmbientController did not bound invalid easing.");
    }

    private bool VerifyCropGrowthBounds()
    {
        var seasonal = new SeasonalComponent { Name = "Seasonal", AutoCycle = false };
        var cropHost = new Node2D { Name = "CropHost" };
        var crop = new CropGrowthComponent
        {
            Name = "Crop",
            DaysToMaturity = -10f,
            SpringGrowthRate = -1f,
            SummerGrowthRate = -1f,
            FallGrowthRate = -1f,
            WinterGrowthRate = -1f
        };
        AddChild(seasonal);
        cropHost.AddChild(crop);
        AddChild(cropHost);

        bool bounded = crop.EffectiveDaysToMaturity >= 0.01f
            && Mathf.IsEqualApprox(crop.EffectiveSpringGrowthRate, 0f)
            && Mathf.IsEqualApprox(crop.EffectiveSummerGrowthRate, 0f)
            && Mathf.IsEqualApprox(crop.EffectiveFallGrowthRate, 0f)
            && Mathf.IsEqualApprox(crop.EffectiveWinterGrowthRate, 0f)
            && crop.EffectiveDayLengthSeconds >= 1f;

        crop.DaysToMaturity = float.NaN;
        crop.SpringGrowthRate = float.NaN;
        crop.SummerGrowthRate = float.NaN;
        crop.FallGrowthRate = float.NaN;
        crop.WinterGrowthRate = float.NaN;
        crop._Process(double.NaN);
        bool nonFiniteBounded = Mathf.IsEqualApprox(crop.EffectiveDaysToMaturity, 10f)
            && Mathf.IsEqualApprox(crop.EffectiveSpringGrowthRate, 0f)
            && Mathf.IsEqualApprox(crop.EffectiveSummerGrowthRate, 0f)
            && Mathf.IsEqualApprox(crop.EffectiveFallGrowthRate, 0f)
            && Mathf.IsEqualApprox(crop.EffectiveWinterGrowthRate, 0f)
            && crop.GetGrowthProgress() >= 0f
            && crop.GetGrowthProgress() <= 1f;

        cropHost.QueueFree();
        seasonal.QueueFree();
        return Expect(bounded, "CropGrowthComponent did not bound invalid growth timing/rates.")
            && Expect(nonFiniteBounded, "CropGrowthComponent accepted non-finite growth timing/rates or frame time.");
    }

    private bool VerifyUiVisualEffectBounds()
    {
        var shake = new ShakeComponent
        {
            Intensity = -1f,
            Duration = float.NaN,
            Vibrato = -1
        };
        bool shakeBounded = Mathf.IsEqualApprox(shake.EffectiveIntensity, 0f)
            && shake.EffectiveDuration >= 0.001f
            && shake.EffectiveVibrato == 1;

        var pulse = new PulseComponent
        {
            MinScale = 3f,
            MaxScale = -1f,
            Speed = float.NaN
        };
        bool pulseBounded = Mathf.IsEqualApprox(pulse.EffectiveMinScale, 0.01f)
            && Mathf.IsEqualApprox(pulse.EffectiveMaxScale, 3f)
            && Mathf.IsEqualApprox(pulse.EffectiveSpeed, 0f);

        var effect = new UIEffectComponent
        {
            Duration = float.NaN,
            InitialDelay = -1f,
            LoopDelay = -1f,
            SlideDistance = -1f,
            ShakeIntensity = -1f,
            ShakeVibrato = 0,
            PulseMinScale = 4f,
            PulseMaxScale = -2f,
            PulseLoops = -5,
            BobHeight = -1f,
            BobSpeed = float.NaN,
            FlashCount = 0,
            GlitchIntensity = -1f,
            GlitchSegments = 0,
            RotateAngle = float.NaN,
            FadeTargetAlpha = 2f,
            TypewriterSpeed = float.NaN,
            BounceHeight = -1f,
            BounceCount = 0,
            OffsetTarget = new Vector2(float.NaN, -8f)
        };
        bool effectBounded = effect.EffectiveDuration >= 0.001f
            && Mathf.IsEqualApprox(effect.EffectiveInitialDelay, 0f)
            && Mathf.IsEqualApprox(effect.EffectiveLoopDelay, 0f)
            && Mathf.IsEqualApprox(effect.EffectiveSlideDistance, 0f)
            && Mathf.IsEqualApprox(effect.EffectiveShakeIntensity, 0f)
            && effect.EffectiveShakeVibrato == 1
            && Mathf.IsEqualApprox(effect.EffectivePulseMinScale, 0.01f)
            && Mathf.IsEqualApprox(effect.EffectivePulseMaxScale, 4f)
            && effect.EffectivePulseLoops == 0
            && Mathf.IsEqualApprox(effect.EffectiveBobHeight, 0f)
            && Mathf.IsEqualApprox(effect.EffectiveBobSpeed, 0f)
            && effect.EffectiveFlashCount == 1
            && Mathf.IsEqualApprox(effect.EffectiveGlitchIntensity, 0f)
            && effect.EffectiveGlitchSegments == 1
            && Mathf.IsEqualApprox(effect.EffectiveRotateAngle, 0f)
            && Mathf.IsEqualApprox(effect.EffectiveFadeTargetAlpha, 1f)
            && Mathf.IsEqualApprox(effect.EffectiveTypewriterSpeed, 30f)
            && Mathf.IsEqualApprox(effect.EffectiveBounceHeight, 0f)
            && effect.EffectiveBounceCount == 1
            && effect.EffectiveOffsetTarget.IsEqualApprox(new Vector2(0f, -8f));

        var vignette = new VignetteComponent
        {
            Intensity = float.NaN,
            Softness = -1f,
            Radius = 2f
        };
        bool vignetteBounded = Mathf.IsEqualApprox(vignette.EffectiveIntensity, 0f)
            && vignette.EffectiveSoftness >= 0.001f
            && Mathf.IsEqualApprox(vignette.EffectiveRadius, 1f);

        var chromatic = new ChromaticAberrationComponent { Strength = float.NaN };
        bool chromaticBounded = Mathf.IsEqualApprox(chromatic.EffectiveStrength, 0f);

        var menu = new AnimatedMenuComponent
        {
            Duration = float.NaN,
            StaggerDelay = -1f,
            InitialDelay = -1f
        };
        bool menuBounded = menu.EffectiveDuration >= 0.001f
            && Mathf.IsEqualApprox(menu.EffectiveStaggerDelay, 0f)
            && Mathf.IsEqualApprox(menu.EffectiveInitialDelay, 0f);

        var carousel = new CarouselComponent
        {
            CardWidth = -1f,
            Spacing = -1f,
            TransitionDuration = float.NaN,
            InactiveScale = -1f,
            InactiveAlpha = 2f,
            AutoPlayInterval = -1f
        };
        bool carouselBounded = carousel.EffectiveCardWidth >= 1f
            && Mathf.IsEqualApprox(carousel.EffectiveSpacing, 0f)
            && carousel.EffectiveTransitionDuration >= 0.001f
            && carousel.EffectiveInactiveScale >= 0.01f
            && Mathf.IsEqualApprox(carousel.EffectiveInactiveAlpha, 1f)
            && carousel.EffectiveAutoPlayInterval >= 0.05f;

        shake.QueueFree();
        pulse.QueueFree();
        effect.QueueFree();
        vignette.QueueFree();
        chromatic.QueueFree();
        menu.QueueFree();
        carousel.QueueFree();

        return Expect(shakeBounded, "ShakeComponent did not bound invalid shake values.")
            && Expect(pulseBounded, "PulseComponent did not normalize scale/speed values.")
            && Expect(effectBounded, "UIEffectComponent did not bound invalid timing/effect values.")
            && Expect(vignetteBounded, "VignetteComponent did not bound invalid shader values.")
            && Expect(chromaticBounded, "ChromaticAberrationComponent did not bound invalid shader strength.")
            && Expect(menuBounded, "AnimatedMenuComponent did not bound invalid animation timing.")
            && Expect(carouselBounded, "CarouselComponent did not bound invalid carousel values.");
    }

    private bool Expect(bool condition, string failure)
    {
        if (condition)
            return true;

        Failure = failure;
        return false;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}

public partial class HazardProbe : HazardComponent
{
    public void Enter(Node2D body) => OnBodyEntered(body);
}
