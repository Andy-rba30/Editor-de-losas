using System;
using System.Collections.Generic;
using Clipper2Lib;

namespace ARBA.Losas.Geometry.Internal
{
    /// <summary>Tipo de vertice, en orden de prioridad para sobrevivir a una fusion (menor = mas importante).</summary>
    internal enum VertexKind
    {
        /// <summary>Vertice original del boceto (union de dos curvas).</summary>
        Original = 0,
        /// <summary>Punto de corte proyectado sobre una curva original.</summary>
        OnCurve = 1,
        /// <summary>Interseccion entre dos cortes o punto sin curva conocida.</summary>
        Cut = 2,
        /// <summary>Vertice intermedio del teselado de un arco.</summary>
        Tessellation = 3
    }

    /// <summary>Pertenencia de un vertice a una curva original, con su parametro normalizado.</summary>
    internal readonly struct Membership
    {
        public int CurveId { get; }
        public double T { get; }
        public Membership(int curveId, double t) { CurveId = curveId; T = t; }
    }

    /// <summary>
    /// Vertice canonico: una posicion real compartida por todas las regiones que pasan por
    /// el, con las curvas originales a las que pertenece. Asi los bordes de dos losas
    /// vecinas terminan exactamente en el mismo punto.
    /// </summary>
    internal sealed class RegistryPoint
    {
        public int Id { get; }
        public Vec2 Position { get; internal set; }
        public Point64 Key { get; internal set; }
        public VertexKind Kind { get; internal set; }
        /// <summary>Losa de origen (union) o -1.</summary>
        public int Slab { get; internal set; } = -1;
        public List<Membership> Memberships { get; } = new List<Membership>();
        /// <summary>Si no es null, este punto se fusiono en otro.</summary>
        public RegistryPoint AliasOf { get; internal set; }

        public RegistryPoint(int id, Vec2 position, Point64 key, VertexKind kind)
        {
            Id = id; Position = position; Key = key; Kind = kind;
        }

        public bool TryGetT(int curveId, out double t)
        {
            foreach (Membership m in Memberships)
                if (m.CurveId == curveId) { t = m.T; return true; }
            t = 0;
            return false;
        }

        public void AddMembership(int curveId, double t)
        {
            foreach (Membership m in Memberships)
                if (m.CurveId == curveId) return;
            Memberships.Add(new Membership(curveId, t));
        }

        public override string ToString() => "#" + Id + " " + Position + " " + Kind;
    }

    /// <summary>
    /// Registro de vertices canonicos con busqueda por proximidad en coordenadas escaladas.
    /// Clipper2 redondea a enteros, asi que dos apariciones del mismo punto pueden diferir
    /// en una o dos unidades: la busqueda admite <see cref="MatchTolerance"/> unidades.
    /// </summary>
    internal sealed class PointRegistry
    {
        public const double MatchTolerance = 2.0;
        private const long CellSize = 4;

        private readonly Dictionary<(long, long), List<(Point64 Key, RegistryPoint Point)>> _cells =
            new Dictionary<(long, long), List<(Point64, RegistryPoint)>>();
        private readonly List<RegistryPoint> _all = new List<RegistryPoint>();

        public double Scale { get; }

        public PointRegistry(double scale)
        {
            if (!(scale > 0)) throw new ArgumentException("La escala debe ser positiva.");
            Scale = scale;
        }

        public IReadOnlyList<RegistryPoint> All => _all;

        public Point64 ToKey(Vec2 p) => new Point64((long)Math.Round(p.X * Scale), (long)Math.Round(p.Y * Scale));
        public Vec2 FromKey(Point64 k) => new Vec2(k.X / Scale, k.Y / Scale);

        private static (long, long) CellOf(Point64 k) => (FloorDiv(k.X, CellSize), FloorDiv(k.Y, CellSize));

        private static long FloorDiv(long a, long b) => (a >= 0) ? a / b : -((-a + b - 1) / b);

        /// <summary>Punto registrado (resuelto) a menos de <see cref="MatchTolerance"/> unidades, o null.</summary>
        public RegistryPoint Find(Point64 key)
        {
            (long cx, long cy) = CellOf(key);
            RegistryPoint best = null;
            double bestD = double.MaxValue;
            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                {
                    if (!_cells.TryGetValue((cx + dx, cy + dy), out var list)) continue;
                    foreach ((Point64 k, RegistryPoint p) in list)
                    {
                        double ddx = k.X - key.X, ddy = k.Y - key.Y;
                        double d = Math.Sqrt(ddx * ddx + ddy * ddy);
                        if (d <= MatchTolerance && d < bestD) { bestD = d; best = p; }
                    }
                }
            return best == null ? null : Resolve(best);
        }

        public RegistryPoint Find(Vec2 p) => Find(ToKey(p));

        private void Index(Point64 key, RegistryPoint p)
        {
            (long, long) cell = CellOf(key);
            if (!_cells.TryGetValue(cell, out var list))
            {
                list = new List<(Point64, RegistryPoint)>();
                _cells[cell] = list;
            }
            list.Add((key, p));
        }

        /// <summary>Devuelve el punto existente en esa posicion o crea uno nuevo.</summary>
        public RegistryPoint GetOrAdd(Vec2 position, VertexKind kind, out bool created)
        {
            Point64 key = ToKey(position);
            RegistryPoint existing = Find(key);
            if (existing != null)
            {
                if (kind < existing.Kind) existing.Kind = kind;
                created = false;
                return existing;
            }
            var p = new RegistryPoint(_all.Count, position, key, kind);
            _all.Add(p);
            Index(key, p);
            created = true;
            return p;
        }

        /// <summary>Hace que una coordenada escalada distinta (por ejemplo la salida sin proyectar de Clipper) resuelva a <paramref name="p"/>.</summary>
        public void AddAlias(Point64 rawKey, RegistryPoint p) => Index(rawKey, p);

        public RegistryPoint Resolve(RegistryPoint p)
        {
            while (p.AliasOf != null) p = p.AliasOf;
            return p;
        }

        /// <summary>Fusiona <paramref name="victim"/> en <paramref name="survivor"/>: conserva la posicion del superviviente y hereda las pertenencias que le falten.</summary>
        public void Merge(RegistryPoint victim, RegistryPoint survivor)
        {
            victim = Resolve(victim);
            survivor = Resolve(survivor);
            if (ReferenceEquals(victim, survivor)) return;
            foreach (Membership m in victim.Memberships) survivor.AddMembership(m.CurveId, m.T);
            if (victim.Kind < survivor.Kind) survivor.Kind = victim.Kind;
            victim.AliasOf = survivor;
            Index(victim.Key, survivor);
        }

        /// <summary>Puntos canonicos a menos de <paramref name="tolerance"/> (en unidades reales) de <paramref name="p"/>. Busqueda lineal: pensada para tolerancias grandes.</summary>
        public IEnumerable<RegistryPoint> Near(Vec2 p, double tolerance)
        {
            foreach (RegistryPoint q in _all)
            {
                if (q.AliasOf != null) continue;
                if (q.Position.DistanceTo(p) <= tolerance) yield return q;
            }
        }
    }
}
