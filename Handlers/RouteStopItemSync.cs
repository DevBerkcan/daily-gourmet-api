using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Keeps a route's loading positions (RouteStopItem) in step with the order they were built
/// from. Routes snapshot the order when they're created (DeliveryRouteHandler.BuildStopsAsync), so
/// without this a later order edit either hit the Restrict FK RouteStopItem.OrderItemId (500) or —
/// for a same-day reduction — left the driver loading the old portion count.</summary>
public static class RouteStopItemSync
{
    private static readonly OrderStatus[] BindingStatuses = [OrderStatus.SUBMITTED, OrderStatus.CONFIRMED, OrderStatus.LOCKED];

    public const string DefaultTemperature = "mind. 65 °C";
    public static string ContainerDescription(int portions) => $"{Math.Ceiling(portions / 15.0)} × GN 1/1";

    public static RouteStopItem Build(Guid stopId, OrderItem orderItem) => new()
    {
        Id = Guid.NewGuid(), RouteStopId = stopId, RecipeId = orderItem.RecipeId, OrderId = orderItem.OrderId, OrderItemId = orderItem.Id,
        Portions = orderItem.Portions, ContainerDescription = ContainerDescription(orderItem.Portions),
        TemperatureRequirement = DefaultTemperature, CreatedAt = DateTime.UtcNow,
    };

    /// <summary>Call after changing <paramref name="order"/>.Items in memory, before SaveChanges.
    /// <paramref name="removedItemIds"/> are order items about to be deleted. Routes that are already
    /// ABGESCHLOSSEN are history: their positions only get detached (OrderItemId = null) so the order
    /// item can be deleted, never rewritten. Does not save.</summary>
    public static async Task SyncAsync(DailyGourmetDbContext db, Order order, IReadOnlyCollection<Guid> removedItemIds, CancellationToken ct)
    {
        // Through db.Routes (tenant-filtered) — RouteStop itself has no tenant query filter.
        var stops = await db.Routes
            .Where(r => r.Stops.Any(s => s.FacilityId == order.FacilityId))
            .SelectMany(r => r.Stops.Where(s => s.FacilityId == order.FacilityId).Select(s => new { Stop = s, r.Date, r.Status }))
            .ToListAsync(ct);
        if (stops.Count == 0) return;

        var stopIds = stops.Select(s => s.Stop.Id).ToList();
        var stopItems = await db.RouteStopItems.Where(i => stopIds.Contains(i.RouteStopId)).ToListAsync(ct);
        var closedStopIds = stops.Where(s => s.Status == RouteStatus.ABGESCHLOSSEN).Select(s => s.Stop.Id).ToHashSet();

        foreach (var item in stopItems.Where(i => i.OrderItemId is { } id && removedItemIds.Contains(id)))
        {
            if (closedStopIds.Contains(item.RouteStopId)) item.OrderItemId = null;
            else db.RouteStopItems.Remove(item);
        }

        var binding = BindingStatuses.Contains(order.Status);
        foreach (var orderItem in order.Items.Where(i => !removedItemIds.Contains(i.Id)))
        {
            foreach (var stop in stops.Where(s => s.Date == orderItem.Date && s.Status != RouteStatus.ABGESCHLOSSEN))
            {
                var existing = stopItems.FirstOrDefault(i => i.RouteStopId == stop.Stop.Id && i.OrderItemId == orderItem.Id);
                if (!binding || orderItem.Portions <= 0)
                {
                    if (existing is not null) db.RouteStopItems.Remove(existing);
                    continue;
                }
                if (existing is null)
                {
                    db.RouteStopItems.Add(Build(stop.Stop.Id, orderItem));
                }
                else if (existing.Portions != orderItem.Portions || existing.RecipeId != orderItem.RecipeId)
                {
                    existing.Portions = orderItem.Portions;
                    existing.RecipeId = orderItem.RecipeId;
                    existing.ContainerDescription = ContainerDescription(orderItem.Portions);
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}
