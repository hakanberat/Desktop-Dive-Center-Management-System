using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class CalendarViewModel(ReservationService reservations, Func<ReservationViewModel> reservationScreen)
    : ScheduleViewModel(true, reservations, reservationScreen);
