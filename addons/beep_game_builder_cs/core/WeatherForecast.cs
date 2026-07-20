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

        // Names match WeatherSystemComponent.WeatherType (Clear/Cloudy/Rain/Snow/Storm) so a consumer
        // that Enum.TryParses the stamped WeatherData.WeatherType gets a real value — "Rainy"/"Stormy"
        // parsed to nothing (ApplyTuning already rejects "Rainy").
        public enum WeatherType
        {
            Clear,
            Cloudy,
            Rain,
            Storm
        }

        /// <summary>
        /// Generate a 7-day forecast deterministically seeded by the starting day number.
        /// </summary>
        public void GenerateForecast(int dayStart)
        {
            var noise = new FastNoiseLite
            {
                Seed = RandomSeed,
                NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
                Frequency = PerlinNoiseScale
            };

            for (int i = 0; i < DaysForward.Length; i++)
            {
                DaysForward[i] = DaysForward[i] ?? new WeatherData();

                float dayOffset = (dayStart + i) * PerlinNoiseScale;
                float noiseValue = Mathf.Clamp(noise.GetNoise1D(dayOffset) * 0.5f + 0.5f, 0f, 1f);

                // Determine weather type from noise value
                WeatherType type = noiseValue switch
                {
                    < 0.2f => WeatherType.Rain,
                    < 0.4f => WeatherType.Storm,
                    < 0.6f => WeatherType.Cloudy,
                    _ => WeatherType.Clear
                };

                DaysForward[i].WeatherType = type.ToString();
                DaysForward[i].Intensity = noiseValue;
                DaysForward[i].Temperature = 20f + Mathf.Sin(dayOffset * 6.28f) * TemperatureVariance;
                DaysForward[i].WindSpeed = noiseValue * 10f;
            }
        }
    }
}
