namespace DataPersistentApi.Models;

public record ProfileListItemDto(
    string Id,
    string Name,
    string Gender,
    double GenderProbability,
    int Age,
    string AgeGroup,
    string CountryId,
    string CountryName,
    double CountryProbability,
    DateTime CreatedAt);
