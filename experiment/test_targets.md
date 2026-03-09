# Experiment Test Targets

16 test targets selected from the POS system's service layer. Each method performs meaningful business logic beyond simple CRUD delegation.

## Selection Criteria

- Public method in a `*Service` class
- Calls at least one injected dependency (repository or other service)
- Performs meaningful business logic (not a simple CRUD proxy)
- Involves cross-component interaction (multiple repositories, validations, branching logic, calculations)

---

## Targets

### OrderService (12 targets)

| # | Target Class | Target Method | Description |
|---|---|---|---|
| 1 | OrderService | PayOrder | Test that paying an order with a gift card validates the gift card exists and is not expired, deducts the payment amount from the gift card balance, records the payment, and marks the order as Completed when the full amount is paid |
| 2 | OrderService | RefundOrder | Test that refunding a completed order iterates all payments, restores gift card balances and extends expiration dates by 7 days, triggers Stripe refunds for card payments, and marks the order as refunded |
| 3 | OrderService | GetOrderById | Test that retrieving an order by ID assembles the full order with employee name enrichment, item details with discounts and taxes, service details with discounts and taxes, and calculates total price, total paid, and left to pay |
| 4 | OrderService | AddItemToOrder | Test that adding an item to an open order validates the order status is Open, checks stock availability in storage, reduces the storage count, creates a new FullOrder record linking the item to the order, and saves a tax history snapshot |
| 5 | OrderService | AddServiceToOrder | Test that adding a service to an order creates a new FullOrderService record linking the service to the order and saves a tax history snapshot for the service's taxes |
| 6 | OrderService | RemoveItemFromOrder | Test that removing an item from an open order validates the order is Open, verifies the item is linked to the order, restores the item count to storage capped by the original reserved amount, and either reduces the quantity or fully removes the item from the order |
| 7 | OrderService | RemoveServiceFromOrder | Test that removing a service from an open order validates the order is Open, verifies the service is linked to the order, and either reduces the service quantity or fully removes it from the order |
| 8 | OrderService | CancelOrder | Test that cancelling an open order validates the order status is Open, iterates all order items to restore their quantities back to storage, and sets the order status to Cancelled |
| 9 | OrderService | DiscountOrder | Test that applying a discount to an order correctly handles three-way branching: applying a discount to a specific item in the order, applying a discount to a specific service in the order, or applying a discount to the entire order |
| 10 | OrderService | GetAllOrders | Test that retrieving all orders returns a paginated list with each order enriched with the creating employee's full name by cross-referencing the employee repository |
| 11 | OrderService | TipOrder | Test that adding a tip to an open order validates the order is Open and correctly handles both fixed-amount tips and percentage-based tips, clearing the other tip type when one is set |
| 12 | OrderService | CloseOrder | Test that closing an order validates the order status is Open, transitions the order status from Open to Closed, and persists the updated status |

### PaymentService (2 targets)

| # | Target Class | Target Method | Description |
|---|---|---|---|
| 13 | PaymentService | CreatePaymentIntent | Test that creating a Stripe payment intent converts the decimal amount to cents, creates a PaymentIntent with automatic payment methods enabled, and returns the created PaymentIntent object |
| 14 | PaymentService | RefundPaymentIntent | Test that refunding a Stripe payment intent creates a refund request with the correct payment intent ID and calls the Stripe RefundService |

### ReservationService (2 targets)

| # | Target Class | Target Method | Description |
|---|---|---|---|
| 15 | ReservationService | CreateNewReservation | Test that creating a new reservation validates the employee ID is present, sets the creator, sends an SMS notification with reservation details via the SMS client, and persists the reservation to the database |
| 16 | ReservationService | DeleteReservation | Test that deleting a reservation fetches the reservation details, sends an SMS cancellation notification to the customer, and removes the reservation from the database |
