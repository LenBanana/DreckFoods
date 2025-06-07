using System.Net.Sockets;
using System.Text.RegularExpressions;
using FoodDbAPI.DTOs;
using FoodDbAPI.Services.Interfaces;
using HtmlAgilityPack;

namespace FoodDbAPI.Services;

public class FddbScrapingService(HttpClient httpClient, ILogger<FddbScrapingService> logger) : IFddbScrapingService
{
    public async Task<List<FddbFoodImportDTO>> FindFoodItemByNameAsync(string foodName, CancellationToken cancellationToken = default)
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
            var foodDetails = new List<FddbFoodImportDTO>();
            var urlRegex = new Regex(@"window\.location\.href='(/db/de/lebensmittel/[^']+)'");
            
            foreach (var item in foodItems)
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

    private async Task<FddbFoodImportDTO?> ProcessUrlWithRetryAsync(
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
                logger.LogWarning(ex, "Transient error processing {Url}, retrying in {Delay}s (attempt {Attempt}/{MaxRetries})", 
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

    private FddbFoodImportDTO ParseFoodItem(string html, string uri)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return new FddbFoodImportDTO
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
            Nutrition = new Models.Fddb.NutritionInfo
            {
                Kilojoules = ExtractNutritionalValue(doc, "Brennwert"),
                Calories = ExtractNutritionalValue(doc, "Kalorien"),
                Protein = ExtractNutritionalValue(doc, "Protein"),
                Fat = ExtractNutritionalValue(doc, "Fett"),
                Fiber = ExtractNutritionalValue(doc, "Ballaststoffe"),
                Carbohydrates = new Models.Fddb.CarbohydrateInfo
                {
                    Total = ExtractNutritionalValue(doc, "Kohlenhydrate"),
                    Sugar = ExtractNutritionalValue(doc, "Zucker"),
                    Polyols = ExtractNutritionalValue(doc, "Polyole")
                },
                Minerals = new Models.Fddb.MineralInfo
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

    private static Models.Fddb.NutritionalValue ExtractNutritionalValue(HtmlDocument doc, string label)
    {
        var raw = doc.DocumentNode.SelectSingleNode(
            $"//*[self::a or self::span][contains(text(), '{label}')]/parent::div/following-sibling::div[1]"
        )?.InnerText?.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("k.a") || raw.StartsWith("k. a"))
            return new Models.Fddb.NutritionalValue { Value = 0, Unit = string.Empty };

        var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var valueStr = parts.Length > 0 ? parts[0].Replace(",", ".") : "0";
        var unit = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        var value = double.TryParse(valueStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;

        return new Models.Fddb.NutritionalValue
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