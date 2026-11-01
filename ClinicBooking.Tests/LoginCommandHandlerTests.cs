using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Auth;
using Moq;

namespace ClinicBooking.Tests;

/// <summary>PDF: Moq. Handler unit tests — no SQL. JWT service is mocked so we assert CreateToken was called.</summary>
public class LoginCommandHandlerTests
{
    /// <summary>Happy path: Identity says yes → IJwtTokenService mints a token.</summary>
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsJwt()
    {
        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.AuthenticateAsync("patient@phcis.local", "Password123!", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser("user-1", "patient@phcis.local", ["Patient"]));

        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateToken("user-1", "patient@phcis.local", It.IsAny<IEnumerable<string>>()))
            .Returns("test-token");

        var handler = new LoginCommandHandler(users.Object, jwt.Object);

        var result = await handler.Handle(new LoginCommand("patient@phcis.local", "Password123!"), CancellationToken.None);

        Assert.Equal("test-token", result.Token);
        Assert.Equal("patient@phcis.local", result.Email);
        Assert.Contains("Patient", result.Roles);
        jwt.Verify(j => j.CreateToken("user-1", "patient@phcis.local", It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    /// <summary>Bad password: directory returns null → 401, and no JWT is created.</summary>
    [Fact]
    public async Task Handle_BadPassword_ThrowsUnauthorized()
    {
        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.AuthenticateAsync("patient@phcis.local", "wrong", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticatedUser?)null);

        var handler = new LoginCommandHandler(users.Object, Mock.Of<IJwtTokenService>());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new LoginCommand("patient@phcis.local", "wrong"), CancellationToken.None));
    }
}
