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

        public async Task AddItemToOrder(FullOrderModel fullOrder, int? userId, IPrincipal user)
        {
            if (!userId.HasValue)
            {
                _logger.LogError("Failed to add item to order: invalid or expired access token");
                throw new UnauthorizedAccessException("Operation failed: Invalid or expired access token");
            }

            // Before adding an item to an order, check if:
            // 1. The order exists
            // 2. The item exists and there is enough stock in storage
            await GetOrderIfExistsAndStatusIs(fullOrder.OrderId, (int)OrderStatusEnum.Open,user, "AddItemToOrder");

            await ItemIsAvailableInStorage();

            var existingFullOrder = await _fullOrderRepository.GetFullOrderAsync(fullOrder.OrderId, fullOrder.ItemId);

            // Reduce reserved item count in storage
            await _itemRepository.AddStorageAsync(fullOrder.ItemId, -fullOrder.Count);

            // If the item is already in the order (fullOrder record which links the order with the item exists in the database)
            // update its quantity by adding new count to existing count
            // Otherwise, create a new record for it
            if (existingFullOrder != null)
            {
                await _fullOrderRepository.UpdateItemInOrderCountAsync(fullOrder);
            }
            else
            {
                await _fullOrderRepository.AddItemToOrderAsync(fullOrder, userId.Value);

                //refetch to get back the id
                existingFullOrder = await _fullOrderRepository.GetFullOrderAsync(fullOrder.OrderId, fullOrder.ItemId);

                //Save tax historic data
                var taxes = await _taxService.GetItemTaxes(fullOrder.ItemId);

                foreach(var tax in taxes)
                {
                    var fullOrderTax = new FullOrderTaxModel
                    {
                        FullOrderId = existingFullOrder.FullOrderId,
                        Percentage = tax.Percentage,
                        Description = tax.Description
                    };

                    await _fullOrderTaxRepository.AddItemToFullOrderTaxAsync(fullOrderTax);
                }
            }

            async Task ItemIsAvailableInStorage()
            {
                var storage = await _itemRepository.GetItemStorageAsync(fullOrder.ItemId);
                if (storage == null)
                {
                    _logger.LogError($"Failed to add item {fullOrder.ItemId} to order {fullOrder.OrderId}: Item not found in storage");
                    throw new ItemNotFoundException(fullOrder.ItemId);
                }

                if (storage.Count < fullOrder.Count)
                {
                    _logger.LogError($"Not enough stock for item {fullOrder.ItemId}. Requested: {fullOrder.Count}, Available: {storage.Count} for order {fullOrder.OrderId}");
                    throw new StockExhaustedException(fullOrder.ItemId, storage.Count);
                }
            }
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

        // === Stubs for other IOrderService methods ===

        public Task<OrderItemsPayments> OpenOrder(int? createdByEmployeeId, int? establishmentId)
            => throw new NotImplementedException();

        public Task<PaginatedResult<OrderModel>> GetAllOrders(int pageNumber, int pageSize, IPrincipal user)
            => throw new NotImplementedException();

        public Task<OrderItemsPayments> GetOrderById(int orderId, IPrincipal user)
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
