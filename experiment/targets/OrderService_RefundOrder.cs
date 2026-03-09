using System.Security.Principal;
using ReactApp1.Server.Data.Repositories;
using ReactApp1.Server.Exceptions.GiftCardExceptions;
using ReactApp1.Server.Exceptions.ItemExceptions;
using ReactApp1.Server.Exceptions.OrderExceptions;
using ReactApp1.Server.Exceptions.StorageExceptions;
using ReactApp1.Server.Models;
using ReactApp1.Server.Models.Enums;
using ReactApp1.Server.Models.Models.Base;
using ReactApp1.Server.Models.Models.Domain;
using Stripe;

namespace ReactApp1.Server.Services
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IServiceRepository _serviceRepository;
        private readonly IFullOrderRepository _fullOrderRepository;
        private readonly IFullOrderServiceRepository _fullOrderServiceRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IGiftCardRepository _giftcardRepository;
        private readonly ILogger<OrderService> _logger;
        private readonly IPaymentService _paymentService;
        private readonly IDiscountRepository _discountRepository;
        private readonly ITaxService _taxService;
        private readonly IFullOrderTaxRepository _fullOrderTaxRepository;
        private readonly IFullOrderServiceTaxRepository _fullOrderServiceTaxRepository;

        public OrderService(IOrderRepository orderRepository, IItemRepository itemRepository, IServiceRepository serviceRepository,
            IFullOrderRepository fullOrderRepository, IFullOrderServiceRepository fullOrderServiceRepository, IEmployeeRepository employeeRepository,
            ILogger<OrderService> logger, IPaymentRepository paymentRepository, IGiftCardRepository giftcardRepository, IPaymentService paymentService,
            IDiscountRepository discountRepository, ITaxService taxService, IFullOrderTaxRepository fullOrderTaxRepository,
            IFullOrderServiceTaxRepository fullOrderServiceTaxRepository)
        {
            _orderRepository = orderRepository;
            _itemRepository = itemRepository;
            _serviceRepository = serviceRepository;
            _fullOrderRepository = fullOrderRepository;
            _fullOrderServiceRepository = fullOrderServiceRepository;
            _employeeRepository = employeeRepository;
            _paymentRepository = paymentRepository;
            _giftcardRepository = giftcardRepository;
            _logger = logger;
            _paymentService = paymentService;
            _discountRepository = discountRepository;
            _taxService = taxService;
            _fullOrderTaxRepository = fullOrderTaxRepository;
            _fullOrderServiceTaxRepository = fullOrderServiceTaxRepository;
        }

        // === Target method ===

        public async Task RefundOrder(int orderId, IPrincipal user)
        {
            var order = await GetOrderIfExistsAndStatusIs(orderId, (int)OrderStatusEnum.Completed, user, "RefundOrder");

            if (order == null || order.Refunded)
                return;

            var orderPayments = await GetOrderPayments(orderId);

            foreach (var payment in orderPayments)
            {
                if(payment.Type == (int)PaymentTypeEnum.Cash)
                {
                    // idk?
                }
                else if(payment.Type == (int)PaymentTypeEnum.GiftCard)
                {
                    GiftCardModel? giftcard = await _giftcardRepository.GetGiftCardByIdAsync(payment.GiftCardId);

                    if(giftcard == null)
                    {
                        _logger.LogError($"Could not refund payment {payment.PaymentId}. Giftcard not found.");
                        continue;
                    }

                    giftcard.Amount += payment.Value;

                    //Add 1 extra week to spend the money
                    if(giftcard.ExpirationDate < DateTime.Now)
                    {
                        giftcard.ExpirationDate = DateTime.Now.AddDays(7);
                    }
                    else
                    {
                        giftcard.ExpirationDate = giftcard.ExpirationDate.AddDays(7);
                    }

                    await _giftcardRepository.UpdateGiftCardAsync(giftcard);
                }
                else if(payment.Type == (int)PaymentTypeEnum.Card)
                {
                    await _paymentService.RefundPaymentIntent(payment.StripePaymentId);
                }
            }

            order.Refunded = true;
            await _orderRepository.UpdateOrderAsync(order);
        }

        // === Private helpers ===

        private async Task<OrderModel?> GetOrderIfExistsAndStatusIs(int orderId, int orderStatus, IPrincipal user, string? operation = null)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId, user);
            if (order == null)
            {
                _logger.LogError($"Operation '{operation}' failed: Order {orderId} not found");
                throw new OrderNotFoundException(orderId);
            }

            if (order.Status != orderStatus)
            {
                _logger.LogError($"Operation '{operation}' failed: Order status is {order.Status}");
                throw new OrderStatusConflictException(order.Status.ToString());
            }

            return order;
        }

        private async Task<List<PaymentModel>> GetOrderPayments(int orderId)
        {
            var payments = await _paymentRepository.GetPaymentsByOrderIdAsync(orderId);
            return payments;
        }

        // === Stubs for other IOrderService methods ===

        public Task<OrderItemsPayments> OpenOrder(int? createdByEmployeeId, int? establishmentId)
            => throw new NotImplementedException();

        public Task<PaginatedResult<OrderModel>> GetAllOrders(int pageNumber, int pageSize, IPrincipal user)
            => throw new NotImplementedException();

        public Task<OrderItemsPayments> GetOrderById(int orderId, IPrincipal user)
            => throw new NotImplementedException();

        public Task AddItemToOrder(FullOrderModel fullOrder, int? userId, IPrincipal user)
            => throw new NotImplementedException();

        public Task AddServiceToOrder(FullOrderServiceModel fullOrderServiceModel, int? userId, IPrincipal user)
            => throw new NotImplementedException();

        public Task UpdateOrder(OrderModel order, IPrincipal user)
            => throw new NotImplementedException();

        public Task RemoveItemFromOrder(FullOrderModel fullOrder, IPrincipal user)
            => throw new NotImplementedException();

        public Task RemoveServiceFromOrder(FullOrderServiceModel fullOrderService, IPrincipal user)
            => throw new NotImplementedException();

        public Task CloseOrder(int orderId, IPrincipal user)
            => throw new NotImplementedException();

        public Task CancelOrder(int orderId, IPrincipal user)
            => throw new NotImplementedException();

        public Task TipOrder(TipModel tip, IPrincipal user)
            => throw new NotImplementedException();

        public Task DiscountOrder(DiscountModel discount, IPrincipal user)
            => throw new NotImplementedException();

        public Task PayOrder(PaymentModel payment, IPrincipal user)
            => throw new NotImplementedException();

        public Task<byte[]> DownloadReceipt(int orderId, IPrincipal user)
            => throw new NotImplementedException();
    }
}
