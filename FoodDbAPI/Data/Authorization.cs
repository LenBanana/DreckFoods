using FoodDbAPI.Models;

namespace FoodDbAPI.Data;

public static class AppPolicies
{
    public const string CanImportData = nameof(CanImportData);
}

public static class AppRoles
{
    // note: we use the enum names here
    public const string User = nameof(AppRole.User);
    public const string DataEditor = nameof(AppRole.DataEditor);
    public const string Admin = nameof(AppRole.Admin);
}