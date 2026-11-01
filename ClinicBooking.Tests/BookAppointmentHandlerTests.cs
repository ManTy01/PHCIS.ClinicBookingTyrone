using ClinicBooking.Application.Abstractions;
using ClinicBooking.Application.Appointments;
using ClinicBooking.Application.Common;
using ClinicBooking.Domain.Entities;
using ClinicBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ClinicBooking.Tests;

/// <summary>PDF: Moq + xUnit. Booking rules without SQL — TestDbSet stands in for DbSet.</summary>
public class BookAppointmentHandlerTests
{
    /// <summary>Available future slot → status Booked and a PHC- confirmation code.</summary>
    [Fact]
    public async Task Handle_AvailableFutureSlot_BooksAndMarksSlot()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FirstName = "Sipho",
            LastName = "Dlamini",
            Email = "patient@phcis.local"
        };
        var slot = new TimeSlot
        {
            Id = Guid.NewGuid(),
            ClinicId = Guid.NewGuid(),
            StartUtc = DateTimeOffset.UtcNow.AddDays(2),
            EndUtc = DateTimeOffset.UtcNow.AddDays(2).AddHours(1),
            Status = TimeSlotStatus.Available,
            Capacity = 1
        };

        var db = new Mock<IAppDbContext>();
        db.Setup(d => d.Patients).Returns(CreateDbSet(new List<Patient> { patient }));
        db.Setup(d => d.TimeSlots).Returns(CreateDbSet(new List<TimeSlot> { slot }));
        db.Setup(d => d.Appointments).Returns(CreateDbSet(new List<Appointment>()));
        db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var board = new Mock<ISlotBoardNotifier>();
        var handler = new BookAppointmentCommandHandler(db.Object, board.Object);
        var result = await handler.Handle(new BookAppointmentCommand("user-1", slot.Id), CancellationToken.None);

        Assert.StartsWith("PHC-", result.ConfirmationCode);
        Assert.Equal(TimeSlotStatus.Booked, slot.Status);
        db.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        board.Verify(
            b => b.NotifySlotChangedAsync(slot.ClinicId, slot.Id, TimeSlotStatus.Booked, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>PDF no double bookings: already Booked → ConflictException (HTTP 409).</summary>
    [Fact]
    public async Task Handle_AlreadyBooked_ThrowsConflict()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            FirstName = "Sipho",
            LastName = "Dlamini",
            Email = "patient@phcis.local"
        };
        var slot = new TimeSlot
        {
            Id = Guid.NewGuid(),
            ClinicId = Guid.NewGuid(),
            StartUtc = DateTimeOffset.UtcNow.AddDays(2),
            EndUtc = DateTimeOffset.UtcNow.AddDays(2).AddHours(1),
            Status = TimeSlotStatus.Booked,
            Capacity = 1
        };

        var db = new Mock<IAppDbContext>();
        db.Setup(d => d.Patients).Returns(CreateDbSet(new List<Patient> { patient }));
        db.Setup(d => d.TimeSlots).Returns(CreateDbSet(new List<TimeSlot> { slot }));

        var board = new Mock<ISlotBoardNotifier>();
        var handler = new BookAppointmentCommandHandler(db.Object, board.Object);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new BookAppointmentCommand("user-1", slot.Id), CancellationToken.None));
        board.Verify(
            b => b.NotifySlotChangedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TimeSlotStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>Wrap the in-memory list so FirstOrDefaultAsync works in the handler.</summary>
    private static DbSet<T> CreateDbSet<T>(List<T> data) where T : class
    {
        return new TestDbSet<T>(data);
    }
}
