# run_experiment.ps1
# Runs all 16 test targets in both RAG and baseline (no-RAG) conditions.
# Prerequisites: ragitgen-generate must be installed and accessible on PATH.
# Usage: .\run_experiment.ps1

$ErrorActionPreference = "Stop"

# Define all test targets: TargetClass, TargetMethod, Description, TargetSourceFile
$targets = @(
    @{
        Class       = "OrderService"
        Method      = "PayOrder"
        Description = "Test that paying an order with a gift card validates the gift card exists and is not expired, deducts the payment amount from the gift card balance, records the payment, and marks the order as Completed when the full amount is paid"
        Source      = "OrderService_PayOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "RefundOrder"
        Description = "Test that refunding a completed order iterates all payments, restores gift card balances and extends expiration dates by 7 days, triggers Stripe refunds for card payments, and marks the order as refunded"
        Source      = "OrderService_RefundOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "GetOrderById"
        Description = "Test that retrieving an order by ID assembles the full order with employee name enrichment, item details with discounts and taxes, service details with discounts and taxes, and calculates total price, total paid, and left to pay"
        Source      = "OrderService_GetOrderById.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "AddItemToOrder"
        Description = "Test that adding an item to an open order validates the order status is Open, checks stock availability in storage, reduces the storage count, creates a new FullOrder record linking the item to the order, and saves a tax history snapshot"
        Source      = "OrderService_AddItemToOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "AddServiceToOrder"
        Description = "Test that adding a service to an order creates a new FullOrderService record linking the service to the order and saves a tax history snapshot for the service's taxes"
        Source      = "OrderService_AddServiceToOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "RemoveItemFromOrder"
        Description = "Test that removing an item from an open order validates the order is Open, verifies the item is linked to the order, restores the item count to storage capped by the original reserved amount, and either reduces the quantity or fully removes the item from the order"
        Source      = "OrderService_RemoveItemFromOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "RemoveServiceFromOrder"
        Description = "Test that removing a service from an open order validates the order is Open, verifies the service is linked to the order, and either reduces the service quantity or fully removes it from the order"
        Source      = "OrderService_RemoveServiceFromOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "CancelOrder"
        Description = "Test that cancelling an open order validates the order status is Open, iterates all order items to restore their quantities back to storage, and sets the order status to Cancelled"
        Source      = "OrderService_CancelOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "DiscountOrder"
        Description = "Test that applying a discount to an order correctly handles three-way branching: applying a discount to a specific item in the order, applying a discount to a specific service in the order, or applying a discount to the entire order"
        Source      = "OrderService_DiscountOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "GetAllOrders"
        Description = "Test that retrieving all orders returns a paginated list with each order enriched with the creating employee's full name by cross-referencing the employee repository"
        Source      = "OrderService_GetAllOrders.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "TipOrder"
        Description = "Test that adding a tip to an open order validates the order is Open and correctly handles both fixed-amount tips and percentage-based tips, clearing the other tip type when one is set"
        Source      = "OrderService_TipOrder.cs"
    },
    @{
        Class       = "OrderService"
        Method      = "CloseOrder"
        Description = "Test that closing an order validates the order status is Open, transitions the order status from Open to Closed, and persists the updated status"
        Source      = "OrderService_CloseOrder.cs"
    },
    @{
        Class       = "PaymentService"
        Method      = "CreatePaymentIntent"
        Description = "Test that creating a Stripe payment intent converts the decimal amount to cents, creates a PaymentIntent with automatic payment methods enabled, and returns the created PaymentIntent object"
        Source      = "PaymentService_CreatePaymentIntent.cs"
    },
    @{
        Class       = "PaymentService"
        Method      = "RefundPaymentIntent"
        Description = "Test that refunding a Stripe payment intent creates a refund request with the correct payment intent ID and calls the Stripe RefundService"
        Source      = "PaymentService_RefundPaymentIntent.cs"
    },
    @{
        Class       = "ReservationService"
        Method      = "CreateNewReservation"
        Description = "Test that creating a new reservation validates the employee ID is present, sets the creator, sends an SMS notification with reservation details via the SMS client, and persists the reservation to the database"
        Source      = "ReservationService_CreateNewReservation.cs"
    },
    @{
        Class       = "ReservationService"
        Method      = "DeleteReservation"
        Description = "Test that deleting a reservation fetches the reservation details, sends an SMS cancellation notification to the customer, and removes the reservation from the database"
        Source      = "ReservationService_DeleteReservation.cs"
    }
)

$experimentDir = $PSScriptRoot
$targetsDir = Join-Path $experimentDir "targets"
$fixtureFile = Join-Path $experimentDir "IntegrationTestFixture.cs"
$exampleFile = Join-Path $experimentDir "ExampleIntegrationTest.cs"
$ragResultsDir = Join-Path $experimentDir "results" "rag"
$baselineResultsDir = Join-Path $experimentDir "results" "baseline"

# Ensure output directories exist
New-Item -ItemType Directory -Force -Path $ragResultsDir | Out-Null
New-Item -ItemType Directory -Force -Path $baselineResultsDir | Out-Null

$totalTargets = $targets.Count
$currentTarget = 0

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " POS Integration Test Experiment Runner" -ForegroundColor Cyan
Write-Host " $totalTargets targets x 2 conditions = $($totalTargets * 2) generations" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

foreach ($target in $targets) {
    $currentTarget++
    $class = $target.Class
    $method = $target.Method
    $description = $target.Description
    $sourceFile = Join-Path $targetsDir $target.Source
    $ragOutput = Join-Path $ragResultsDir "${class}_${method}.cs"
    $baselineOutput = Join-Path $baselineResultsDir "${class}_${method}.cs"

    Write-Host "[$currentTarget/$totalTargets] ${class}.${method}" -ForegroundColor Yellow

    # RAG condition
    Write-Host "  -> RAG condition..." -ForegroundColor Green
    ragitgen-generate $class $method $description `
        --environment remote `
        --target-source $sourceFile `
        --fixture-source $fixtureFile `
        --example-test $exampleFile `
        --retrieval-query-strategy combined `
        --output $ragOutput

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [WARN] RAG generation failed for ${class}.${method}" -ForegroundColor Red
    } else {
        Write-Host "  [OK] RAG result saved to $ragOutput" -ForegroundColor Green
    }

    # Baseline (no-RAG) condition
    Write-Host "  -> Baseline (no-RAG) condition..." -ForegroundColor Green
    ragitgen-generate $class $method $description `
        --environment remote `
        --target-source $sourceFile `
        --fixture-source $fixtureFile `
        --example-test $exampleFile `
        --no-retrieval `
        --output $baselineOutput

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [WARN] Baseline generation failed for ${class}.${method}" -ForegroundColor Red
    } else {
        Write-Host "  [OK] Baseline result saved to $baselineOutput" -ForegroundColor Green
    }

    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Experiment complete!" -ForegroundColor Cyan
Write-Host " RAG results:      $ragResultsDir" -ForegroundColor Cyan
Write-Host " Baseline results: $baselineResultsDir" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
