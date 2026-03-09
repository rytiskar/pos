using System.Security.Principal;
using ReactApp1.Server.Data.Repositories;
using ReactApp1.Server.Migrations;
using ReactApp1.Server.Models;
using ReactApp1.Server.Models.Models.Base;
using ReactApp1.Server.Models.Models.Domain;
using Sms77.Api.Library;
using Client = Sms77.Api.Client;

namespace ReactApp1.Server.Services
{
    public class ReservationService : IReservationService
    {
        private readonly IReservationRepository _reservationRepository;
        private readonly ILogger<OrderService> _logger;
        private readonly Client _smsClient;

        public ReservationService(IReservationRepository reservationRepository, ILogger<OrderService> logger, string apiKey)
        {
            _reservationRepository = reservationRepository;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API Key cannot be null or empty", nameof(apiKey));
            }

            _smsClient = new Client(apiKey);
        }

        public Task<PaginatedResult<Reservation>> GetAllReservations(int pageSize, int pageNumber, IPrincipal user)
        {
            return _reservationRepository.GetAllReservationsAsync(pageSize, pageNumber, user);
        }

        public Task<ReservationModel?> GetReservationById(int reservationId, IPrincipal user)
        {
            return _reservationRepository.GetReservationByIdAsync(reservationId, user);
        }

        public async Task<Reservation> CreateNewReservation(ReservationModel reservation, int? createdByEmployeeId)
        {
            if (!createdByEmployeeId.HasValue)
            {
                _logger.LogError("Failed to open reservation: invalid or expired access token");
                throw new UnauthorizedAccessException("Operation failed: Invalid or expired access token");
            }

            reservation.CreatedByEmployeeId = createdByEmployeeId.Value;

            var smsParams = new SmsParams
            {
                Text = $"Reservation from {reservation.StartTime} to {reservation.EndTime} created successfully.",
                To = reservation.CustomerPhoneNumber,
                From = "Not A Scam"
            };

            var smsResponse = await _smsClient.Sms(smsParams);

            if (smsResponse == "100")
            {
                _logger.LogInformation($"SMS sent successfully.");
            }
            else
            {
                _logger.LogWarning("Failed to send SMS.");
            }

            return await _reservationRepository.AddReservationAsync(reservation);
        }


        public Task UpdateReservation(ReservationModel reservation)
        {
            return _reservationRepository.UpdateReservationAsync(reservation);
        }

        public async Task DeleteReservation(int reservationId, IPrincipal user)
        {
            var reservation = await _reservationRepository.GetReservationByIdAsync(reservationId, user);

            var smsParams = new SmsParams
            {
                Text = $"Reservation from {reservation.StartTime} to {reservation.EndTime} has been cancelled.",
                To = reservation.CustomerPhoneNumber,
                From = "Not A Scam"
            };

            var smsResponse = await _smsClient.Sms(smsParams);

            if (smsResponse == "100")
            {
                _logger.LogInformation($"SMS sent successfully.");
            }
            else
            {
                _logger.LogWarning("Failed to send SMS.");
            }

            await _reservationRepository.DeleteReservationAsync(reservationId, user);

            return;
        }
    }
}
