using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusBackend.Services;

public class WeatherService
{
    private const double DefaultLatitude = -20.82;
    private const double DefaultLongitude = -49.378889;
    private const string DefaultTimezone = "America/Sao_Paulo";

    private readonly IHttpClientFactory _httpClientFactory;

    public WeatherService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<CurrentWeather> GetCurrentWeatherAsync()
    {
        var forecast = await GetForecastAsync();
        var current = forecast.Current ?? new OpenMeteoCurrent();

        return new CurrentWeather
        {
            Location = "Sao Jose do Rio Preto",
            Time = current.Time ?? "",
            Temperature = current.Temperature,
            ApparentTemperature = current.ApparentTemperature,
            Humidity = current.Humidity,
            Precipitation = current.Precipitation,
            WeatherCode = current.WeatherCode,
            Condition = DescribeWeatherCode(current.WeatherCode),
            CloudCover = current.CloudCover,
            WindSpeed = current.WindSpeed,
            WindGusts = current.WindGusts
        };
    }

    public async Task<DailyWeather> GetDailyWeatherAsync()
    {
        var forecast = await GetForecastAsync();
        var daily = forecast.Daily ?? new OpenMeteoDaily();

        return new DailyWeather
        {
            Location = "Sao Jose do Rio Preto",
            Date = GetAt(daily.Time, 0),
            MaxTemperature = GetAt(daily.MaxTemperature, 0),
            MinTemperature = GetAt(daily.MinTemperature, 0),
            PrecipitationSum = GetAt(daily.PrecipitationSum, 0),
            PrecipitationProbabilityMax = GetAt(daily.PrecipitationProbabilityMax, 0),
            UvIndexMax = GetAt(daily.UvIndexMax, 0),
            UvLevel = DescribeUvIndex(GetAt(daily.UvIndexMax, 0))
        };
    }

    public async Task<WeatherReport> GetWeatherReportAsync()
    {
        var forecast = await GetForecastAsync();
        var current = forecast.Current ?? new OpenMeteoCurrent();
        var daily = forecast.Daily ?? new OpenMeteoDaily();

        var report = new WeatherReport
        {
            Location = "Sao Jose do Rio Preto",
            GeneratedAt = DateTime.Now,
            Current = new CurrentWeather
            {
                Location = "Sao Jose do Rio Preto",
                Time = current.Time ?? "",
                Temperature = current.Temperature,
                ApparentTemperature = current.ApparentTemperature,
                Humidity = current.Humidity,
                Precipitation = current.Precipitation,
                WeatherCode = current.WeatherCode,
                Condition = DescribeWeatherCode(current.WeatherCode),
                CloudCover = current.CloudCover,
                WindSpeed = current.WindSpeed,
                WindGusts = current.WindGusts
            },
            Daily = new DailyWeather
            {
                Location = "Sao Jose do Rio Preto",
                Date = GetAt(daily.Time, 0),
                MaxTemperature = GetAt(daily.MaxTemperature, 0),
                MinTemperature = GetAt(daily.MinTemperature, 0),
                PrecipitationSum = GetAt(daily.PrecipitationSum, 0),
                PrecipitationProbabilityMax = GetAt(daily.PrecipitationProbabilityMax, 0),
                UvIndexMax = GetAt(daily.UvIndexMax, 0),
                UvLevel = DescribeUvIndex(GetAt(daily.UvIndexMax, 0))
            }
        };

        report.Recommendation = BuildRecommendation(report);
        report.Summary = BuildSummary(report);
        return report;
    }

    public async Task<string> GetWeatherReportTextAsync()
    {
        var report = await GetWeatherReportAsync();

        return
            $"Relatorio do tempo - {report.Location}\n\n" +
            "Agora:\n" +
            $"- Temperatura: {Round(report.Current.Temperature)}°C\n" +
            $"- Sensacao termica: {Round(report.Current.ApparentTemperature)}°C\n" +
            $"- Umidade: {Round(report.Current.Humidity)}%\n" +
            $"- Vento: {Round(report.Current.WindSpeed)} km/h\n" +
            $"- Condicao: {report.Current.Condition}\n\n" +
            "Hoje:\n" +
            $"- Maxima: {Round(report.Daily.MaxTemperature)}°C\n" +
            $"- Minima: {Round(report.Daily.MinTemperature)}°C\n" +
            $"- Chance de chuva: {Round(report.Daily.PrecipitationProbabilityMax)}%\n" +
            $"- Precipitacao prevista: {Round(report.Daily.PrecipitationSum, 1)} mm\n" +
            $"- Indice UV: {report.Daily.UvLevel}\n\n" +
            $"Recomendacao:\n{report.Recommendation}";
    }

    private async Task<OpenMeteoForecast> GetForecastAsync()
    {
        var latitude = Environment.GetEnvironmentVariable("WEATHER_LATITUDE") ?? DefaultLatitude.ToString(CultureInfo.InvariantCulture);
        var longitude = Environment.GetEnvironmentVariable("WEATHER_LONGITUDE") ?? DefaultLongitude.ToString(CultureInfo.InvariantCulture);
        var timezone = Environment.GetEnvironmentVariable("WEATHER_TIMEZONE") ?? DefaultTimezone;

        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={Uri.EscapeDataString(latitude)}" +
            $"&longitude={Uri.EscapeDataString(longitude)}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,cloud_cover,wind_speed_10m,wind_gusts_10m" +
            "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,uv_index_max" +
            $"&timezone={Uri.EscapeDataString(timezone)}";

        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OpenMeteoForecast>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new OpenMeteoForecast();
    }

    private static string BuildSummary(WeatherReport report)
    {
        return $"{Round(report.Current.Temperature)}°C agora, maxima prevista de {Round(report.Daily.MaxTemperature)}°C, " +
            $"chance de chuva de {Round(report.Daily.PrecipitationProbabilityMax)}% e UV {report.Daily.UvLevel}.";
    }

    private static string BuildRecommendation(WeatherReport report)
    {
        var recommendations = new List<string>();

        if (report.Daily.MaxTemperature >= 32 || report.Current.ApparentTemperature >= 32)
            recommendations.Add("leve agua e evite sol forte no comeco da tarde");

        if (report.Daily.PrecipitationProbabilityMax >= 50 || report.Daily.PrecipitationSum >= 3)
            recommendations.Add("leve guarda-chuva ou planeje deslocamentos com margem");
        else if (report.Daily.PrecipitationProbabilityMax >= 30)
            recommendations.Add("fique atento a chuva isolada ao longo do dia");

        if (report.Daily.UvIndexMax >= 6)
            recommendations.Add("use protetor solar se for sair");

        if (report.Current.WindGusts >= 45)
            recommendations.Add("atenção a rajadas de vento");

        return recommendations.Count == 0
            ? "Clima sem alertas fortes agora. Pode seguir o dia normalmente, mantendo agua por perto."
            : ToSentence(recommendations) + ".";
    }

    private static string ToSentence(IReadOnlyList<string> items)
    {
        if (items.Count == 1)
            return FirstUpper(items[0]);

        return FirstUpper(string.Join(", ", items.Take(items.Count - 1)) + " e " + items[^1]);
    }

    private static string FirstUpper(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? text
            : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string DescribeUvIndex(double value)
    {
        return value switch
        {
            < 3 => "baixo",
            < 6 => "moderado",
            < 8 => "alto",
            < 11 => "muito alto",
            _ => "extremo"
        };
    }

    private static string DescribeWeatherCode(int code)
    {
        return code switch
        {
            0 => "ceu limpo",
            1 => "principalmente limpo",
            2 => "parcialmente nublado",
            3 => "nublado",
            45 or 48 => "neblina",
            51 or 53 or 55 => "garoa",
            56 or 57 => "garoa congelante",
            61 or 63 or 65 => "chuva",
            66 or 67 => "chuva congelante",
            71 or 73 or 75 => "neve",
            77 => "graos de neve",
            80 or 81 or 82 => "pancadas de chuva",
            85 or 86 => "pancadas de neve",
            95 => "trovoadas",
            96 or 99 => "trovoadas com granizo",
            _ => "condicao nao identificada"
        };
    }

    private static T GetAt<T>(IReadOnlyList<T>? values, int index)
    {
        return values is not null && values.Count > index ? values[index] : default!;
    }

    private static string Round(double value, int decimals = 0)
    {
        return Math.Round(value, decimals).ToString(decimals == 0 ? "0" : "0.0", CultureInfo.InvariantCulture);
    }
}

public class WeatherReport
{
    public string Location { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public CurrentWeather Current { get; set; } = new();
    public DailyWeather Daily { get; set; } = new();
    public string Summary { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

public class CurrentWeather
{
    public string Location { get; set; } = "";
    public string Time { get; set; } = "";
    public double Temperature { get; set; }
    public double ApparentTemperature { get; set; }
    public double Humidity { get; set; }
    public double Precipitation { get; set; }
    public int WeatherCode { get; set; }
    public string Condition { get; set; } = "";
    public double CloudCover { get; set; }
    public double WindSpeed { get; set; }
    public double WindGusts { get; set; }
}

public class DailyWeather
{
    public string Location { get; set; } = "";
    public string Date { get; set; } = "";
    public double MaxTemperature { get; set; }
    public double MinTemperature { get; set; }
    public double PrecipitationSum { get; set; }
    public double PrecipitationProbabilityMax { get; set; }
    public double UvIndexMax { get; set; }
    public string UvLevel { get; set; } = "";
}

internal class OpenMeteoForecast
{
    [JsonPropertyName("current")]
    public OpenMeteoCurrent? Current { get; set; }

    [JsonPropertyName("daily")]
    public OpenMeteoDaily? Daily { get; set; }
}

internal class OpenMeteoCurrent
{
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("temperature_2m")]
    public double Temperature { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public double Humidity { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double ApparentTemperature { get; set; }

    [JsonPropertyName("precipitation")]
    public double Precipitation { get; set; }

    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }

    [JsonPropertyName("cloud_cover")]
    public double CloudCover { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeed { get; set; }

    [JsonPropertyName("wind_gusts_10m")]
    public double WindGusts { get; set; }
}

internal class OpenMeteoDaily
{
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = new();

    [JsonPropertyName("temperature_2m_max")]
    public List<double> MaxTemperature { get; set; } = new();

    [JsonPropertyName("temperature_2m_min")]
    public List<double> MinTemperature { get; set; } = new();

    [JsonPropertyName("precipitation_sum")]
    public List<double> PrecipitationSum { get; set; } = new();

    [JsonPropertyName("precipitation_probability_max")]
    public List<double> PrecipitationProbabilityMax { get; set; } = new();

    [JsonPropertyName("uv_index_max")]
    public List<double> UvIndexMax { get; set; } = new();
}
