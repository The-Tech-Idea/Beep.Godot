using Godot;

namespace Beep.GameBuilder
{
    /// <summary>
    /// Single day's weather forecast data. Serializable resource for persistence.
    /// </summary>
    [GlobalClass]
    public partial class WeatherData : Resource
    {
        [Export] public string WeatherType { get; set; } = "Clear";
        [Export] public float Intensity { get; set; } = 0.0f;
        [Export] public float Temperature { get; set; } = 20.0f;
        [Export] public float WindSpeed { get; set; } = 0.0f;
        public string EffectiveWeatherType => string.IsNullOrWhiteSpace(WeatherType) ? "Clear" : WeatherType;
        public float EffectiveIntensity => float.IsFinite(Intensity) ? Mathf.Clamp(Intensity, 0f, 1f) : 0f;
        public float EffectiveTemperature => float.IsFinite(Temperature) ? Mathf.Clamp(Temperature, -100f, 100f) : 20f;
        public float EffectiveWindSpeed => float.IsFinite(WindSpeed) ? Mathf.Clamp(WindSpeed, 0f, 200f) : 0f;
    }

    /// <summary>
    /// Multi-day weather forecast generator using Perlin noise.
    /// Deterministic based on seed so forecasts are repeatable.
    /// </summary>
    [GlobalClass]
    public partial class WeatherForecast : Resource
    {
        [Export] public WeatherData[] DaysForward { get; set; } = new WeatherData[7];
        [Export] public int RandomSeed { get; set; } = 12345;
        [Export] public float PerlinNoiseScale { get; set; } = 0.1f;
        [Export] public float TemperatureVariance { get; set; } = 10.0f;
        [Export] public float BaseTemperature { get; set; } = 20.0f;
        public int EffectiveForecastDayCount => Mathf.Clamp(DaysForward?.Length ?? 7, 1, 31);
        public float EffectivePerlinNoiseScale => float.IsFinite(PerlinNoiseScale)
            ? Mathf.Clamp(PerlinNoiseScale, 0.001f, 10f)
            : 0.1f;
        public float EffectiveTemperatureVariance => float.IsFinite(TemperatureVariance)
            ? Mathf.Clamp(TemperatureVariance, 0f, 100f)
            : 0f;
        public float EffectiveBaseTemperature => float.IsFinite(BaseTemperature)
            ? Mathf.Clamp(BaseTemperature, -100f, 100f)
            : 20f;

        // Names match WeatherSystemComponent.WeatherType so a consumer that Enum.TryParses
        // WeatherData.WeatherType gets a real value.
        public enum WeatherType
        {
            Clear,
            Cloudy,
            Rain,
            Snow,
            Storm,
            Fog,
            Sandstorm,
            Hail,
            LeafFall,
            Heatwave
        }

        /// <summary>
        /// Generate a 7-day forecast deterministically seeded by the starting day number.
        /// </summary>
        public void GenerateForecast(int dayStart)
        {
            WeatherData[] days = NormalizeDaysForward();
            float scale = EffectivePerlinNoiseScale;
            float baseTemperature = EffectiveBaseTemperature;
            float temperatureVariance = EffectiveTemperatureVariance;
            var moistureNoise = NewNoise(RandomSeed, scale);
            var pressureNoise = NewNoise(RandomSeed + 179, scale * 0.73f);
            var temperatureNoise = NewNoise(RandomSeed + 353, scale * 0.47f);
            var windNoise = NewNoise(RandomSeed + 719, scale * 1.31f);

            for (int i = 0; i < days.Length; i++)
            {
                days[i] ??= new WeatherData();

                float x = dayStart + i;
                float moisture = UnitNoise(moistureNoise.GetNoise1D(x));
                float pressure = UnitNoise(pressureNoise.GetNoise1D(x + 41.7f));
                float temperatureBand = Mathf.Sin(x * scale * Mathf.Tau);
                float temperatureJitter = temperatureNoise.GetNoise1D(x + 97.3f);
                float temperature = baseTemperature
                    + temperatureBand * temperatureVariance
                    + temperatureJitter * temperatureVariance * 0.35f;

                WeatherType type = PickWeather(moisture, pressure, temperature);
                float severity = SeverityFor(type, moisture, pressure, temperature);
                float wind = WindFor(type, severity, UnitNoise(windNoise.GetNoise1D(x + 13.2f)));

                days[i].WeatherType = type.ToString();
                days[i].Intensity = severity;
                days[i].Temperature = temperature;
                days[i].WindSpeed = wind;
            }
        }

        private WeatherData[] NormalizeDaysForward()
        {
            int count = EffectiveForecastDayCount;
            if (DaysForward == null || DaysForward.Length != count)
                DaysForward = new WeatherData[count];
            return DaysForward;
        }

        private static FastNoiseLite NewNoise(int seed, float frequency) => new()
        {
            Seed = seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            Frequency = frequency,
            FractalOctaves = 3,
        };

        private static float UnitNoise(float value)
            => Mathf.Clamp(value * 0.5f + 0.5f, 0f, 1f);

        private static WeatherType PickWeather(float moisture, float pressure, float temperature)
        {
            if (temperature <= 0f && moisture > 0.48f) return moisture > 0.82f ? WeatherType.Hail : WeatherType.Snow;
            if (temperature >= 38f && pressure > 0.62f) return WeatherType.Heatwave;
            if (temperature >= 28f && moisture < 0.18f && pressure > 0.48f) return WeatherType.Sandstorm;
            if (moisture > 0.82f && pressure < 0.38f) return WeatherType.Storm;
            if (moisture > 0.63f) return WeatherType.Rain;
            if (moisture > 0.47f) return WeatherType.Cloudy;
            if (pressure < 0.18f && moisture > 0.32f) return WeatherType.Fog;
            if (temperature < 14f && moisture > 0.34f) return WeatherType.LeafFall;
            return WeatherType.Clear;
        }

        private static float SeverityFor(WeatherType type, float moisture, float pressure, float temperature) => type switch
        {
            WeatherType.Clear => Mathf.Lerp(0.02f, 0.12f, 1f - moisture),
            WeatherType.Cloudy => Mathf.Lerp(0.30f, 0.58f, moisture),
            WeatherType.Rain => Mathf.Lerp(0.45f, 0.82f, moisture),
            WeatherType.Snow => Mathf.Lerp(0.42f, 0.78f, moisture),
            WeatherType.Storm => Mathf.Lerp(0.72f, 1.0f, Mathf.Max(moisture, 1f - pressure)),
            WeatherType.Fog => Mathf.Lerp(0.35f, 0.72f, 1f - pressure),
            WeatherType.Sandstorm => Mathf.Lerp(0.56f, 0.92f, Mathf.Max(1f - moisture, pressure)),
            WeatherType.Hail => Mathf.Lerp(0.65f, 0.95f, moisture),
            WeatherType.LeafFall => Mathf.Lerp(0.24f, 0.50f, moisture),
            WeatherType.Heatwave => Mathf.Lerp(0.50f, 0.90f, Mathf.Clamp((temperature - 32f) / 14f, 0f, 1f)),
            _ => 0.2f,
        };

        private static float WindFor(WeatherType type, float severity, float gustNoise)
        {
            float baseWind = type switch
            {
                WeatherType.Clear => 1.2f,
                WeatherType.Cloudy => 2.4f,
                WeatherType.Rain => 4.2f,
                WeatherType.Snow => 3.0f,
                WeatherType.Storm => 7.4f,
                WeatherType.Fog => 1.0f,
                WeatherType.Sandstorm => 8.0f,
                WeatherType.Hail => 6.2f,
                WeatherType.LeafFall => 2.8f,
                WeatherType.Heatwave => 1.6f,
                _ => 2.0f,
            };
            return Mathf.Clamp(baseWind * Mathf.Lerp(0.55f, 1.25f, severity) + gustNoise * 1.4f, 0f, 12f);
        }
    }
}
