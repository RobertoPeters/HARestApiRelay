var builder = WebApplication.CreateBuilder(args);

var settingsFolder = Path.Combine(builder.Environment.ContentRootPath, "Settings");
var settingsPath = Path.Combine(settingsFolder, "appsettings.json");

if (!Directory.Exists(settingsFolder))
{
    Directory.CreateDirectory(settingsFolder);
}
if (!File.Exists(settingsPath))
{
    File.Copy(Path.Combine(builder.Environment.ContentRootPath, "appsettings.json"), settingsPath);
}

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile(settingsPath, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/", () => "HA Rest API Relay");
app.MapGet("/{token}/{command}", async (
    string token, 
    string command, 
    [Microsoft.AspNetCore.Mvc.FromServices] IConfiguration configuration, 
    [Microsoft.AspNetCore.Mvc.FromServices] IHttpClientFactory httpClientFactory
    ) =>
{
    var expectedToken = configuration.GetValue("Token", "");
    if (expectedToken != token)
    {
        return Results.Unauthorized();
    }

    var endpointMapSettings = configuration.GetSection("Mappings").Get<List<EndpointMapSetting>>();
    var endpointMapSetting = endpointMapSettings?.FirstOrDefault(x => string.Compare(x.Command, command, true) == 0);
    if (endpointMapSetting == null)
    {
        return Results.BadRequest();
    }

    if (!string.IsNullOrWhiteSpace(endpointMapSetting.Method) && !string.IsNullOrWhiteSpace(endpointMapSetting.Url))
    {
        var client = httpClientFactory.CreateClient();
        try
        {
            var request = new HttpRequestMessage(endpointMapSetting.Method.ToLower() switch
            {
                "post" => HttpMethod.Post,
                "get" => HttpMethod.Get,
                "put" => HttpMethod.Put,
                "head" => HttpMethod.Head,
                "options" => HttpMethod.Options,
                "trace" => HttpMethod.Trace,
                "patch" => HttpMethod.Patch,
                "delete" => HttpMethod.Delete,
                _ => HttpMethod.Get
            }, endpointMapSetting.Url);
            if (!string.IsNullOrWhiteSpace(endpointMapSetting.Body))
            {
                request.Content = new StringContent(endpointMapSetting.Body);
                if (!string.IsNullOrWhiteSpace(endpointMapSetting.ContentType))
                {
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(endpointMapSetting.ContentType);
                }
            }
            if (!string.IsNullOrWhiteSpace(endpointMapSetting.Header))
            {
                var parts = endpointMapSetting.Header.Split("%LF%");
                foreach(var part in parts)
                {
                    var pair = part.Split(':');
                    request.Headers.Add(pair[0], pair[1]);
                }
            }

            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            if (!string.IsNullOrEmpty(endpointMapSetting.ShouldMatch) || endpointMapSetting.ReturnResultWithOffset != null || !string.IsNullOrWhiteSpace(endpointMapSetting.ResultProperty))
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrEmpty(endpointMapSetting.ShouldMatch) && string.Compare(responseBody, endpointMapSetting.ShouldMatch) != 0)
                {
                    return Results.StatusCode(endpointMapSetting.NotOkResult);
                }

                if (!string.IsNullOrWhiteSpace(endpointMapSetting.ResultProperty))
                {
                    var jsonElement = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseBody);

                    var parts = endpointMapSetting.ResultProperty.Split('.');
                    var jsonObj = jsonElement;
                    foreach(var part in parts)
                    {
                        jsonObj = jsonObj.GetProperty(part);
                    }

                    var v = (int)double.Parse(jsonObj.ToString(), System.Globalization.CultureInfo.InvariantCulture);
                    if (endpointMapSetting.ReturnResultWithOffset != null)
                    {
                        return Results.StatusCode(v + endpointMapSetting.ReturnResultWithOffset.Value);
                    }
                    return Results.StatusCode(v);
                }

                if (endpointMapSetting.ReturnResultWithOffset != null)
                {
                    return Results.StatusCode(int.Parse(responseBody) + endpointMapSetting.ReturnResultWithOffset.Value);
                }

            }
        }
        catch
        {
            return Results.StatusCode(endpointMapSetting.NotOkResult);
        }
    }

    return Results.StatusCode(endpointMapSetting.OkResult);
});

app.Run();

public class EndpointMapSetting
{
    public string? Command { get; set; }
    public string? Method { get; set; }
    public string? Url { get; set; }
    public string? Header { get; set; }
    public string? ContentType { get; set; }
    public string? Body { get; set; }
    public string? ShouldMatch { get; set; }
    public int? ReturnResultWithOffset { get; set; }
    public string? ResultProperty { get; set; }
    public int OkResult { get; set; }
    public int NotOkResult { get; set; }
}
