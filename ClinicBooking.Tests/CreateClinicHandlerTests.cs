using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Clinics;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ClinicBooking.Tests;

/// <summary>PDF: Moq + xUnit. Admin create-clinic rules without SQL.</summary>
public class CreateClinicHandlerTests
{
    /// <summary>New name → clinic row plus weekday hour slots (same grid as the seeder).</summary>
    [Fact]
    public async Task Handle_NewName_AddsClinicAndHourSlots()
    {
        var clinics = new List<Clinic>();
        var slots = new List<TimeSlot>();
        var db = new Mock<IAppDbContext>();
        db.Setup(d => d.Clinics).Returns(CreateDbSet(clinics));
        db.Setup(d => d.TimeSlots).Returns(CreateDbSet(slots));
        db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateClinicCommandHandler(db.Object);
        var result = await handler.Handle(
            new CreateClinicCommand(" Durban Clinic ", "18th Avenue", " 011 000 1003 "),
            CancellationToken.None);

        Assert.Equal("Durban Clinic", result.Name);
        Assert.Equal("18th Avenue", result.Address);
        Assert.Equal("011 000 1003", result.Phone);
        Assert.Single(clinics);
        Assert.Equal(ClinicHourSlots.DefaultTimeZoneId, clinics[0].TimeZoneId);
        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(clinics[0].Id, s.ClinicId));
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromHours(1), s.EndUtc - s.StartUtc));
        db.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Duplicate name → ConflictException (HTTP 409).</summary>
    [Fact]
    public async Task Handle_DuplicateName_ThrowsConflict()
    {
        var existing = new Clinic
        {
            Id = Guid.NewGuid(),
            Name = "Jhb Community Clinic",
            Address = "Jhb, Jhb",
            TimeZoneId = ClinicHourSlots.DefaultTimeZoneId
        };
        var db = new Mock<IAppDbContext>();
        db.Setup(d => d.Clinics).Returns(CreateDbSet(new List<Clinic> { existing }));

        var handler = new CreateClinicCommandHandler(db.Object);
        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new CreateClinicCommand("Jhb Community Clinic", "Somewhere else", null),
            CancellationToken.None));
    }

    private static DbSet<T> CreateDbSet<T>(List<T> data) where T : class => new TestDbSet<T>(data);
}
