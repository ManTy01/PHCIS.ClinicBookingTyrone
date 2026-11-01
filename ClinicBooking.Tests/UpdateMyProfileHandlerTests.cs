using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Auth;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Entities;
using Moq;

namespace ClinicBooking.Tests;

/// <summary>Profile update returns a new JWT (email claim may change).</summary>
public class UpdateMyProfileHandlerTests
{
    /// <summary>Patient row is updated and CreateToken is called.</summary>
    [Fact]
    public async Task Handle_Patient_UpdatesNameAndPhone()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FirstName = "John",
            LastName = "Doe",
            Email = "patient@phcis.local"
        };

        var db = new Mock<IAppDbContext>();
        db.Setup(d => d.Patients).Returns(new TestDbSet<Patient>([patient]));
        db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.FindByIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser("user-1", "patient@phcis.local", ["Patient"]));
        users.Setup(u => u.UpdateEmailAsync("user-1", "patient@phcis.local", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateToken("user-1", "patient@phcis.local", It.IsAny<IEnumerable<string>>()))
            .Returns("new-token");

        var handler = new UpdateMyProfileCommandHandler(users.Object, db.Object, jwt.Object);
        var result = await handler.Handle(
            new UpdateMyProfileCommand(
                "user-1",
                "patient@phcis.local",
                "Jane",
                "Doe",
                "0820000000",
                new DateOnly(1991, 3, 12)),
            CancellationToken.None);

        Assert.Equal("new-token", result.Token);
        Assert.Equal("Jane", patient.FirstName);
        Assert.Equal("Doe", patient.LastName);
        Assert.Equal("0820000000", patient.Phone);
        Assert.Equal(new DateOnly(1991, 3, 12), patient.DateOfBirth);
        db.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Blank names on a patient profile are rejected before save.</summary>
    [Fact]
    public async Task Handle_PatientMissingName_ThrowsConflict()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FirstName = "John",
            LastName = "Doe",
            Email = "patient@phcis.local"
        };

        var db = new Mock<IAppDbContext>();
        db.Setup(d => d.Patients).Returns(new TestDbSet<Patient>([patient]));

        var users = new Mock<IUserDirectory>();
        users.Setup(u => u.FindByIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser("user-1", "patient@phcis.local", ["Patient"]));
        users.Setup(u => u.UpdateEmailAsync("user-1", "patient@phcis.local", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateMyProfileCommandHandler(users.Object, db.Object, Mock.Of<IJwtTokenService>());

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(
                new UpdateMyProfileCommand("user-1", "patient@phcis.local", " ", "", null, null),
                CancellationToken.None));
    }
}
