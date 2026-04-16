namespace DataPersistentApi.Models;

public class Profile
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Gender { get; set; } = null!;
    public double GenderProbability { get; set; }
    public int SampleSize { get; set; }
    public int Age { get; set; }
    public string AgeGroup { get; set; } = null!;
    public string CountryId { get; set; } = null!;
    public double CountryProbability { get; set; }
    public DateTime CreatedAt { get; set; }
}
