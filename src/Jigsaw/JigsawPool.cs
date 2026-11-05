namespace AllMatters.Jigsaw;

internal static class JigsawPool
{
    public static IReadOnlyList<JigsawPiece> Create(int seed, bool doubleBoss)
    {
        GridPoint[][] shapes =
        [
            [new(0,0), new(1,0), new(2,0), new(3,0)],
            [new(0,0), new(1,0), new(0,1), new(1,1)],
            [new(0,0), new(1,0), new(2,0), new(1,1)],
            [new(0,0), new(1,0), new(1,1), new(2,1)],
            [new(0,1), new(1,1), new(1,0), new(2,0)],
            [new(0,0), new(0,1), new(1,1), new(2,1)],
            [new(0,0), new(1,0), new(2,0), new(2,1)]
        ];
        var random = new Random(seed);
        var counts = new[] { 20, 16, 6, 4, 6, 4 };
        JigsawRoom[] kinds = [JigsawRoom.Combat, JigsawRoom.Unknown, JigsawRoom.Elite, JigsawRoom.Shop, JigsawRoom.Rest, JigsawRoom.Treasure];
        var rooms = new JigsawRoom[56];
        var edges = new List<(int A,int B)>[14];
        for (int p = 0; p < 14; p++)
        {
            var s = shapes[p / 2];
            edges[p] = [];
            for (int a = 0; a < 4; a++) for (int b = a + 1; b < 4; b++)
                if (Math.Abs(s[a].X - s[b].X) + Math.Abs(s[a].Y - s[b].Y) == 1) edges[p].Add((a,b));
        }
        bool Fill(int index)
        {
            if (index == 56) return true;
            int p = index / 4, n = index % 4;
            foreach (int k in Enumerable.Range(0,6).OrderBy(_ => random.Next()))
            {
                if (counts[k] == 0) continue;
                var room = kinds[k];
                // Construct the pool with six distinct rest-bearing pieces. The
                // accompanying six-piece spatial witness uses these identities.
                if ((p < 6 && n == (p==0?1:0)) != (room == JigsawRoom.Rest)) continue;
                if (p == 0 && n >= (doubleBoss ? 2 : 3) && room is JigsawRoom.Shop or JigsawRoom.Rest or JigsawRoom.Treasure) continue;
                if (room == JigsawRoom.Rest && Enumerable.Range(p*4,n).Any(i => rooms[i] == room)) continue;
                if (n == 3 && Enumerable.Range(p*4,3).All(i => rooms[i] == room)) continue;
                if (room is JigsawRoom.Elite or JigsawRoom.Shop or JigsawRoom.Rest or JigsawRoom.Treasure)
                    if (edges[p].Any(e => e.B == n && rooms[p*4+e.A] == room)) continue;
                rooms[index] = room;
                counts[k]--;
                if (Fill(index + 1)) return true;
                counts[k]++;
            }
            return false;
        }
        if (!Fill(0)) throw new InvalidOperationException("Room allocation failed.");
        var result = new List<JigsawPiece>();
        for (int p = 0; p < 14; p++)
        {
            int spacing = p == 0 ? doubleBoss ? 4 : 3 : 2;
            var nodes = shapes[p/2].Select((v,n) => new JigsawPieceNode(p*4+n+1,
                new((v.X-shapes[p/2][0].X) * spacing, (v.Y-shapes[p/2][0].Y) * spacing), p == 0 && n == 3 ? doubleBoss ? JigsawRoom.SecondBoss : JigsawRoom.Boss : p == 0 && n == 2 && doubleBoss ? JigsawRoom.Boss : rooms[p*4+n], rooms[p*4+n])).ToArray();
            result.Add(new(p, (JigsawShape)(p/2), Array.AsReadOnly(nodes), Array.AsReadOnly(edges[p].Select(e => new JigsawEdge(nodes[e.A].Id,nodes[e.B].Id,true)).ToArray())));
        }
        return result.AsReadOnly();
    }
}
