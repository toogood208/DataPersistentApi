namespace DataPersistentApi.Models;

public record AuthCallbackResponseDto(
    string Status,
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    int RefreshExpiresIn,
    AuthUserDto User);
