namespace Shared.Contracts.Policy;

public record PolicyResponseDto(string Username, IReadOnlyList<string> Groups, IReadOnlyList<AppDto> Applications);
