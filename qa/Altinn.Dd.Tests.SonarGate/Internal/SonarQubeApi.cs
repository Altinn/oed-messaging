using Altinn.Dd.Tests.Shared;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Altinn.Dd.Tests.SonarGate.Internal;

/// <summary>
/// Thin client over the SonarQube web API endpoints we touch. Stateless apart from the wrapped
/// <see cref="HttpClient"/> and the project key used as a query-string parameter.
/// </summary>
internal sealed class SonarQubeApi
{
    public const int TopN = 50;

    // SonarQube's `s=SEVERITY` sort is alphabetical (BLOCKER, CRITICAL, INFO, MAJOR, MINOR),
    // not by impact ranking, so we re-sort client-side using these maps.
    private static readonly Dictionary<string, int> SeverityRank = new(StringComparer.Ordinal)
    {
        ["BLOCKER"] = 0, ["CRITICAL"] = 1, ["MAJOR"] = 2, ["MINOR"] = 3, ["INFO"] = 4,
    };

    private static readonly Dictionary<string, int> HotspotProbabilityRank = new(StringComparer.Ordinal)
    {
        ["HIGH"] = 0, ["MEDIUM"] = 1, ["LOW"] = 2,
    };

    // Headline metrics: counts, coverage/duplication, lines of code, technical debt (sqale_index,
    // minutes), cyclomatic + cognitive complexity, and SonarQube's A–E ratings (1=A best, 5=E worst).
    private static readonly string[] MetricKeys =
    [
        "bugs", "vulnerabilities", "code_smells", "security_hotspots",
        "coverage", "duplicated_lines_density", "ncloc", "sqale_index",
        "complexity", "cognitive_complexity",
        "reliability_rating", "security_rating", "sqale_rating",
    ];

    private readonly HttpClient _client;
    private readonly string _projectKey;

    public SonarQubeApi(HttpClient client, string projectKey)
    {
        _client = client;
        _projectKey = projectKey;
    }

    public static async Task<bool> DockerIsAvailable()
    {
        try
        {
            var (code, _, _) = await ProcessRunner.RunRawAsync(
                "docker", "version --format {{.Server.Version}}", workingDir: null, streamLog: null);
            return code == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetBasicAuth(HttpClient client, string user, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encoded);
    }

    public static async Task RotateAdminPasswordAsync(HttpClient sonarClient, string newPassword)
    {
        // SonarQube 9.7+ requires admin/admin to be rotated before any other admin API call.
        // 1) rotate via Basic auth with the default creds; 2) verify the new creds authenticate.
        // (Some SonarQube versions return 2xx but silently keep the old password if the new one fails a
        // complexity check, so we don't trust the rotation status alone — we follow up with validate.)
        SetBasicAuth(sonarClient, "admin", "admin");
        var changeForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["login"] = "admin",
            ["previousPassword"] = "admin",
            ["password"] = newPassword,
        });
        string changeStatus, changeBody;
        using (var changeResp = await sonarClient.PostAsync("/api/users/change_password", changeForm))
        {
            changeStatus = $"{(int)changeResp.StatusCode} {changeResp.ReasonPhrase}";
            changeBody = await changeResp.Content.ReadAsStringAsync();
            if (!changeResp.IsSuccessStatusCode)
                throw new SonarGateException(
                    $"Failed to rotate admin password: {changeStatus}. Body: {changeBody}");
        }

        SetBasicAuth(sonarClient, "admin", newPassword);
        using var validateResp = await sonarClient.GetAsync("/api/authentication/validate");
        var validateStatus = $"{(int)validateResp.StatusCode} {validateResp.ReasonPhrase}";
        var validateBody = await validateResp.Content.ReadAsStringAsync();
        if (!validateResp.IsSuccessStatusCode || !validateBody.Contains("\"valid\":true"))
            throw new SonarGateException(
                "Admin password rotation appeared to succeed but the new credentials don't authenticate. "
                + $"change_password: {changeStatus} body={changeBody}. "
                + $"validate: {validateStatus} body={validateBody}.");
    }

    public async Task WaitForComputeTaskAsync(TimeSpan timeout)
    {
        // After `sonarscanner end`, SonarQube enqueues a Compute Engine task to process the report.
        // Poll the activity endpoint until the latest task for the project is SUCCESS or FAILED.
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var resp = await _client.GetAsync($"/api/ce/activity?component={_projectKey}&onlyCurrents=true");
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var tasks = doc.RootElement.GetProperty("tasks");
            if (tasks.GetArrayLength() > 0)
            {
                var status = tasks[0].GetProperty("status").GetString();
                if (status == "SUCCESS") return;
                if (status == "FAILED" || status == "CANCELED")
                    throw new SonarGateException($"SonarQube compute task ended with status {status}.");
            }
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        throw new TimeoutException($"SonarQube compute task did not finish within {timeout}.");
    }

    public async Task<QualityGate> FetchQualityGateAsync()
    {
        using var resp = await _client.GetAsync($"/api/qualitygates/project_status?projectKey={_projectKey}");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var ps = doc.RootElement.GetProperty("projectStatus");

        var conditions = new List<QualityGateCondition>();
        if (ps.TryGetProperty("conditions", out var conds))
        {
            foreach (var c in conds.EnumerateArray())
            {
                conditions.Add(new QualityGateCondition(
                    c.GetProperty("metricKey").GetString() ?? "",
                    c.GetProperty("status").GetString() ?? "",
                    c.TryGetProperty("actualValue", out var av) ? av.GetString() : null,
                    c.TryGetProperty("errorThreshold", out var et) ? et.GetString() : null));
            }
        }

        return new QualityGate(ps.GetProperty("status").GetString() ?? "UNKNOWN", conditions);
    }

    public async Task<Dictionary<string, string>> FetchMetricsAsync()
    {
        var keys = string.Join(",", MetricKeys);
        using var resp = await _client.GetAsync(
            $"/api/measures/component?component={_projectKey}&metricKeys={keys}");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var m in doc.RootElement.GetProperty("component").GetProperty("measures").EnumerateArray())
        {
            var metric = m.GetProperty("metric").GetString();
            if (metric is null) continue;
            result[metric] = m.TryGetProperty("value", out var v) ? (v.GetString() ?? "") : "";
        }
        return result;
    }

    public async Task<TopFindings> FetchTopFindingsAsync()
    {
        var bugs = await FetchTopIssuesAsync("BUG");
        var smells = await FetchTopIssuesAsync("CODE_SMELL");
        var hotspots = await FetchTopHotspotsAsync();
        return new TopFindings(bugs, smells, hotspots);
    }

    // Pulls a wider page than TopN because SonarQube's severity sort is alphabetical, so we
    // need a meaningful pool to rank client-side before taking the top N.
    private async Task<IReadOnlyList<TopIssue>> FetchTopIssuesAsync(string type)
    {
        using var resp = await _client.GetAsync(
            $"/api/issues/search?componentKeys={_projectKey}&types={type}&ps=200&resolved=false");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        var issues = new List<TopIssue>();
        foreach (var i in doc.RootElement.GetProperty("issues").EnumerateArray())
        {
            issues.Add(new TopIssue(
                Rule: Json.GetStringOrEmpty(i, "rule"),
                Severity: Json.GetStringOrEmpty(i, "severity"),
                Component: StripProjectPrefix(Json.GetStringOrEmpty(i, "component"), _projectKey),
                Line: Json.GetIntOrNull(i, "line"),
                Message: Json.GetStringOrEmpty(i, "message")));
        }

        return issues
            .OrderBy(x => SeverityRank.GetValueOrDefault(x.Severity, int.MaxValue))
            .ThenBy(x => x.Component, StringComparer.Ordinal)
            .ThenBy(x => x.Line ?? int.MaxValue)
            .Take(TopN)
            .ToList();
    }

    private async Task<IReadOnlyList<TopHotspot>> FetchTopHotspotsAsync()
    {
        using var resp = await _client.GetAsync(
            $"/api/hotspots/search?projectKey={_projectKey}&ps=200&status=TO_REVIEW");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        var hotspots = new List<TopHotspot>();
        if (!doc.RootElement.TryGetProperty("hotspots", out var arr)) return hotspots;
        foreach (var h in arr.EnumerateArray())
        {
            hotspots.Add(new TopHotspot(
                Rule: Json.GetStringOrEmpty(h, "ruleKey"),
                VulnerabilityProbability: Json.GetStringOrEmpty(h, "vulnerabilityProbability"),
                SecurityCategory: Json.GetStringOrEmpty(h, "securityCategory"),
                Component: StripProjectPrefix(Json.GetStringOrEmpty(h, "component"), _projectKey),
                Line: Json.GetIntOrNull(h, "line"),
                Message: Json.GetStringOrEmpty(h, "message")));
        }

        return hotspots
            .OrderBy(x => HotspotProbabilityRank.GetValueOrDefault(x.VulnerabilityProbability, int.MaxValue))
            .ThenBy(x => x.Component, StringComparer.Ordinal)
            .ThenBy(x => x.Line ?? int.MaxValue)
            .Take(TopN)
            .ToList();
    }



    // Components come back as "<projectKey>:Path/To/File.cs" — strip the prefix for readability.
    private static string StripProjectPrefix(string component, string projectKey)
        => component.StartsWith($"{projectKey}:", StringComparison.Ordinal)
            ? component[(projectKey.Length + 1)..]
            : component;
}

// ---- model types ----

internal record QualityGateCondition(string MetricKey, string Status, string? ActualValue, string? ErrorThreshold);

internal record QualityGate(string Status, IReadOnlyList<QualityGateCondition> Conditions)
{
    public string ConditionsSummary
    {
        get
        {
            var nonOk = Conditions.Where(c => c.Status != "OK").ToList();
            if (nonOk.Count == 0) return "none";
            return string.Join("; ",
                nonOk.Select(c => $"{c.MetricKey}={c.ActualValue ?? "?"} ({c.Status})"));
        }
    }
}

internal record TopIssue(string Rule, string Severity, string Component, int? Line, string Message);

internal record TopHotspot(
    string Rule,
    string VulnerabilityProbability,
    string SecurityCategory,
    string Component,
    int? Line,
    string Message);

internal record TopFindings(
    IReadOnlyList<TopIssue> Bugs,
    IReadOnlyList<TopIssue> CodeSmells,
    IReadOnlyList<TopHotspot> Hotspots);
