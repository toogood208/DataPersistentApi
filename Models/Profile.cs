namespace DataPersistentApi.Models;

public class Profile
{
    public string Id { get; set; } = null!;             // UUID v7
    public string Name { get; set; } = null!;           // UNIQUE
    public string Gender { get; set; } = null!;         // male|female
    public double GenderProbability { get; set; }
    public int Age { get; set; }
    public int SampleSize { get; set; }
    public string AgeGroup { get; set; } = null!;       // child/teenager/adult/senior
    public string CountryId { get; set; } = null!;      // ISO2
    public string CountryName { get; set; } = null!;    // full name
    public double CountryProbability { get; set; }
    public DateTime CreatedAt { get; set; }             // UTC
}