using ReliefLink.Api.Domain;

namespace ReliefLink.Tests.Unit;

public class InventoryManagerTests
{
    private static StockLevel CreateStock(int onHand = 100, int reserved = 20)
    {
        return new StockLevel
        {
            Id = Guid.NewGuid(),
            DepotId = Guid.NewGuid(),
            SupplyItemId = Guid.NewGuid(),
            QuantityOnHand = onHand,
            QuantityReserved = reserved
        };
    }

    [Fact]
    public void AllocateReservation_Succeeds_WhenStockAvailable()
    {
        var stock = CreateStock(onHand: 100, reserved: 20); // available = 80
        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var reservation = InventoryManager.AllocateReservation(stock, 30, userId, requestId, TimeSpan.FromHours(24), "Test");

        Assert.NotNull(reservation);
        Assert.Equal(50, stock.QuantityReserved); // 20 + 30
        Assert.Equal(50, stock.QuantityAvailable); // 100 - 50
        Assert.Equal(30, reservation.Quantity);
        Assert.Equal(ReservationStatus.Allocated, reservation.Status);
        Assert.Equal(requestId, reservation.ReliefRequestId);
        Assert.Equal(userId, reservation.ReservedByUserId);
        Assert.StartsWith("RES-", reservation.ReservationNumber);
    }

    [Fact]
    public void AllocateReservation_Throws_WhenRequestedQtyExceedsAvailable()
    {
        var stock = CreateStock(onHand: 50, reserved: 40); // available = 10
        var userId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            InventoryManager.AllocateReservation(stock, 15, userId));

        Assert.Contains("Insufficient available stock", ex.Message);
        Assert.Equal(40, stock.QuantityReserved); // Unchanged
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AllocateReservation_Throws_WhenQuantityNotPositive(int qty)
    {
        var stock = CreateStock(100, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InventoryManager.AllocateReservation(stock, qty, Guid.NewGuid()));
    }

    [Fact]
    public void ReleaseReservation_RestoresQuantityReserved()
    {
        var stock = CreateStock(onHand: 100, reserved: 35);
        var res = new StockReservation
        {
            Status = ReservationStatus.Allocated,
            Quantity = 20
        };

        var now = DateTime.UtcNow;
        InventoryManager.ReleaseReservation(res, stock, now, "Request cancelled");

        Assert.Equal(15, stock.QuantityReserved); // 35 - 20
        Assert.Equal(ReservationStatus.Cancelled, res.Status);
        Assert.Equal(now, res.CancelledAt);
        Assert.Contains("Request cancelled", res.Notes);
    }

    [Fact]
    public void ReleaseReservation_Throws_WhenAlreadyCancelledOrFulfilled()
    {
        var stock = CreateStock(100, 20);
        var res = new StockReservation { Status = ReservationStatus.Fulfilled, Quantity = 20 };

        Assert.Throws<InvalidOperationException>(() =>
            InventoryManager.ReleaseReservation(res, stock, DateTime.UtcNow));
    }

    [Fact]
    public void FulfillReservation_DeductsBothOnHandAndReserved()
    {
        var stock = CreateStock(onHand: 100, reserved: 30);
        var res = new StockReservation
        {
            Status = ReservationStatus.Allocated,
            Quantity = 25
        };

        var now = DateTime.UtcNow;
        InventoryManager.FulfillReservation(res, stock, now, "Dispatched to driver");

        Assert.Equal(75, stock.QuantityOnHand); // 100 - 25
        Assert.Equal(5, stock.QuantityReserved); // 30 - 25
        Assert.Equal(ReservationStatus.Fulfilled, res.Status);
        Assert.Equal(now, res.FulfilledAt);
        Assert.Contains("Dispatched to driver", res.Notes);
    }

    [Fact]
    public void FulfillReservation_Throws_WhenOnHandLessThanReservationQuantity()
    {
        var stock = CreateStock(onHand: 10, reserved: 10);
        var res = new StockReservation { Status = ReservationStatus.Allocated, Quantity = 15 };

        Assert.Throws<InvalidOperationException>(() =>
            InventoryManager.FulfillReservation(res, stock, DateTime.UtcNow));
    }

    [Fact]
    public void ReceiveStock_IncreasesQuantityOnHand()
    {
        var stock = CreateStock(onHand: 50, reserved: 10);
        var now = DateTime.UtcNow;

        InventoryManager.ReceiveStock(stock, 100, now);

        Assert.Equal(150, stock.QuantityOnHand);
        Assert.Equal(now, stock.LastRestockedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ReceiveStock_Throws_WhenQuantityNotPositive(int qty)
    {
        var stock = CreateStock();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            InventoryManager.ReceiveStock(stock, qty, DateTime.UtcNow));
    }

    [Fact]
    public void AdjustStock_Succeeds_WhenNewOnHandGteReserved()
    {
        var stock = CreateStock(onHand: 100, reserved: 30);

        InventoryManager.AdjustStock(stock, 40);

        Assert.Equal(40, stock.QuantityOnHand);
        Assert.Equal(10, stock.QuantityAvailable); // 40 - 30
    }

    [Fact]
    public void AdjustStock_Throws_WhenNewOnHandLessThanReserved()
    {
        var stock = CreateStock(onHand: 100, reserved: 30);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            InventoryManager.AdjustStock(stock, 25));

        Assert.Contains("below active reservations", ex.Message);
        Assert.Equal(100, stock.QuantityOnHand); // Unchanged
    }

    [Fact]
    public void IsLowStock_IdentifiesThresholds()
    {
        Assert.True(InventoryManager.IsLowStock(availableQty: 10, reorderThreshold: 20));
        Assert.True(InventoryManager.IsLowStock(availableQty: 20, reorderThreshold: 20));
        Assert.False(InventoryManager.IsLowStock(availableQty: 21, reorderThreshold: 20));
    }

    [Fact]
    public void ScoreDistrictProximity_CalculatesCorrectScores()
    {
        // Identical district
        Assert.Equal(100, InventoryManager.ScoreDistrictProximity("Kalutara", "Kalutara"));
        Assert.Equal(100, InventoryManager.ScoreDistrictProximity("kalutara", "KALUTARA"));

        // Neighbor district (Kalutara neighbours Colombo, Ratnapura, Galle)
        Assert.Equal(70, InventoryManager.ScoreDistrictProximity("Colombo", "Kalutara"));
        Assert.Equal(70, InventoryManager.ScoreDistrictProximity("Ratnapura", "Kalutara"));

        // Distant district
        Assert.Equal(30, InventoryManager.ScoreDistrictProximity("Jaffna", "Kalutara"));
    }
}
