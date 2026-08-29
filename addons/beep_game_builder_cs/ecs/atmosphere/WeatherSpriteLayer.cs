using Godot;
using System.Collections.Generic;

namespace Beep.ECS
{
    /// <summary>
    /// Screen-space pixel weather renderer. It draws hard-edged sprites directly instead of
    /// velocity-stretched particles, so 2D games get readable rain, snow, hail, and sand.
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class WeatherSpriteLayer : Node2D
    {
        public enum PixelWeatherKind
        {
            Rain,
            Storm,
            Snow,
            Hail,
            Sandstorm,
        }

        [Export] public PixelWeatherKind Kind { get; set; } = PixelWeatherKind.Rain;
        [Export] public Texture2D? RainTexture { get; set; }
        [Export] public Texture2D? SplashTexture { get; set; }
        [Export] public Texture2D? SnowTexture { get; set; }
        [Export] public Texture2D? HailTexture { get; set; }
        [Export] public Texture2D? SandTexture { get; set; }
        [Export] public Vector2 Field { get; set; } = new(1280, 720);
        [Export(PropertyHint.Range, "0,1,0.01")] public float Intensity { get; set; } = 1f;
        [Export] public Vector2 Wind { get; set; } = Vector2.Zero;
        [Export] public WeatherSystemComponent.WeatherViewMode ViewMode { get; set; } = WeatherSystemComponent.WeatherViewMode.Side;
        [Export] public int MaxSprites { get; set; } = 260;
        [Export] public bool UseCollisionSplashes { get; set; } = true;
        [Export(PropertyHint.Layers2DPhysics)] public uint SplashCollisionMask { get; set; } = 1;
        [Export] public Vector2 CameraCenter { get; set; } = Vector2.Zero;
        [Export] public Vector2 CameraZoom { get; set; } = Vector2.One;
        [Export] public int Seed { get; set; } = 44621;
        [ExportGroup("Rendering")]
        [Export] public bool UsePixelArtSampling { get; set; } = false;
        [Export] public bool SnapToPixelGrid { get; set; } = false;

        private readonly List<Fleck> _flecks = new();
        private readonly List<Impact> _impacts = new();
        private RandomNumberGenerator _rng = new();
        private Vector2 _lastField = Vector2.Zero;
        private int _lastMaxSprites = -1;
        private PixelWeatherKind _lastKind;

        public Vector2 EffectiveField => new(
            Mathf.Max(1f, float.IsFinite(Field.X) ? Mathf.Abs(Field.X) : 1280f),
            Mathf.Max(1f, float.IsFinite(Field.Y) ? Mathf.Abs(Field.Y) : 720f));
        public float EffectiveIntensity => Mathf.Clamp(float.IsFinite(Intensity) ? Intensity : 0f, 0f, 1f);
        public int EffectiveMaxSprites => Mathf.Clamp(MaxSprites, 0, 1000);
        public Vector2 EffectiveCameraZoom => new(
            Mathf.Max(0.001f, float.IsFinite(CameraZoom.X) ? Mathf.Abs(CameraZoom.X) : 1f),
            Mathf.Max(0.001f, float.IsFinite(CameraZoom.Y) ? Mathf.Abs(CameraZoom.Y) : 1f));

        private struct Fleck
        {
            public Vector2 Position;
            public float Speed;
            public float Scale;
            public float Alpha;
            public float Phase;
            public float WidthMul;
        }

        private struct Impact
        {
            public Vector2 Position;
            public float Age;
            public float Life;
            public float Scale;
            public float Alpha;
            public float WidthMul;
        }

        public override void _Ready()
        {
            ApplyTextureFilter();
            _rng = new RandomNumberGenerator { Seed = (ulong)Seed };
            Rebuild();
        }

        public void Rebuild()
        {
            _flecks.Clear();
            _impacts.Clear();
            Vector2 field = EffectiveField;
            _lastField = field;
            _lastMaxSprites = EffectiveMaxSprites;
            _lastKind = Kind;

            int count = EffectiveMaxSprites;
            for (int i = 0; i < count; i++)
                _flecks.Add(NewFleck(true));
        }

        public override void _Process(double delta)
        {
            if (Engine.IsEditorHint()) return;
            ApplyTextureFilter();
            if (!EffectiveField.IsEqualApprox(_lastField) || EffectiveMaxSprites != _lastMaxSprites || Kind != _lastKind)
                Rebuild();

            float visibleIntensity = EffectiveIntensity;
            Visible = visibleIntensity > 0.01f;
            if (!Visible) return;

            int active = Mathf.Clamp((int)(_flecks.Count * visibleIntensity), 0, _flecks.Count);
            Vector2 dir = TravelDirection();
            float dt = double.IsFinite(delta) ? Mathf.Max(0f, (float)delta) : 0f;

            for (int i = 0; i < active; i++)
            {
                Fleck f = _flecks[i];
                Vector2 previous = f.Position;
                f.Position += dir * f.Speed * dt;
                f.Position += Wobble(f) * dt;
                if (CanImpact() && TryGetCollisionImpact(previous, f.Position, out Vector2 impact))
                {
                    SpawnImpact(impact);
                    f = NewFleck(false);
                    _flecks[i] = f;
                    continue;
                }
                if (Offscreen(f.Position))
                {
                    if (CanImpact())
                        SpawnImpact(f.Position);
                    f = NewFleck(false);
                }
                _flecks[i] = f;
            }

            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                Impact impact = _impacts[i];
                impact.Age += dt;
                if (impact.Age >= impact.Life)
                    _impacts.RemoveAt(i);
                else
                    _impacts[i] = impact;
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            if (!Visible) return;
            float visibleIntensity = EffectiveIntensity;
            int active = Mathf.Clamp((int)(_flecks.Count * visibleIntensity), 0, _flecks.Count);
            Texture2D? texture = TextureForKind();
            Vector2 baseSize = TextureSizeOrFallback(texture);
            Color tint = TintForKind();

            for (int i = 0; i < active; i++)
            {
                Fleck f = _flecks[i];
                Vector2 size = SizeForKind(baseSize, f.Scale, f.WidthMul);
                Rect2 rect = new(SnapIfNeeded(f.Position - size * 0.5f), SizeForDraw(size));
                Color c = tint with { A = tint.A * f.Alpha * visibleIntensity };
                if (texture != null)
                    DrawTextureRect(texture, rect, false, c);
                else
                    DrawRect(rect, c);
            }

            DrawImpacts(visibleIntensity);
        }

        private Fleck NewFleck(bool anywhere)
        {
            Vector2 start = SpawnPosition(anywhere);
            return new Fleck
            {
                Position = start,
                Speed = SpeedRange(),
                Scale = ScaleRange(),
                Alpha = _rng.RandfRange(0.62f, 1f),
                Phase = _rng.RandfRange(0f, Mathf.Tau),
                WidthMul = Kind == PixelWeatherKind.Sandstorm ? _rng.RandfRange(1f, 2.3f) : 1f,
            };
        }

        private Vector2 SpawnPosition(bool anywhere)
        {
            Vector2 field = EffectiveField;
            if (Kind == PixelWeatherKind.Sandstorm)
            {
                float x = anywhere ? _rng.RandfRange(-12f, field.X + 12f) : _rng.RandfRange(-42f, -8f);
                float y = _rng.RandfRange(field.Y * 0.18f, field.Y * 0.92f);
                return new Vector2(x, y);
            }

            float spawnPad = Kind == PixelWeatherKind.Snow ? 18f : 30f;
            float xPos = _rng.RandfRange(-spawnPad, field.X + spawnPad);
            float yPos = anywhere ? _rng.RandfRange(-spawnPad, field.Y + spawnPad) : _rng.RandfRange(-44f, -4f);
            return new Vector2(xPos, yPos);
        }

        private float SpeedRange() => Kind switch
        {
            PixelWeatherKind.Storm => _rng.RandfRange(310f, 520f),
            PixelWeatherKind.Rain => _rng.RandfRange(210f, 390f),
            PixelWeatherKind.Snow => _rng.RandfRange(22f, 68f),
            PixelWeatherKind.Hail => _rng.RandfRange(300f, 560f),
            PixelWeatherKind.Sandstorm => _rng.RandfRange(120f, 340f),
            _ => 180f,
        };

        private float ScaleRange() => Kind switch
        {
            PixelWeatherKind.Storm => _rng.RandfRange(1.0f, 1.35f),
            PixelWeatherKind.Rain => _rng.RandfRange(0.85f, 1.15f),
            PixelWeatherKind.Snow => _rng.RandfRange(0.75f, 1.25f),
            PixelWeatherKind.Hail => _rng.RandfRange(0.85f, 1.2f),
            PixelWeatherKind.Sandstorm => _rng.RandfRange(0.65f, 1.45f),
            _ => 1f,
        };

        private Vector2 TravelDirection()
        {
            float windX = Mathf.Clamp(float.IsFinite(Wind.X) ? Wind.X * 0.08f : 0f, -0.38f, 0.38f);
            bool topLike = IsTopDownLike(ViewMode);
            return Kind switch
            {
                PixelWeatherKind.Sandstorm => new Vector2(1f + Mathf.Abs(windX), topLike ? 0.05f : 0.12f).Normalized(),
                PixelWeatherKind.Snow => new Vector2(windX * 0.5f, 1f).Normalized(),
                PixelWeatherKind.Hail => new Vector2((topLike ? 0.04f : 0.1f) + windX * 0.35f, 1f).Normalized(),
                PixelWeatherKind.Storm => ViewMode == WeatherSystemComponent.WeatherViewMode.Isometric
                    ? new Vector2(0.22f + windX, 0.78f).Normalized()
                    : new Vector2(0.18f + windX, 1f).Normalized(),
                _ => ViewMode == WeatherSystemComponent.WeatherViewMode.Isometric
                    ? new Vector2(0.16f + windX, 0.82f).Normalized()
                    : new Vector2((topLike ? 0.04f : 0.12f) + windX, 1f).Normalized(),
            };
        }

        private Vector2 Wobble(Fleck f)
        {
            float t = (float)Time.GetTicksMsec() * 0.001f + f.Phase;
            return Kind switch
            {
                PixelWeatherKind.Snow => new Vector2(Mathf.Sin(t * 1.8f) * 22f, Mathf.Cos(t * 0.9f) * 3f),
                PixelWeatherKind.Sandstorm => new Vector2(0f, Mathf.Sin(t * 6f) * 28f),
                PixelWeatherKind.Storm => new Vector2(Mathf.Sin(t * 5f) * 6f, 0f),
                _ => Vector2.Zero,
            };
        }

        private bool Offscreen(Vector2 p)
        {
            Vector2 field = EffectiveField;
            if (Kind == PixelWeatherKind.Sandstorm)
                return p.X > field.X + 48f || p.Y < -32f || p.Y > field.Y + 32f;
            return p.Y > field.Y + 24f || p.X < -48f || p.X > field.X + 48f;
        }

        private bool CanImpact() =>
            Kind is PixelWeatherKind.Rain or PixelWeatherKind.Storm or PixelWeatherKind.Hail;

        private bool TryGetCollisionImpact(Vector2 fromScreen, Vector2 toScreen, out Vector2 hitScreen)
        {
            hitScreen = Vector2.Zero;
            if (!UseCollisionSplashes || IsTopDownLike(ViewMode) || SplashCollisionMask == 0)
                return false;

            World2D? world = GetWorld2D();
            var space = world?.DirectSpaceState;
            if (space == null) return false;

            Vector2 fromWorld = ScreenToWorld(fromScreen);
            Vector2 toWorld = ScreenToWorld(toScreen);
            var query = PhysicsRayQueryParameters2D.Create(fromWorld, toWorld, SplashCollisionMask);
            query.CollideWithAreas = false;
            query.CollideWithBodies = true;

            var result = space.IntersectRay(query);
            if (result.Count == 0) return false;

            Vector2 hitWorld = result["position"].AsVector2();
            hitScreen = WorldToScreen(hitWorld);
            Vector2 field = EffectiveField;
            return hitScreen.X >= -16f && hitScreen.X <= field.X + 16f
                && hitScreen.Y >= -16f && hitScreen.Y <= field.Y + 16f;
        }

        private Vector2 ScreenToWorld(Vector2 screen) =>
            CameraCenter + (screen - EffectiveField * 0.5f) * EffectiveCameraZoom;

        private Vector2 WorldToScreen(Vector2 world) =>
            (world - CameraCenter) / EffectiveCameraZoom + EffectiveField * 0.5f;

        private Texture2D? TextureForKind() => Kind switch
        {
            PixelWeatherKind.Rain or PixelWeatherKind.Storm => RainTexture,
            PixelWeatherKind.Snow => SnowTexture,
            PixelWeatherKind.Hail => HailTexture,
            PixelWeatherKind.Sandstorm => SandTexture,
            _ => null,
        };

        private static Vector2 TextureSizeOrFallback(Texture2D? texture) =>
            texture != null ? new Vector2(texture.GetWidth(), texture.GetHeight()) : new Vector2(3f, 3f);

        private Vector2 SizeForKind(Vector2 baseSize, float scale, float widthMul) => Kind switch
        {
            PixelWeatherKind.Storm => new Vector2(baseSize.X, baseSize.Y) * scale,
            PixelWeatherKind.Rain => new Vector2(baseSize.X, baseSize.Y) * scale,
            PixelWeatherKind.Snow => baseSize * scale,
            PixelWeatherKind.Hail => baseSize * scale,
            PixelWeatherKind.Sandstorm => new Vector2(baseSize.X * widthMul, baseSize.Y) * scale,
            _ => baseSize * scale,
        };

        private Color TintForKind() => Kind switch
        {
            PixelWeatherKind.Storm => new Color(0.58f, 0.70f, 0.90f, 0.88f),
            PixelWeatherKind.Rain => new Color(0.66f, 0.82f, 1.0f, 0.78f),
            PixelWeatherKind.Snow => new Color(0.92f, 0.98f, 1.0f, 0.9f),
            PixelWeatherKind.Hail => new Color(0.80f, 0.90f, 1.0f, 0.95f),
            PixelWeatherKind.Sandstorm => new Color(0.96f, 0.68f, 0.34f, 0.62f),
            _ => Colors.White,
        };

        private void SpawnImpact(Vector2 source)
        {
            if (_impacts.Count > (Kind == PixelWeatherKind.Storm ? 130 : 80)) return;
            bool topLike = IsTopDownLike(ViewMode);
            Vector2 field = EffectiveField;
            float y = topLike
                ? _rng.RandfRange(field.Y * 0.18f, field.Y * 0.95f)
                : source.Y;
            if (!topLike && (y < -8f || y > field.Y + 8f))
                y = _rng.RandfRange(field.Y * 0.83f, field.Y * 0.97f);
            _impacts.Add(new Impact
            {
                Position = new Vector2(Mathf.Wrap(source.X, 0f, field.X), y),
                Age = 0f,
                Life = Kind == PixelWeatherKind.Hail ? _rng.RandfRange(0.10f, 0.18f) : _rng.RandfRange(0.16f, 0.28f),
                Scale = _rng.RandfRange(0.95f, Kind == PixelWeatherKind.Storm ? 1.55f : 1.25f),
                Alpha = _rng.RandfRange(0.55f, Kind == PixelWeatherKind.Hail ? 0.9f : 0.82f),
                WidthMul = _rng.RandfRange(0.85f, 1.35f),
            });
        }

        private void DrawImpacts(float visibleIntensity)
        {
            if (Kind is not (PixelWeatherKind.Rain or PixelWeatherKind.Storm or PixelWeatherKind.Hail)) return;
            foreach (Impact impact in _impacts)
            {
                float t = Mathf.Clamp(impact.Age / impact.Life, 0f, 1f);
                Color c = (Kind == PixelWeatherKind.Hail
                    ? new Color(0.86f, 0.94f, 1f, impact.Alpha)
                    : new Color(0.62f, 0.78f, 1f, impact.Alpha)) with { A = impact.Alpha * (1f - t) * visibleIntensity };
                DrawPixelSplash(impact, t, c);
            }
        }

        private void DrawPixelSplash(Impact impact, float t, Color color)
        {
            float spread = Mathf.Lerp(4f, Kind == PixelWeatherKind.Hail ? 8f : 12f, t) * impact.Scale * impact.WidthMul;
            float pixel = Mathf.Max(1f, Mathf.Round(impact.Scale));
            Vector2 p = SnapIfNeeded(impact.Position);

            // Ground contact: a short horizontal mark.
            DrawRect(new Rect2(SnapIfNeeded(new Vector2(p.X - spread * 0.5f, p.Y)), SizeForDraw(new Vector2(spread, pixel))), color);

            if (Kind == PixelWeatherKind.Hail)
            {
                DrawRect(new Rect2(SnapIfNeeded(new Vector2(p.X - pixel, p.Y - pixel * 2f)), SizeForDraw(new Vector2(pixel, pixel))), color);
                DrawRect(new Rect2(SnapIfNeeded(new Vector2(p.X + pixel * 2f, p.Y - pixel)), SizeForDraw(new Vector2(pixel, pixel))), color);
                return;
            }

            // Two side flecks and one center fleck sell the splash without looking like bubbles.
            float lift = Mathf.Lerp(3f, 0f, t) * impact.Scale;
            DrawRect(new Rect2(SnapIfNeeded(new Vector2(p.X - spread * 0.55f, p.Y - lift)), SizeForDraw(new Vector2(pixel, pixel))), color);
            DrawRect(new Rect2(SnapIfNeeded(new Vector2(p.X + spread * 0.45f, p.Y - lift * 0.8f)), SizeForDraw(new Vector2(pixel, pixel))), color);
            if (Kind == PixelWeatherKind.Storm)
                DrawRect(new Rect2(SnapIfNeeded(new Vector2(p.X, p.Y - lift * 1.35f - pixel)), SizeForDraw(new Vector2(pixel, pixel))), color);
        }

        private void ApplyTextureFilter()
        {
            var wanted = UsePixelArtSampling ? TextureFilterEnum.Nearest : TextureFilterEnum.Linear;
            if (TextureFilter != wanted) TextureFilter = wanted;
        }

        private Vector2 SnapIfNeeded(Vector2 v)
            => SnapToPixelGrid ? new Vector2(Mathf.Round(v.X), Mathf.Round(v.Y)) : v;

        private Vector2 SizeForDraw(Vector2 size)
            => SnapToPixelGrid ? new Vector2(Mathf.Max(1f, Mathf.Round(size.X)), Mathf.Max(1f, Mathf.Round(size.Y))) : size;

        private static bool IsTopDownLike(WeatherSystemComponent.WeatherViewMode view) =>
            view is WeatherSystemComponent.WeatherViewMode.TopDown
                or WeatherSystemComponent.WeatherViewMode.RpgTopDown
                or WeatherSystemComponent.WeatherViewMode.Isometric
                or WeatherSystemComponent.WeatherViewMode.CityBuilder;
    }
}
