namespace DataPersistentApi.Models;

public record AuthSuccessResponseDto(
    string Status,
    AuthTokenPairDto Data);
