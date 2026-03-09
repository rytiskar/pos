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

        public async Task PayOrder(PaymentModel payment, IPrincipal user)
        {
            var existingOrderWithClosedStatus = (await GetOrderById(payment.OrderId, user)).Order;

            if (existingOrderWithClosedStatus == null)
                return;

            if (payment.Type == (int)PaymentTypeEnum.GiftCard)
            {
                GiftCardModel? giftcard = await _giftcardRepository.GetGiftCardByCodeAsync(payment.GiftCardCode);
                if (giftcard == null || giftcard.ExpirationDate < DateTime.Now)
                {
                    throw new GiftcardInvalidException(payment.GiftCardCode);
                }

                if (giftcard.Amount < payment.Value)
                {
                    throw new GiftcardNotEnoughFundsException(giftcard.Amount, payment.Value);
                }

                giftcard.Amount -= payment.Value;
                payment.GiftCardId = giftcard.GiftCardId;

                await _giftcardRepository.UpdateGiftCardAsync(giftcard);

            }

            if (existingOrderWithClosedStatus.LeftToPay == payment.Value)
            {
                existingOrderWithClosedStatus.Status = (int)OrderStatusEnum.Completed;
                await _orderRepository.UpdateOrderAsync(existingOrderWithClosedStatus);
            }

            await _paymentRepository.AddPaymentAsync(payment);
        }

        // Private helpers called by PayOrder (via GetOrderById)

        public async Task<OrderItemsPayments> GetOrderById(int orderId, IPrincipal user)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId, user);
            if (order == null)
            {
                _logger.LogInformation($"Order with id: {orderId} not found");
                return new OrderItemsPayments(null, null, null, null);
            }

            var employee = await _employeeRepository.GetEmployeeByIdAsync(order.CreatedByEmployeeId, user);
            if(employee != null)
                order.CreatedByEmployeeName = employee.FirstName + " " + employee.LastName;

            var orderItems = await GetOrderItems(orderId, user);
            var orderServices = await GetOrderServices(orderId, user);
            var orderWithTotalPrice = await CalculateTotalPriceForOrder(order, orderItems, orderServices);
            var orderPayments = await GetOrderPayments(orderId);
            var orderWithTotalPaidAndLeftToPay = CalculateTotalPaidAndLeftToPayForOrder(orderWithTotalPrice, orderPayments);

            return new OrderItemsPayments(orderWithTotalPaidAndLeftToPay, orderItems, orderServices, orderPayments);
        }

        private async Task<List<ItemModel>> GetOrderItems(int orderId, IPrincipal user)
        {
            var fullOrders = await _fullOrderRepository.GetOrderItemsAsync(orderId);
            var orderItems = new List<ItemModel>();
            foreach (var fullOrder in fullOrders)
            {
                var item = await _itemRepository.GetItemByIdFromFullOrderAsync(fullOrder.ItemId, orderId);
                if(item == null) continue;
                if (fullOrder.DiscountId.HasValue)
                {
                    var discount = await GetDiscountById(fullOrder.DiscountId.Value);
                    item.Discount = discount.Value;
                    item.DiscountName = discount.DiscountName + " (" + discount.Value + "%)";
                }
                item.Taxes = await _fullOrderTaxRepository.GetFullOrderItemTaxesAsync(fullOrder.FullOrderId);
                item.Count = fullOrder.Count;
                orderItems.Add(item);
            }
            return orderItems;
        }

        private async Task<List<ServiceModel>> GetOrderServices(int orderId, IPrincipal user)
        {
            var fullOrderServices = await _fullOrderServiceRepository.GetOrderServicesAsync(orderId);
            var orderServices = new List<ServiceModel>();
            foreach (var fullOrderService in fullOrderServices)
            {
                var service = await _serviceRepository.GetServiceByIdFromFullOrderAsync(fullOrderService.ServiceId, orderId);
                if (service == null) continue;
                if (fullOrderService.DiscountId.HasValue)
                {
                    var discount = await GetDiscountById(fullOrderService.DiscountId.Value);
                    service.Discount = discount.Value;
                    service.DiscountName = discount.DiscountName + " (" + discount.Value + "%)";
                }
                service.Taxes = await _fullOrderServiceTaxRepository.GetFullOrderServiceTaxesAsync(fullOrderService.FullOrderServiceId);
                service.Count = fullOrderService.Count;
                orderServices.Add(service);
            }
            return orderServices;
        }

        private async Task<DiscountModel> GetDiscountById(int discountId)
        {
            var discount = await _discountRepository.GetDiscountAsync(discountId);
            return discount;
        }

        private async Task<List<PaymentModel>> GetOrderPayments(int orderId)
        {
            var payments = await _paymentRepository.GetPaymentsByOrderIdAsync(orderId);
            return payments;
        }

        private async Task<OrderModel> CalculateTotalPriceForOrder(OrderModel order, List<ItemModel> orderItems, List<ServiceModel> orderServices)
        {
            decimal totalPrice = 0;
            foreach (var item in orderItems)
            {
                decimal itemCost = item.Cost ?? 0;
                decimal itemCount = item.Count ?? 0;
                if (item.Discount.HasValue)
                    itemCost -= Math.Round(itemCost * (item.Discount.Value / 100), 2);
                foreach(var tax in item.Taxes)
                    totalPrice += (Math.Round(tax.Percentage / 100 * itemCost, 2) * itemCount);
                totalPrice += itemCost * itemCount;
            }
            foreach (var service in orderServices)
            {
                decimal serviceCost = service.Cost ?? 0;
                decimal serviceCount = service.Count ?? 0;
                if (service.Discount.HasValue)
                    serviceCost -= Math.Round(serviceCost * (service.Discount.Value / 100), 2);
                foreach (var tax in service.Taxes)
                    totalPrice += (Math.Round(tax.Percentage / 100 * serviceCost, 2) * serviceCount);
                totalPrice += serviceCost * serviceCount;
            }
            if (order.DiscountId.HasValue)
            {
                var discount = await GetDiscountById(order.DiscountId.Value);
                totalPrice -= Math.Round(totalPrice * (discount.Value / 100), 2);
            }
            if (order.TipFixed != null)
            {
                totalPrice += (order.TipFixed ?? 0);
                order.TipAmount = (order.TipFixed ?? 0);
            }
            else if (order.TipPercentage != null)
            {
                decimal tip = Math.Round(totalPrice * ((order.TipPercentage ?? 0) / 100), 2);
                totalPrice += tip;
                order.TipAmount = tip;
            }
            order.TotalPrice = Math.Round(totalPrice, 2);
            return order;
        }

        private OrderModel CalculateTotalPaidAndLeftToPayForOrder(OrderModel order, List<PaymentModel> orderPayments)
        {
            decimal totalPaid = orderPayments.Sum(payment => payment.Value);
            order.TotalPaid = totalPaid;
            order.LeftToPay = order.TotalPrice - order.TotalPaid;
            return order;
        }

        // Stub signatures for other public methods (not under test)
        public Task<OrderItemsPayments> OpenOrder(int? createdByEmployeeId, int? establishmentId) => throw new NotImplementedException();
        public Task<PaginatedResult<OrderModel>> GetAllOrders(int pageNumber, int pageSize, IPrincipal user) => throw new NotImplementedException();
        public Task AddItemToOrder(FullOrderModel fullOrder, int? userId, IPrincipal user) => throw new NotImplementedException();
        public Task AddServiceToOrder(FullOrderServiceModel fullOrderServiceModel, int? userId, IPrincipal user) => throw new NotImplementedException();
        public Task RemoveItemFromOrder(FullOrderModel fullOrder, IPrincipal user) => throw new NotImplementedException();
        public Task RemoveServiceFromOrder(FullOrderServiceModel fullOrderService, IPrincipal user) => throw new NotImplementedException();
        public Task UpdateOrder(OrderModel order, IPrincipal user) => throw new NotImplementedException();
        public Task CloseOrder(int orderId, IPrincipal user) => throw new NotImplementedException();
        public Task CancelOrder(int orderId, IPrincipal user) => throw new NotImplementedException();
        public Task TipOrder(TipModel tip, IPrincipal user) => throw new NotImplementedException();
        public Task DiscountOrder(DiscountModel discount, IPrincipal user) => throw new NotImplementedException();
        public Task RefundOrder(int orderId, IPrincipal user) => throw new NotImplementedException();
        public Task<byte[]> DownloadReceipt(int orderId, IPrincipal user) => throw new NotImplementedException();
    }
}
