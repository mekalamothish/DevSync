using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Trace;

namespace DevSync.Web.testcontrolls;


[ApiController]
[Route("test")]
public class TelemetryTestController : ControllerBase
{
    
    private readonly HttpClient _httpClient;

    public TelemetryTestController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    private static readonly ActivitySource ActivitySource =
        new("Devsync.Api");
    
    [HttpGet("external-api")]
    [Obsolete("Obsolete")]
    public async Task<IActionResult> GetExternalApi()
    {
        using var activity = ActivitySource.StartActivity("FetchExternalPost");

        activity?.SetTag("external.api", "JSONPlaceholder");
        activity?.SetTag("operation.type", "FetchPost");

        try
        {
            var response = await _httpClient.GetAsync(
                "https://jsonplaceholder.typicode.com/posts/1");

            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync();

            return Content(data, "application/json");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.RecordException(ex);

            throw;
        }
    }
    
}