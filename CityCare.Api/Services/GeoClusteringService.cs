using CityCare.Api.Dtos.Map;
using CityCare.Core.Enums;

namespace CityCare.Api.Services;

/// Regroupement géographique (clustering) des incidents pour l'affichage carte
/// en dézoom (Lot 2). Algorithme par grille : l'espace est découpé en cellules
/// carrées de <c>cellSize</c> degrés ; chaque incident tombe dans une cellule ;
/// on renvoie un cluster par cellule non vide (centroïde + comptages).
public static class GeoClusteringService
{
    /// Détermine la taille de cellule (en degrés). <paramref name="cellSize"/> explicite
    /// prioritaire ; sinon dérivée du niveau de zoom (plus on dézoome, plus la cellule est large).
    public static decimal ResolveCellSize(int? zoom, decimal? cellSize)
    {
        if (cellSize is > 0)
            return cellSize.Value;

        var z = Math.Clamp(zoom ?? 12, 0, 20);
        return z switch
        {
            <= 3 => 5m,      // niveau pays
            <= 6 => 1m,      // niveau région
            <= 9 => 0.2m,    // niveau département / agglomération
            <= 12 => 0.05m,   // niveau ville (~5 km)
            <= 15 => 0.01m,   // niveau quartier (~1 km)
            _ => 0.002m   // niveau rue
        };
    }

    public static List<MapClusterDto> Cluster(
        IEnumerable<(decimal Lat, decimal Lng, IncidentStatus Status)> points,
        decimal cellSize)
    {
        if (cellSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(cellSize), "La taille de cellule doit être > 0.");

        var size = (double)cellSize;
        var cells = new Dictionary<(long X, long Y), Accumulator>();

        foreach (var (lat, lng, status) in points)
        {
            var key = (
                (long)Math.Floor((double)lat / size),
                (long)Math.Floor((double)lng / size));

            if (!cells.TryGetValue(key, out var acc))
            {
                acc = new Accumulator();
                cells[key] = acc;
            }

            acc.Count++;
            acc.SumLat += lat;
            acc.SumLng += lng;

            switch (status)
            {
                case IncidentStatus.Reported: acc.Reported++; break;
                case IncidentStatus.InProgress: acc.InProgress++; break;
                case IncidentStatus.Resolved: acc.Resolved++; break;
            }
        }

        return cells.Values
            .Select(a => new MapClusterDto(
                Latitude: Math.Round(a.SumLat / a.Count, 6),
                Longitude: Math.Round(a.SumLng / a.Count, 6),
                Count: a.Count,
                Reported: a.Reported,
                InProgress: a.InProgress,
                Resolved: a.Resolved))
            .ToList();
    }

    private sealed class Accumulator
    {
        public int Count;
        public decimal SumLat;
        public decimal SumLng;
        public int Reported;
        public int InProgress;
        public int Resolved;
    }
}