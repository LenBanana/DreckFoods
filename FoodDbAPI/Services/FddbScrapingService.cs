using System.Net.Sockets;
using System.Text.RegularExpressions;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using HtmlAgilityPack;

namespace FoodDbAPI.Services;

public class FddbScrapingService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<FddbScrapingService> logger) : IFddbScrapingService
{
    // Limit concurrent outbound requests to fddb.info globally (shared across all service instances).
    private static readonly SemaphoreSlim ConcurrencyLimiter = new(3, 3);
    public async Task<List<FddbFoodImportDto>> FindFoodItemByNameAsync(string foodName,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Searching for food item: {FoodName}", foodName);

        var searchUrl = $"https://fddb.info/db/de/suche/?search={Uri.EscapeDataString(foodName)}";

        try
        {
            var response = await httpClient.GetAsync(searchUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch search results: {StatusCode}", response.StatusCode);
                return [];
            }

            // Check if we have been redirected to a product page directly
            if (response.RequestMessage?.RequestUri?.AbsolutePath.StartsWith("/db/de/lebensmittel/") == true)
            {
                var url = response.RequestMessage.RequestUri.AbsolutePath;
                var relativeUrl = url.Replace("https://fddb.info", string.Empty);
                var foodItem = await ProcessUrlWithRetryAsync(relativeUrl, maxRetries: 5, cancellationToken);
                return foodItem != null ? [foodItem] : [];
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find every div that has "onclick="window.location.href='/db/de/lebensmittel/..."
            var foodItems = doc.DocumentNode.SelectNodes(
                "//div[starts-with(@onclick, \"window.location.href='/db/de/lebensmittel/\")]");

            if (foodItems == null || foodItems.Count == 0)
            {
                logger.LogInformation("No food items found for the given name: {FoodName}", foodName);
                return [];
            }

            var urlRegex = new Regex(@"window\.location\.href='(/db/de/lebensmittel/[^']+)'");
            var maxItemsToScrape = configuration.GetValue("Fddb:MaxItemsToScrape", 100);

            // Extract all matching food page URLs then fetch each in parallel, bounded by
            // ConcurrencyLimiter, so the scrape is fast without hammering the server.
            var foodUrls = foodItems.Take(maxItemsToScrape)
                .Select(item => urlRegex.Match(item.GetAttributeValue("onclick", string.Empty)))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .ToList();

            var scraped = await Task.WhenAll(
                foodUrls.Select(foodUrl => ProcessUrlWithRetryAsync(foodUrl, maxRetries: 5, cancellationToken)));

            var foodDetails = scraped.OfType<FddbFoodImportDto>().ToList();

            logger.LogInformation("Found {Count} food items for '{FoodName}'", foodDetails.Count, foodName);
            return foodDetails;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while searching for food item: {FoodName}", foodName);
            return [];
        }
    }

    private async Task<FddbFoodImportDto?> ProcessUrlWithRetryAsync(
        string url, int maxRetries, CancellationToken cancellationToken)
    {
        var uri = $"https://fddb.info{url}";

        // Acquire a slot before making any network call so we never exceed ConcurrencyLimiter
        // simultaneous connections to fddb.info across all parallel tasks.
        await ConcurrencyLimiter.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await httpClient.GetAsync(
                        uri, HttpCompletionOption.ResponseContentRead, cancellationToken);

                    // Explicit rate-limit handling: honour Retry-After when present.
                    if ((int)response.StatusCode == 429)
                    {
                        if (attempt >= maxRetries) break;
                        var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
                        var rl429Delay = retryAfter + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 2000));
                        logger.LogWarning(
                            "Rate limited by fddb.info for {Url}, waiting {Delay:F0}s (attempt {Attempt}/{MaxRetries})",
                            uri, rl429Delay.TotalSeconds, attempt + 1, maxRetries);
                        await Task.Delay(rl429Delay, cancellationToken);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        if ((int)response.StatusCode >= 500 && attempt < maxRetries)
                        {
                            await Task.Delay(ExponentialDelay(attempt), cancellationToken);
                            continue;
                        }

                        logger.LogWarning("Failed to fetch food item from {Url}: {StatusCode}", uri, response.StatusCode);
                        return null;
                    }

                    var html = await response.Content.ReadAsStringAsync(cancellationToken);
                    return ParseFoodItem(html, uri);
                }
                catch (OperationCanceledException)
                {
                    throw; // Always propagate — covers both user cancellation and HttpClient timeout.
                }
                catch (Exception ex) when (IsTransientException(ex)
                                           && attempt < maxRetries
                                           && !cancellationToken.IsCancellationRequested)
                {
                    var delay = ExponentialDelay(attempt);
                    logger.LogWarning(ex,
                        "Transient error for {Url}, retrying in {Delay:F1}s (attempt {Attempt}/{MaxRetries})",
                        uri, delay.TotalSeconds, attempt + 1, maxRetries);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing food item from {Url}", uri);
                    return null;
                }
            }
        }
        finally
        {
            ConcurrencyLimiter.Release();
        }

        logger.LogWarning("All {MaxRetries} retries exhausted for {Url}", maxRetries, uri);
        return null;
    }

    /// <summary>
    /// Exponential backoff: 2^(attempt+1) seconds, capped at 60 s, with ±25 % random jitter.
    /// </summary>
    private static TimeSpan ExponentialDelay(int attempt)
    {
        var baseSeconds = Math.Min(Math.Pow(2, attempt + 1), 60.0);
        var jitter = 0.75 + Random.Shared.NextDouble() * 0.5; // [0.75, 1.25]
        return TimeSpan.FromSeconds(baseSeconds * jitter);
    }

    private FddbFoodImportDto ParseFoodItem(string html, string uri)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return new FddbFoodImportDto
        {
            Url = uri,
            Name = doc.DocumentNode.SelectSingleNode("//h1[@id='fddb-headline1']")
                ?.InnerText.Trim() ?? "Unknown",
            Description = doc.DocumentNode.SelectSingleNode("//p[@class='lidesc2012']")
                ?.InnerText.Trim() ?? "No description available",
            ImageUrl = doc.DocumentNode.SelectSingleNode("//img[@class='imagesimpleborder']")
                ?.GetAttributeValue("src", string.Empty) ?? string.Empty,
            Brand = doc.DocumentNode.SelectSingleNode(
                    "//span[contains(text(), 'Hersteller:')]/following-sibling::a")
                ?.InnerText.Trim() ?? "Unknown",
            Ean = doc.DocumentNode.SelectSingleNode("//p[contains(., 'EAN:')]")
                ?.InnerText.Trim().Split(["EAN:"], StringSplitOptions.None).LastOrDefault()?.Trim(),
            Tags = doc.DocumentNode.SelectNodes("//h2[@id='fddb-headline2']//a")
                ?.Select(tag => tag.InnerText.Trim())
                .ToList() ?? [],
            Servings = ExtractServingInfo(doc),
            Nutrition = new NutritionInfo
            {
                Kilojoules = ExtractNutritionalValue(doc, "Brennwert"),
                Calories = ExtractNutritionalValue(doc, "Kalorien"),
                Protein = ExtractNutritionalValue(doc, "Protein"),
                Fat = ExtractNutritionalValue(doc, "Fett"),
                Fiber = ExtractNutritionalValue(doc, "Ballaststoffe"),
                Caffeine = ExtractNutritionalValue(doc, "Koffein"),
                Carbohydrates = new CarbohydrateInfo
                {
                    Total = ExtractNutritionalValue(doc, "Kohlenhydrate"),
                    Sugar = ExtractNutritionalValue(doc, "Zucker"),
                    Polyols = ExtractNutritionalValue(doc, "Polyole")
                },
                Minerals = new MineralInfo
                {
                    Salt = ExtractNutritionalValue(doc, "Salz"),
                    Iron = ExtractNutritionalValue(doc, "Eisen"),
                    Zinc = ExtractNutritionalValue(doc, "Zink"),
                    Magnesium = ExtractNutritionalValue(doc, "Magnesium"),
                    Chloride = ExtractNutritionalValue(doc, "Chlorid"),
                    Manganese = ExtractNutritionalValue(doc, "Mangan"),
                    Sulfur = ExtractNutritionalValue(doc, "Schwefel"),
                    Potassium = ExtractNutritionalValue(doc, "Kalium"),
                    Calcium = ExtractNutritionalValue(doc, "Kalzium"),
                    Phosphorus = ExtractNutritionalValue(doc, "Phosphor"),
                    Copper = ExtractNutritionalValue(doc, "Kupfer"),
                    Fluoride = ExtractNutritionalValue(doc, "Fluorid"),
                    Iodine = ExtractNutritionalValue(doc, "Jod")
                }
            }
        };
    }

    // Matches link text like "Portion (150 g)", "Stück (100 g)", "100 g (100 g)", "Packung (200 g)"
    private static readonly Regex ServingLinkRegex = new(
        @"^(.+?)\s*\((\d+(?:[,\.]\d+)?)\s*g\)\s*$",
        RegexOptions.Compiled);

    // Matches generic weight-only names like "100 g", "250 g"
    private static readonly Regex PlainWeightNameRegex = new(
        @"^\d+(?:[,\.]\d+)?\s*g$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static List<ServingInfo> ExtractServingInfo(HtmlDocument doc)
    {
        var linkNodes = doc.DocumentNode.SelectNodes("//div[@class='serva']//a[@class='servb']");
        if (linkNodes == null) return [];

        // Key by weight; prefer named entries (e.g. "Portion") over generic "100 g"
        var seen = new Dictionary<double, ServingInfo>();

        foreach (var node in linkNodes)
        {
            var linkText = node.InnerText.Trim();
            var match = ServingLinkRegex.Match(linkText);
            if (!match.Success) continue;

            var name = match.Groups[1].Value.Trim();
            var weightStr = match.Groups[2].Value.Replace(",", ".");
            if (!double.TryParse(weightStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var weight) || weight <= 0)
                continue;

            if (!seen.TryGetValue(weight, out var existing))
            {
                seen[weight] = new ServingInfo { Name = name, WeightGrams = weight };
            }
            else if (PlainWeightNameRegex.IsMatch(existing.Name) && !PlainWeightNameRegex.IsMatch(name))
            {
                // Replace the generic "100 g" label with a more descriptive name
                seen[weight] = new ServingInfo { Name = name, WeightGrams = weight };
            }
        }

        return [.. seen.Values.OrderBy(s => s.WeightGrams)];
    }


    private static NutritionalValue ExtractNutritionalValue(HtmlDocument doc, string label)
    {
        var raw = doc.DocumentNode.SelectSingleNode(
            $"//*[self::a or self::span][contains(text(), '{label}')]/parent::div/following-sibling::div[1]"
        )?.InnerText?.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("k.a") || raw.StartsWith("k. a"))
            return new NutritionalValue { Value = 0, Unit = string.Empty };

        var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var valueStr = parts.Length > 0 ? parts[0].Replace(",", ".") : "0";
        var unit = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        var value = double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;

        return new NutritionalValue
        {
            Value = value,
            Unit = unit
        };
    }

    private static bool IsTransientException(Exception ex)
    {
        // TaskCanceledException is intentionally excluded — handled separately so that
        // user-initiated cancellation and HttpClient timeouts are never silently retried.
        return ex is HttpRequestException or IOException or SocketException or TimeoutException;
    }
}