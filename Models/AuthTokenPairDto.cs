namespace DataPersistentApi.Models;

public record AuthTokenPairDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    int RefreshExpiresIn,
    AuthUserDto User);
