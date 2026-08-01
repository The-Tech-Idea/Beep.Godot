using Godot;
using System.Linq;
using Beep.ECS.UI;
using Beep.GameBuilder;

/// Asserts every genre's tuning JSON actually lands on GameInfo.
///
/// BeepGenreGenerator.ApplyTuning is public and static, so this needs no editor and no generated
/// project — it exercises the exact call the generator makes. The link BEYOND this (GameInfo to
/// the components) is what was broken and is fixed separately; this one guards the half that the
/// generator owns, so a tuning key added to genre.json and forgotten in ApplyTuning cannot pass.
public partial class TuningProbe : Node
{
    public override void _Ready()
    {
        int bad = 0, checked_ = 0;
        foreach (var genre in SkinCatalog.AllGenres.Values.OrderBy(g => g.Id))
        {
            var info = new GameInfo();
            BeepGenreGenerator.ApplyTuning(info, genre);
            var t = genre.Tuning;

            void Check(string key, string what, double got)
            {
                if (!t.ContainsKey(key)) return;
                checked_++;
                double want = t[key].AsDouble();
                if (Mathf.IsEqualApprox((float)want, (float)got)) return;
                GD.Print($"tuning: FAIL {genre.Id}/{key}: json={want:0.##} but GameInfo.{what}={got:0.##}");
                bad++;
            }
            void CheckBool(string key, string what, bool got)
            {
                if (!t.ContainsKey(key)) return;
                checked_++;
                if (t[key].AsBool() == got) return;
                GD.Print($"tuning: FAIL {genre.Id}/{key}: json={t[key].AsBool()} but GameInfo.{what}={got}");
                bad++;
            }

            CheckBool("enable_weather", "EnableWeather", info.EnableWeather);
            CheckBool("enable_day_night", "EnableDayNightCycle", info.EnableDayNightCycle);
            CheckBool("enable_seasons", "EnableSeasons", info.EnableSeasons);
            CheckBool("enable_temperature", "EnableTemperature", info.EnableTemperature);
            CheckBool("enable_forecast", "EnableWeatherForecast", info.EnableWeatherForecast);
            CheckBool("auto_cycle", "AutoCycleWeather", info.AutoCycleWeather);
            Check("days_per_season", "DaysPerSeason", info.DaysPerSeason);
            Check("ambient_temperature", "AmbientTemperature", info.AmbientTemperature);
            Check("forecast_days", "ForecastDays", info.ForecastDays);
        }

        GD.Print($"tuning: {checked_} genre/key pairs checked across "
               + $"{SkinCatalog.AllGenres.Count} genres");
        GD.Print($"tuning: {(bad == 0 ? "PASS" : $"FAIL ({bad})")}");
        GetTree().Quit(bad == 0 ? 0 : 1);
    }
}
