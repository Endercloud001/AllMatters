namespace AllMatters.Jigsaw;

public sealed record JigsawLayout(JigsawPlacement[] Placements, JigsawEdge[] Edges);

/// <summary>Independent acceptance of complete automatic results, including fallbacks.</summary>
public static class JigsawAutomatic
{
    public const string Algorithm = "attachment-search-v1";
    public static JigsawLayout? Solve(JigsawRun original,int expansions)
    {
        if(expansions<0)throw new ArgumentOutOfRangeException(nameof(expansions));
        var best=original.AutomaticWitness;
        if(best!=null && !Validate(original,best).Success)throw new ArgumentException("Saved automatic witness is invalid.");
        int remaining=expansions;
        JigsawLayout start=best??new([],[]);
        bool Visit(JigsawLayout current)
        {
            if(Validate(original,current).Success) { best=current; if(current.Placements.Length==original.Pool.Count)return true; }
            var nodes=Nodes(original,current);
            var used=current.Placements.Select(p=>p.PieceId).ToHashSet();
            foreach(var piece in original.Pool.Where(p=>!used.Contains(p.Id)).OrderByDescending(p=>p.Nodes.Any(n=>n.Room is JigsawRoom.Boss)).ThenByDescending(p=>p.Nodes.Any(n=>n.Room==JigsawRoom.Rest)).ThenBy(p=>Array.IndexOf(original.AutomaticOperation?.CandidateOrder??original.Pool.Select(v=>v.Id).Order().ToArray(),p.Id)))
            for(int rotation=0;rotation<4;rotation++)
            foreach(var from in nodes.Where(n=>n.Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss)).OrderBy(n=>n.Id))
            foreach(var to in piece.Nodes.Where(n=>n.Room is not (JigsawRoom.Boss or JigsawRoom.SecondBoss)).OrderBy(n=>n.Id))
            foreach(var direction in new GridPoint[]{new(1,0),new(0,1),new(-1,0),new(0,-1)})
            {
                if(remaining--<=0)return true;
                var offset=to.Offset;for(int r=0;r<rotation;r++)offset=offset.Rotate();
                var anchor=new GridPoint(from.Center.X+direction.X*(from.Radius+2)-offset.X,from.Center.Y+direction.Y*(from.Radius+2)-offset.Y);
                var next=new JigsawLayout([..current.Placements,new(piece.Id,anchor,rotation)],[..current.Edges,new(from.Id,to.Id)]);
                var all=Nodes(original,next);
                var edges=original.Pool.Where(p=>next.Placements.Any(v=>v.PieceId==p.Id)).SelectMany(p=>p.InternalEdges).Concat(next.Edges).ToArray();
                if(JigsawGeometry.Validate(all,edges).Success&&Visit(next))return true;
            }
            return false;
        }
        Visit(start);
        return best;
    }
    private static JigsawNode[] Nodes(JigsawRun original,JigsawLayout layout) =>
        layout.Placements.SelectMany(p=>original.Pool.Single(v=>v.Id==p.PieceId).Nodes.Select(n=>
        {
            var point=n.Offset;for(int r=0;r<p.Rotation;r++)point=point.Rotate();
            return new JigsawNode(n.Id,p.PieceId,n.Room,point+p.Anchor);
        })).Prepend(new(0,null,JigsawRoom.Ancient,new(0,0))).ToArray();
    public static int[] ExplorationOrder(JigsawRun original,JigsawLayout layout)
    {
        if(!Validate(original,layout).Success)return [];
        var nodes=Nodes(original,layout);var boss=nodes.Single(n=>n.Room==JigsawRoom.Boss).Id;
        var blocked=nodes.Where(n=>n.Room==JigsawRoom.SecondBoss).Select(n=>n.Id).ToHashSet();
        var edges=original.Pool.Where(p=>layout.Placements.Any(v=>v.PieceId==p.Id)).SelectMany(p=>p.InternalEdges).Concat(layout.Edges).ToArray();
        int[] Neighbors(int id)=>edges.Where(e=>e.Touches(id)).Select(e=>e.First==id?e.Second:e.First).Where(n=>!blocked.Contains(n)).Distinct().Order().ToArray();
        var parent=new Dictionary<int,int>{{0,-1}};var queue=new Queue<int>();queue.Enqueue(0);
        while(queue.TryDequeue(out int id))foreach(int next in Neighbors(id))if(parent.TryAdd(next,id))queue.Enqueue(next);
        var path=new List<int>();for(int id=boss;id!=0;id=parent[id])path.Add(id);path.Reverse();path.RemoveAt(path.Count-1);
        var seen=path.Append(0).ToHashSet();
        while(path.Count<original.Budget-1)
        {
            int next=seen.Order().SelectMany(Neighbors).First(n=>n!=boss&&!seen.Contains(n));
            seen.Add(next);path.Add(next);
        }
        return [..path,boss];
    }
    public static JigsawLayout? Witness(JigsawRun original)
    {
        bool doubled=original.Pool.SelectMany(p=>p.Nodes).Any(n=>n.Room==JigsawRoom.SecondBoss);
        JigsawPlacement[] places = doubled
            ? [new(0,new(3,0),0),new(1,new(-3,0),2),new(2,new(-11,0),1),new(3,new(-15,0),2),new(4,new(7,2),1),new(5,new(3,-2),3)]
            : [new(0,new(3,0),0),new(1,new(-3,0),2),new(2,new(-11,0),1),new(3,new(-15,0),2),new(4,new(3,6),3),new(5,new(8,-2),2)];
        places[2]=new(2,new(-13,0),0);
        JigsawEdge[] edges=doubled
            ? [new(0,1),new(0,5),new(8,10),new(9,13),new(1,21),new(2,17)]
            : [new(0,1),new(0,5),new(8,10),new(9,13),new(2,22),new(1,19)];
        var layout=new JigsawLayout(places,edges);
        return Validate(original,layout).Success ? layout : null;
    }

    public static JigsawResult Validate(JigsawRun original,JigsawLayout layout)
    {
        try
        {
            if(layout.Placements==null || layout.Edges==null || layout.Placements.Any(p=>p==null) || layout.Edges.Any(e=>e==null))return JigsawResult.Reject("自动候选缺少完整布局或边。");
            if(layout.Placements.Any(p=>p.Rotation<0||p.Rotation>3) || layout.Placements.Select(p=>p.PieceId).Distinct().Count()!=layout.Placements.Length || layout.Edges.Select(e=>(Math.Min(e.First,e.Second),Math.Max(e.First,e.Second))).Distinct().Count()!=layout.Edges.Length)
                return JigsawResult.Reject("自动候选含非法旋转、重复碎块或重复边。");
            var candidate=new JigsawRun(original.Pool,original.Budget);
            foreach(var p in layout.Placements)
            {
                var take=candidate.Take(1,p.PieceId); if(!take.Success)return take;
                for(int r=0;r<p.Rotation;r++)candidate.Rotate(1);
                var drop=candidate.Drop(1,p.Anchor); if(!drop.Success)return drop;
            }
            foreach(var e in layout.Edges)
            {
                if(e.Internal)return JigsawResult.Reject("自动候选含无效外边。");
                var added=candidate.AddEdge(1,e.First,e.Second); if(!added.Success)return added;
            }
            candidate.ChooseEnd(1,EndChoice.Manual);
            var inspect=candidate.Inspect(); if(!inspect.Success)return inspect;
            if(candidate.Placements.Count!=layout.Placements.Length)return JigsawResult.Reject("自动候选含孤立碎块。");
            var used=layout.Placements.Select(p=>p.PieceId).ToHashSet();
            if(original.Pool.Any(p=>p.Nodes.Any(n=>n.Room==JigsawRoom.Rest)&&!used.Contains(p.Id)))return JigsawResult.Reject("自动候选缺少休息碎块。");
            var nodes=candidate.Nodes; var edges=candidate.Edges;
            if(nodes.Count(n=>n.Room is not (JigsawRoom.Ancient or JigsawRoom.Boss or JigsawRoom.SecondBoss))<original.Budget-1)return JigsawResult.Reject("自动候选没有满额度探索机会。");
            var boss=nodes.Single(n=>n.Room==JigsawRoom.Boss).PieceId;
            var owner=nodes.ToDictionary(n=>n.Id,n=>n.PieceId);
            bool direct=layout.Edges.Any(e=>e.First==0&&owner[e.Second]==boss||e.Second==0&&owner[e.First]==boss);
            var neighbors=layout.Edges.Where(e=>owner[e.First]==boss||owner[e.Second]==boss).Select(e=>owner[e.First]==boss?owner[e.Second]:owner[e.First]).Where(p=>p!=null&&p!=boss).Distinct().Count();
            if(direct&&neighbors<2)return JigsawResult.Reject("Boss碎块需要两个不同直连邻块。");
            return JigsawResult.Ok;
        }
        catch(Exception e) when(e is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return JigsawResult.Reject("无效自动候选："+e.Message); }
    }
}
