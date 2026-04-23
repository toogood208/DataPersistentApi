namespace DataPersistentApi.Services;

public class QueryOptions
{
    public string? Gender { get; set; }
    public string? AgeGroup { get; set; }
    public string? CountryId { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public double? MinGenderProbability { get; set; }
    public double? MinCountryProbability { get; set; }
    public string? SortBy { get; set; }      // age | created_at | gender_probability
    public string? Order { get; set; }       // asc | desc
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 10;
}
