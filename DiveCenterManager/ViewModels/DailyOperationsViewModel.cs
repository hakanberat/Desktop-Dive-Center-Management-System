using DiveCenterManager.Services;

namespace DiveCenterManager.ViewModels;

public sealed class DailyOperationsViewModel(ReservationService reservations, Func<ReservationViewModel> reservationScreen)
    : ScheduleViewModel(false, reservations, reservationScreen);
