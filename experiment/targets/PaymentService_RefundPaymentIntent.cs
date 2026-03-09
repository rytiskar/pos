using ReactApp1.Server.Data.Repositories;
using ReactApp1.Server.Models;
using ReactApp1.Server.Models.Models.Base;
using ReactApp1.Server.Models.Models.Domain;
using Stripe;

namespace ReactApp1.Server.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly PaymentIntentService _paymentIntentService;
        private readonly RefundService _refundService;

        public PaymentService(IPaymentRepository giftCardRepository, PaymentIntentService paymentIntentService, RefundService refundService)
        {
            _paymentRepository = giftCardRepository;
            _paymentIntentService = paymentIntentService;
            _refundService = refundService;
        }

        public Task<PaginatedResult<Payment>> GetAllPayments(int pageSize, int pageNumber)
        {
            return _paymentRepository.GetAllPaymentsAsync(pageSize, pageNumber);
        }

        public Task<PaymentModel?> GetPaymentById(int paymentId)
        {
            return _paymentRepository.GetPaymentByIdAsync(paymentId);
        }
        public Task<List<PaymentModel?>> GetPaymentsByOrderId(int orderId)
        {
            return _paymentRepository.GetPaymentsByOrderIdAsync(orderId);
        }

        public Task CreateNewPayment(PaymentModel payment)
        {
            return _paymentRepository.AddPaymentAsync(payment);
        }

        public Task UpdatePayment(PaymentModel payment)
        {
            return _paymentRepository.UpdatePaymentAsync(payment);
        }

        public Task DeletePayment(int paymentId)
        {
            return _paymentRepository.DeletePaymentAsync(paymentId);
        }
        public async Task<PaymentIntent> CreatePaymentIntent(decimal amount, string currency)
        {

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // convert to cents
                Currency = currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
            };

            var paymentIntent = await _paymentIntentService.CreateAsync(options);

            if (paymentIntent.Status != "payment_failed")
            {
                return paymentIntent;
            }
            else
            {
                throw new Exception();
            }
        }
        public async Task RefundPaymentIntent(string paymentIntentId)
        {
            var refundOptions = new RefundCreateOptions
            {
                PaymentIntent = paymentIntentId
            };

            await _refundService.CreateAsync(refundOptions);
        }
    }
}
