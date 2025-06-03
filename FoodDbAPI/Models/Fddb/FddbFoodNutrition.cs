namespace FoodDbAPI.Models.Fddb;

public class FddbFoodNutrition
{
    
    public int Id { get; set; }
    public int FddbFoodId { get; set; }

    // Kilojoules
    public double KilojoulesValue { get; set; }
    public string KilojoulesUnit { get; set; } = string.Empty;

    // Calories
    public double CaloriesValue { get; set; }
    public string CaloriesUnit { get; set; } = string.Empty;

    // Protein
    public double ProteinValue { get; set; }
    public string ProteinUnit { get; set; } = string.Empty;

    // Fat
    public double FatValue { get; set; }
    public string FatUnit { get; set; } = string.Empty;

    // Carbohydrates
    public double CarbohydratesTotalValue { get; set; }
    public string CarbohydratesTotalUnit { get; set; } = string.Empty;
    public double CarbohydratesSugarValue { get; set; }
    public string CarbohydratesSugarUnit { get; set; } = string.Empty;
    public double CarbohydratesPolyolsValue { get; set; }
    public string CarbohydratesPolyolsUnit { get; set; } = string.Empty;

    // Fiber
    public double FiberValue { get; set; }
    public string FiberUnit { get; set; } = string.Empty;

    // Minerals
    public double SaltValue { get; set; }
    public string SaltUnit { get; set; } = string.Empty;
    public double IronValue { get; set; }
    public string IronUnit { get; set; } = string.Empty;
    public double ZincValue { get; set; }
    public string ZincUnit { get; set; } = string.Empty;
    public double MagnesiumValue { get; set; }
    public string MagnesiumUnit { get; set; } = string.Empty;
    public double ChlorideValue { get; set; }
    public string ChlorideUnit { get; set; } = string.Empty;
    public double ManganeseValue { get; set; }
    public string ManganeseUnit { get; set; } = string.Empty;
    public double SulfurValue { get; set; }
    public string SulfurUnit { get; set; } = string.Empty;
    public double PotassiumValue { get; set; }
    public string PotassiumUnit { get; set; } = string.Empty;
    public double CalciumValue { get; set; }
    public string CalciumUnit { get; set; } = string.Empty;
    public double PhosphorusValue { get; set; }
    public string PhosphorusUnit { get; set; } = string.Empty;
    public double CopperValue { get; set; }
    public string CopperUnit { get; set; } = string.Empty;
    public double FluorideValue { get; set; }
    public string FluorideUnit { get; set; } = string.Empty;
    public double IodineValue { get; set; }
    public string IodineUnit { get; set; } = string.Empty;

    // Navigation property
    public virtual FddbFood FddbFood { get; set; } = null!;

    public NutritionInfo ToNutritionInfo()
    {
        return new NutritionInfo
        {
            Kilojoules = new NutritionalValue { Value = KilojoulesValue, Unit = KilojoulesUnit },
            Calories = new NutritionalValue { Value = CaloriesValue, Unit = CaloriesUnit },
            Protein = new NutritionalValue { Value = ProteinValue, Unit = ProteinUnit },
            Fat = new NutritionalValue { Value = FatValue, Unit = FatUnit },
            Carbohydrates = new CarbohydrateInfo
            {
                Total = new NutritionalValue { Value = CarbohydratesTotalValue, Unit = CarbohydratesTotalUnit },
                Sugar = new NutritionalValue { Value = CarbohydratesSugarValue, Unit = CarbohydratesSugarUnit },
                Polyols = new NutritionalValue { Value = CarbohydratesPolyolsValue, Unit = CarbohydratesPolyolsUnit }
            },
            Minerals = new MineralInfo
            {
                Salt = new NutritionalValue { Value = SaltValue, Unit = SaltUnit },
                Iron = new NutritionalValue { Value = IronValue, Unit = IronUnit },
                Zinc = new NutritionalValue { Value = ZincValue, Unit = ZincUnit },
                Magnesium = new NutritionalValue { Value = MagnesiumValue, Unit = MagnesiumUnit },
                Chloride = new NutritionalValue { Value = ChlorideValue, Unit = ChlorideUnit },
                Manganese = new NutritionalValue { Value = ManganeseValue, Unit = ManganeseUnit },
                Sulfur = new NutritionalValue { Value = SulfurValue, Unit = SulfurUnit },
                Potassium = new NutritionalValue { Value = PotassiumValue, Unit = PotassiumUnit },
                Calcium = new NutritionalValue { Value = CalciumValue, Unit = CalciumUnit },
                Phosphorus = new NutritionalValue { Value = PhosphorusValue, Unit = PhosphorusUnit },
                Copper = new NutritionalValue { Value = CopperValue, Unit = CopperUnit },
                Fluoride = new NutritionalValue { Value = FluorideValue, Unit = FluorideUnit },
                Iodine = new NutritionalValue { Value = IodineValue, Unit = IodineUnit }
            },
            Fiber = new NutritionalValue { Value = FiberValue, Unit = FiberUnit }
        };
    }
}