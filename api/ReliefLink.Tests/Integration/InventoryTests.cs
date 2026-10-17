using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReliefLink.Api.Controllers.Dtos;
using ReliefLink.Api.Domain;
using ReliefLink.Tests.Infrastructure;

namespace ReliefLink.Tests.Integration;

[Collection(ApiCollection.Name)]
public class InventoryTests(ApiFactory factory)
{
    private static string NewToken() => Guid.NewGuid().ToString("N");

    [Fact]
    public async Task GetDepots_ReturnsSeededDepots()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/depots");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var depots = await response.Content.ReadFromJsonAsync<List<DepotDto>>();
        Assert.NotNull(depots);
        Assert.True(depots.Count >= 4, "Expected at least 4 seeded depots");
        Assert.Contains(depots, d => d.District == "Kalutara");
    }

    [Fact]
    public async Task CreateDepot_AsCoordinator_Returns201Created()
    {
        var coordinator = await factory.ClientForAsync("coordinator@relieflink.local");
        var token = NewToken();

        var payload = new DepotInput(
            Code: $"DEP-{token[..4].ToUpperInvariant()}",
            Name: $"Test Depot {token}",
            District: "Matara",
            AddressText: "123 Beach Road",
            Latitude: 5.95,
            Longitude: 80.53,
            ContactPerson: "Kamal Perera",
            ContactPhone: "+94771234567",
            CapacitySquareMeters: 500.0,
            Notes: "Integration test depot"
        );

        var response = await coordinator.PostAsJsonAsync("/api/depots", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DepotDto>();
        Assert.NotNull(created);
        Assert.Equal(payload.Name, created.Name);
        Assert.Equal("Matara", created.District);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task CreateDepot_AsCitizen_Returns403Forbidden()
    {
        var citizen = await factory.ClientForAsync("citizen@relieflink.local");
        var token = NewToken();

        var payload = new DepotInput(
            Code: $"DEP-{token[..4].ToUpperInvariant()}",
            Name: $"Unauthorized Depot {token}",
            District: "Matara",
            AddressText: "123 Beach Road",
            Latitude: 5.95,
            Longitude: 80.53,
            ContactPerson: "Kamal",
            ContactPhone: "+94771234567",
            CapacitySquareMeters: 200.0,
            Notes: null
        );

        var response = await citizen.PostAsJsonAsync("/api/depots", payload);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSupplyItems_ReturnsCatalog()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/supply-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<SupplyItemDto>>();
        Assert.NotNull(items);
        Assert.True(items.Count >= 6, "Expected at least 6 seeded supply items");
        Assert.Contains(items, i => i.Code == "FOOD-DRY-RATION-01");
    }

    [Fact]
    public async Task CreateSupplyItem_AsCoordinator_Returns201Created()
    {
        var coordinator = await factory.ClientForAsync("coordinator@relieflink.local");
        var token = NewToken()[..6].ToUpperInvariant();

        var payload = new SupplyItemInput(
            Code: $"MED-KIT-{token}",
            Name: $"First Aid Kit {token}",
            Description: "Emergency medical essentials",
            Category: NeedCategory.Medical,
            UnitOfMeasure: "Kit",
            WeightKg: 1.25,
            ReorderThreshold: 20,
            StandardFamilyAllocation: 1
        );

        var response = await coordinator.PostAsJsonAsync("/api/supply-items", payload);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SupplyItemDto>();
        Assert.NotNull(created);
        Assert.Equal(payload.Code, created.Code);
        Assert.Equal(payload.Name, created.Name);
    }

    [Fact]
    public async Task ReceiveStock_And_QueryStockLevels_WorksCorrectly()
    {
        var coordinator = await factory.ClientForAsync("coordinator@relieflink.local");

        // Fetch depots and items
        var depotsResponse = await coordinator.GetAsync("/api/depots");
        var depots = await depotsResponse.Content.ReadFromJsonAsync<List<DepotDto>>();
        var depot = depots!.First(d => d.District == "Kalutara");

        var itemsResponse = await coordinator.GetAsync("/api/supply-items");
        var items = await itemsResponse.Content.ReadFromJsonAsync<List<SupplyItemDto>>();
        var item = items!.First(i => i.Code == "MED-FIRST-AID-01");

        // Receive stock
        var receivePayload = new ReceiveStockInput(
            DepotId: depot.Id,
            SupplyItemId: item.Id,
            Quantity: 50,
            ReferenceNumber: "PO-TEST-100",
            StorageBin: "BIN-A1",
            Notes: "Integration test incoming shipment"
        );

        var receiveRes = await coordinator.PostAsJsonAsync("/api/stock/receive", receivePayload);
        Assert.Equal(HttpStatusCode.OK, receiveRes.StatusCode);
        var updatedLevel = await receiveRes.Content.ReadFromJsonAsync<StockLevelDto>();
        Assert.NotNull(updatedLevel);
        Assert.True(updatedLevel.QuantityOnHand >= 50);

        // Query stock levels for this depot
        var queryRes = await coordinator.GetAsync($"/api/stock?depotId={depot.Id}");
        Assert.Equal(HttpStatusCode.OK, queryRes.StatusCode);
        var stockList = await queryRes.Content.ReadFromJsonAsync<List<StockLevelDto>>();
        Assert.NotNull(stockList);
        Assert.Contains(stockList, s => s.SupplyItemId == item.Id && s.QuantityOnHand >= 50);
    }

    [Fact]
    public async Task ReservationLifecycle_Reserve_Fulfill_GuaranteesZeroNegativeStock()
    {
        var coordinator = await factory.ClientForAsync("coordinator@relieflink.local");

        // 1. Create a distinct supply item to isolate this test's stock
        var token = NewToken()[..6].ToUpperInvariant();
        var itemRes = await coordinator.PostAsJsonAsync("/api/supply-items", new SupplyItemInput(
            Code: $"ISO-{token}",
            Name: $"Isolated Test Item {token}",
            Description: "Testing reservation invariants",
            Category: NeedCategory.Water,
            UnitOfMeasure: "Bottle",
            WeightKg: 1.0,
            ReorderThreshold: 10,
            StandardFamilyAllocation: 4
        ));
        var item = await itemRes.Content.ReadFromJsonAsync<SupplyItemDto>();

        var depotsRes = await coordinator.GetAsync("/api/depots");
        var depots = await depotsRes.Content.ReadFromJsonAsync<List<DepotDto>>();
        var depot = depots!.First();

        // 2. Receive exactly 10 units into depot
        await coordinator.PostAsJsonAsync("/api/stock/receive", new ReceiveStockInput(
            DepotId: depot.Id,
            SupplyItemId: item!.Id,
            Quantity: 10,
            ReferenceNumber: "REF1",
            StorageBin: null,
            Notes: null
        ));

        // 3. Attempt to reserve 15 units -> Must fail with 400 Bad Request (Zero Negative Stock rule)
        var failReserveRes = await coordinator.PostAsJsonAsync("/api/reservations", new CreateReservationInput(
            DepotId: depot.Id,
            SupplyItemId: item.Id,
            Quantity: 15,
            ReliefRequestId: null,
            HoldHours: 2,
            Notes: "Excess reservation"
        ));
        Assert.Equal(HttpStatusCode.BadRequest, failReserveRes.StatusCode);

        // 4. Reserve valid 6 units -> Succeeds
        var okReserveRes = await coordinator.PostAsJsonAsync("/api/reservations", new CreateReservationInput(
            DepotId: depot.Id,
            SupplyItemId: item.Id,
            Quantity: 6,
            ReliefRequestId: null,
            HoldHours: 2,
            Notes: "Valid reservation"
        ));
        Assert.Equal(HttpStatusCode.Created, okReserveRes.StatusCode);
        var reservation = await okReserveRes.Content.ReadFromJsonAsync<StockReservationDto>();
        Assert.NotNull(reservation);
        Assert.Equal(6, reservation.Quantity);
        Assert.Equal(ReservationStatus.Allocated, reservation.Status);

        // 5. Verify stock level: onHand = 10, reserved = 6, available = 4
        var stockRes = await coordinator.GetAsync($"/api/stock?depotId={depot.Id}");
        var stockList = await stockRes.Content.ReadFromJsonAsync<List<StockLevelDto>>();
        var isolatedStock = stockList!.First(s => s.SupplyItemId == item.Id);
        Assert.Equal(10, isolatedStock.QuantityOnHand);
        Assert.Equal(6, isolatedStock.QuantityReserved);
        Assert.Equal(4, isolatedStock.QuantityAvailable);

        // 6. Fulfill reservation -> onHand = 4, reserved = 0, available = 4
        var fulfillRes = await coordinator.PostAsync($"/api/reservations/{reservation.Id}/fulfill?notes=Delivered+to+camp", null);
        Assert.Equal(HttpStatusCode.OK, fulfillRes.StatusCode);
        var fulfilled = await fulfillRes.Content.ReadFromJsonAsync<StockReservationDto>();
        Assert.Equal(ReservationStatus.Fulfilled, fulfilled!.Status);

        // 7. Verify stock level again
        var stockResAfter = await coordinator.GetAsync($"/api/stock?depotId={depot.Id}");
        var stockListAfter = await stockResAfter.Content.ReadFromJsonAsync<List<StockLevelDto>>();
        var stockAfterFulfill = stockListAfter!.First(s => s.SupplyItemId == item.Id);
        Assert.Equal(4, stockAfterFulfill.QuantityOnHand);
        Assert.Equal(0, stockAfterFulfill.QuantityReserved);
        Assert.Equal(4, stockAfterFulfill.QuantityAvailable);
    }

    [Fact]
    public async Task ReservationCancellation_RestoresAvailableStock()
    {
        var coordinator = await factory.ClientForAsync("coordinator@relieflink.local");

        var token = NewToken()[..6].ToUpperInvariant();
        var itemRes = await coordinator.PostAsJsonAsync("/api/supply-items", new SupplyItemInput(
            Code: $"CAN-{token}",
            Name: $"Cancellation Test Item {token}",
            Description: "Cancel test",
            Category: NeedCategory.Food,
            UnitOfMeasure: "Pack",
            WeightKg: 2.0,
            ReorderThreshold: 5,
            StandardFamilyAllocation: 2
        ));
        var item = await itemRes.Content.ReadFromJsonAsync<SupplyItemDto>();

        var depotsRes = await coordinator.GetAsync("/api/depots");
        var depots = await depotsRes.Content.ReadFromJsonAsync<List<DepotDto>>();
        var depot = depots!.First();

        await coordinator.PostAsJsonAsync("/api/stock/receive", new ReceiveStockInput(
            DepotId: depot.Id,
            SupplyItemId: item!.Id,
            Quantity: 20,
            ReferenceNumber: null,
            StorageBin: null,
            Notes: null
        ));

        // Reserve 8 units
        var reserveRes = await coordinator.PostAsJsonAsync("/api/reservations", new CreateReservationInput(
            DepotId: depot.Id,
            SupplyItemId: item.Id,
            Quantity: 8,
            ReliefRequestId: null,
            HoldHours: 1,
            Notes: null
        ));
        var reservation = await reserveRes.Content.ReadFromJsonAsync<StockReservationDto>();

        // Cancel reservation
        var cancelRes = await coordinator.PostAsync($"/api/reservations/{reservation!.Id}/cancel?reason=Request+rerouted", null);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelled = await cancelRes.Content.ReadFromJsonAsync<StockReservationDto>();
        Assert.Equal(ReservationStatus.Cancelled, cancelled!.Status);

        // Verify stock is fully restored (onHand = 20, reserved = 0, available = 20)
        var stockRes = await coordinator.GetAsync($"/api/stock?depotId={depot.Id}");
        var stockList = await stockRes.Content.ReadFromJsonAsync<List<StockLevelDto>>();
        var stockAfterCancel = stockList!.First(s => s.SupplyItemId == item.Id);
        Assert.Equal(20, stockAfterCancel.QuantityOnHand);
        Assert.Equal(0, stockAfterCancel.QuantityReserved);
        Assert.Equal(20, stockAfterCancel.QuantityAvailable);
    }
}
