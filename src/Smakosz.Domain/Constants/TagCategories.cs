namespace Smakosz.Domain.Constants;

public static class TagCategories
{
    public const string DishCategory = "dish_category";
    public const string Attribute = "attribute";
    public const string Diet = "diet";
    public const string Dietary = "dietary";
    public const string Cuisine = "cuisine";
    public const string Feature = "feature";
    public const string Mood = "mood";
    public const string Occasion = "occasion";
    public const string Spice = "spice";
}

public static class SpiceLevels
{
    public const string Mild = "Łagodne";
    public const string Medium = "Średnio ostre";
    public const string Hot = "Ostre";
    public const string VeryHot = "Bardzo ostre";

    public static readonly string[] All = [Mild, Medium, Hot, VeryHot];
}
