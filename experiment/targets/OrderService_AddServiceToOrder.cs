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

        public async Task AddServiceToOrder(FullOrderServiceModel fullOrderServiceModel, int? userId, IPrincipal user)
        {
            if (!userId.HasValue)
            {
                _logger.LogError("Failed to add item to order: invalid or expired access token");
                throw new UnauthorizedAccessException("Operation failed: Invalid or expired access token");
            }

            var existingFullOrderService = await _fullOrderServiceRepository.GetFullOrderServiceAsync(fullOrderServiceModel.OrderId, fullOrderServiceModel.ServiceId);

            if (existingFullOrderService != null)
            {
                await _fullOrderServiceRepository.UpdateServiceInOrderCountAsync(fullOrderServiceModel);
            }
            else
            {
                await _fullOrderServiceRepository.AddServiceToOrderAsync(fullOrderServiceModel, userId.Value);

                //refetch to get back the id
                existingFullOrderService = await _fullOrderServiceRepository.GetFullOrderServiceAsync(fullOrderServiceModel.OrderId, fullOrderServiceModel.ServiceId);

                //Save tax historic data
                var taxes = await _taxService.GetServiceTaxes(fullOrderServiceModel.ServiceId);

                foreach (var tax in taxes)
                {
                    var fullOrderTax = new FullOrderServiceTaxModel
                    {
                        FullOrderServiceId = existingFullOrderService.FullOrderServiceId,
                        Percentage = tax.Percentage,
                        Description = tax.Description
                    };

                    await _fullOrderServiceTaxRepository.AddItemToFullOrderServiceTaxAsync(fullOrderTax);
                }
            }
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

        public Task RefundOrder(int orderId, IPrincipal user)
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
