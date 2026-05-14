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
                var foodItem = await ProcessUrlWithRetryAsync(relativeUrl, maxRetries: 3, cancellationToken);
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

            // Extract every URL from the onclick attribute and use the ParseFoodItem method to get details
            var foodDetails = new List<FddbFoodImportDto>();
            var urlRegex = new Regex(@"window\.location\.href='(/db/de/lebensmittel/[^']+)'");
            
            // Try to get amount to scrape from configuration, default to 100 if not set
            var maxItemsToScrape = configuration.GetValue("Fddb:MaxItemsToScrape", 100);

            foreach (var item in foodItems.Take(maxItemsToScrape))
            {
                var onclick = item.GetAttributeValue("onclick", string.Empty);
                var urlMatch = urlRegex.Match(onclick);

                if (!urlMatch.Success) continue;

                var foodUrl = urlMatch.Groups[1].Value;
                var foodItem = await ProcessUrlWithRetryAsync(foodUrl, maxRetries: 3, cancellationToken);

                if (foodItem == null) continue;

                foodDetails.Add(foodItem);
                logger.LogDebug("Found food item: {FoodName} ({Url})", foodItem.Name, foodItem.Url);
            }

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
        var attempts = 0;
        var uri = $"https://fddb.info{url}";

        while (attempts <= maxRetries)
        {
            try
            {
                var response = await httpClient.GetAsync(
                    uri, HttpCompletionOption.ResponseContentRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode >= 500 && attempts < maxRetries)
                    {
                        attempts++;
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts));
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    logger.LogWarning("Failed to fetch food item from {Url}: {StatusCode}", uri, response.StatusCode);
                    return null;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                return ParseFoodItem(html, uri);
            }
            catch (Exception ex) when (IsTransientException(ex) && attempts < maxRetries)
            {
                attempts++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts));
                logger.LogWarning(ex,
                    "Transient error processing {Url}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})",
                    uri, delay.TotalSeconds, attempts, maxRetries);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing food item from {Url}", uri);
                return null;
            }
        }

        return null;
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

    private static List<ServingInfo> ExtractServingInfo(HtmlDocument doc)
    {
        var servings = new List<ServingInfo>();
        var servingNodes = doc.DocumentNode.SelectNodes("//div[@class='serva']");

        if (servingNodes == null) return servings;

        foreach (var servingNode in servingNodes)
        {
            try
            {
                // Extract serving name and unit from the title attribute of the link
                var titleNode = servingNode.SelectSingleNode(".//a[@class='servb']");
                var title = titleNode?.GetAttributeValue("title", string.Empty) ?? string.Empty;

                // Extract serving amount and unit from the link text (e.g., "100 g (100 ml)" or "Glas (250 ml)")
                var linkText = titleNode?.InnerText?.Trim() ?? string.Empty;

                // Find the calories and kilojoules from the "less" div (e.g., "Brennwert: 8 kJ, Kalorien: 2 kcal")
                var nutritionText =
                    servingNode.SelectSingleNode(".//div[starts-with(@id, 'less')]//p")?.InnerText?.Trim() ??
                    string.Empty;

                var serving = ParseServingData(title, linkText, nutritionText);
                if (serving != null)
                {
                    servings.Add(serving);
                }
            }
            catch
            {
                // Skip malformed serving entries
                continue;
            }
        }

        return servings;
    }

    private static ServingInfo? ParseServingData(string title, string linkText, string nutritionText)
    {
        if (string.IsNullOrWhiteSpace(linkText) || string.IsNullOrWhiteSpace(nutritionText))
            return null;

        // Extract serving name from title (e.g., "100 g Monster Energy Ultra..." -> "Monster Energy Ultra...")
        var servingName = title.Split(' ').Skip(2).FirstOrDefault() ?? "Unknown";
        if (title.Contains("Monster Energy") || title.Contains("Glas") || title.Contains("Dose"))
        {
            if (title.StartsWith("100 g "))
                servingName = "100g Standard";
            else if (title.Contains("Glas"))
                servingName = "Glas";
            else if (title.Contains("Dose"))
                servingName = "Dose";
        }

        // Parse amount and unit from link text
        var amount = 0.0;
        var unit = "";

        // Match patterns like "100 g (100 ml)", "Glas (250 ml)", "Dose (500 ml)"
        var match = Regex.Match(linkText,
            @"(?:(\d+(?:,\d+)?)\s*([a-zA-Z]+)|\w+)\s*\((\d+(?:,\d+)?)\s*([a-zA-Z]+)\)");
        if (match.Success)
        {
            var amountStr = match.Groups[3].Value.Replace(",", ".");
            if (double.TryParse(amountStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out amount))
            {
                unit = match.Groups[4].Value;
            }
        }

        // Parse nutrition values (e.g., "Brennwert: 8 kJ, Kalorien: 2 kcal")
        var kilojoules = new NutritionalValue { Value = 0, Unit = "kJ" };
        var calories = new NutritionalValue { Value = 0, Unit = "kcal" };

        var kjMatch = Regex.Match(nutritionText, @"Brennwert:\s*(\d+(?:,\d+)?)\s*kJ");
        if (kjMatch.Success && double.TryParse(kjMatch.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture,
                out var kjValue))
        {
            kilojoules.Value = kjValue;
        }

        var kcalMatch = Regex.Match(nutritionText, @"Kalorien:\s*(\d+(?:,\d+)?)\s*kcal");
        if (kcalMatch.Success && double.TryParse(kcalMatch.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture,
                out var kcalValue))
        {
            calories.Value = kcalValue;
        }

        return new ServingInfo
        {
            Name = servingName,
            Amount = amount,
            Unit = unit,
            Kilojoules = kilojoules,
            Calories = calories
        };
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
        return ex is HttpRequestException or IOException or SocketException
            or TaskCanceledException or TimeoutException;
    }
}