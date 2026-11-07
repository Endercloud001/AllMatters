namespace AllMatters.Jigsaw;

internal static class JigsawSaveValidation
{
    public static void Validate(JigsawSave save)
    {
        void Require(bool condition,string reason) { if(!condition) throw new ArgumentException(reason,nameof(save)); }
        Require(Enum.IsDefined(save.Phase),"Unknown phase.");
        if(save.AutomaticOperation!=null || save.Phase==JigsawPhase.Generating)
            Require(save.GenerationRandomState!=0 && save.AutomaticOperation is { Algorithm:JigsawAutomatic.Algorithm, ExpansionBudget:>=0, RandomState:>0 } operation && operation.CandidateOrder.Order().SequenceEqual(save.Pool.Select(p=>p.Id).Order()),"Unsupported or incomplete authorized generation input.");
        Require(save.Pool.Length==14 && Enum.GetValues<JigsawShape>().All(s=>save.Pool.Count(p=>p.Shape==s)==2),"Checkpoint must preserve the fourteen-piece pool.");
        Require(save.Pool.All(p=>p.Nodes.Count==4),"Each piece must have four nodes.");
        var all=save.Pool.SelectMany(p=>p.Nodes).ToArray();
        Require(save.Pool.Select(p=>p.Id).Distinct().Count()==14 && all.Select(n=>n.Id).Distinct().Count()==56 && all.All(n=>n.Id!=0),"Piece and node IDs must be unique.");
        var quota=new Dictionary<JigsawRoom,int> { [JigsawRoom.Combat]=20,[JigsawRoom.Unknown]=16,[JigsawRoom.Elite]=6,[JigsawRoom.Shop]=4,[JigsawRoom.Rest]=6,[JigsawRoom.Treasure]=4 };
        Require(all.All(n=>quota.ContainsKey(n.OriginalRoom)) && quota.All(q=>all.Count(n=>n.OriginalRoom==q.Key)==q.Value),"Original room quota is invalid.");
        Require(all.Count(n=>n.Room==JigsawRoom.Boss)==1 && all.Count(n=>n.Room==JigsawRoom.SecondBoss)<=1,"Invalid Boss count.");
        foreach(var piece in save.Pool)
        {
            var ids=piece.Nodes.Select(n=>n.Id).ToHashSet();
            Require(piece.Nodes[0].Offset==new GridPoint(0,0),"Rotation anchor must be the first fixed node.");
            Require(piece.Nodes.Count(n=>n.OriginalRoom==JigsawRoom.Rest)<=1 && piece.Nodes.Select(n=>n.OriginalRoom).Distinct().Count()>1,"Invalid original piece room allocation.");
            Require(piece.Nodes.All(n=>n.Room==n.OriginalRoom || n.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss && n.OriginalRoom is JigsawRoom.Combat or JigsawRoom.Unknown or JigsawRoom.Elite),"Illegal Boss replacement.");
            Require(piece.InternalEdges.All(e=>e.Internal && ids.Contains(e.First) && ids.Contains(e.Second) && e.First!=e.Second),"Invalid internal edge.");
            Require(piece.InternalEdges.Select(e=>(Math.Min(e.First,e.Second),Math.Max(e.First,e.Second))).Distinct().Count()==piece.InternalEdges.Count,"Duplicate internal edge.");
            foreach(var edge in piece.InternalEdges)
            {
                var a=piece.Nodes.Single(n=>n.Id==edge.First).OriginalRoom; var b=piece.Nodes.Single(n=>n.Id==edge.Second).OriginalRoom;
                Require(a!=b || a is not (JigsawRoom.Elite or JigsawRoom.Shop or JigsawRoom.Rest or JigsawRoom.Treasure),"Forbidden original internal neighbors.");
            }
            var seen=new HashSet<int>{piece.Nodes[0].Id};
            for(int i=0;i<4;i++) foreach(var e in piece.InternalEdges) if(seen.Contains(e.First)||seen.Contains(e.Second)) { seen.Add(e.First); seen.Add(e.Second); }
            Require(seen.Count==4,"Piece internal paths must be connected.");
            var geometry=JigsawGeometry.Validate(piece.Nodes.Select(n=>new JigsawNode(n.Id,piece.Id,n.Room,n.Offset)).ToArray(),piece.InternalEdges);
            Require(geometry.Success,geometry.Reason);
            int bossCount=piece.Nodes.Count(n=>n.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss);
            int spacing=bossCount==0?2:bossCount==1?3:4;
            Require(piece.InternalEdges.All(e=>
            {
                var a=piece.Nodes.Single(n=>n.Id==e.First).Offset; var b=piece.Nodes.Single(n=>n.Id==e.Second).Offset;
                return (a.X==b.X || a.Y==b.Y) && Math.Abs((long)a.X-b.X)+Math.Abs((long)a.Y-b.Y)==spacing;
            }),"Internal paths must follow adjacent skeleton cells at uniform piece spacing.");
        }
        var firstBoss=all.Single(n=>n.Room==JigsawRoom.Boss);
        var bossPiece=save.Pool.Single(p=>p.Nodes.Any(n=>n.Id==firstBoss.Id));
        var secondBoss=all.SingleOrDefault(n=>n.Room==JigsawRoom.SecondBoss);
        var bossEdges=bossPiece.InternalEdges.Where(e=>e.Touches(firstBoss.Id)).ToArray();
        Require(secondBoss==null ? bossEdges.Length==1 : bossPiece.Nodes.Contains(secondBoss) && bossEdges.Length==2 && bossEdges.Any(e=>e.Touches(secondBoss.Id)) && bossPiece.InternalEdges.Count(e=>e.Touches(secondBoss.Id))==1,"Boss pool must retain a terminal chain.");
        Require(save.Placements.All(p=>save.Pool.Any(piece=>piece.Id==p.PieceId) && p.Rotation>=0 && p.Rotation<4) && save.Placements.Select(p=>p.PieceId).Distinct().Count()==save.Placements.Length,"Invalid placement.");
        var placedIds=save.Pool.Where(p=>save.Placements.Any(v=>v.PieceId==p.Id)).SelectMany(p=>p.Nodes).Select(n=>n.Id).Append(0).ToHashSet();
        Require(save.Steps>=0 && save.FirstProgress>=0 && save.FirstProgress<=save.Steps && save.NextEntry>=save.Steps,"Invalid exploration progress.");
        Require(save.Visited.Contains(0) && save.Completed.Contains(0) && save.Visited.All(placedIds.Contains) && save.Completed.All(save.Visited.Contains),"Invalid explored node IDs.");
        Require(save.Visited.Distinct().Count()==save.Visited.Length && save.Completed.Distinct().Count()==save.Completed.Length && save.FirstProgress==save.Visited.Length-1,"Invalid first-entry progress.");
        Require(save.ExternalEdges.All(e=>!e.Internal),"External edge collection cannot contain internal paths.");
        Require(save.Players.Distinct().Count()==save.Players.Length && save.AncientCompleted.All(save.Players.Contains),"Invalid player completion.");
        Require(save.CurrentNode==null || save.Phase==JigsawPhase.Exploring && save.Visited.Contains(save.CurrentNode.Value) && save.CurrentNode!=0,"Invalid active room.");
        Require(save.PendingEntry==null || save.Phase==JigsawPhase.Exploring && save.CurrentNode==null && placedIds.Contains(save.PendingEntry.NodeId) && save.PendingEntry.NodeId!=0 && save.PendingEntry.Id>0 && save.PendingEntry.Id==save.NextEntry,"Invalid pending entry.");
        if(save.Phase==JigsawPhase.Exploring) Require(save.Players.All(save.AncientCompleted.Contains),"Exploration requires complete Ancient rewards.");
        if(save.Phase is JigsawPhase.Editing or JigsawPhase.Inspecting or JigsawPhase.Generating or JigsawPhase.Ancient) Require(save.Steps==0 && save.CurrentNode==null && save.PendingEntry==null,"Pre-exploration phase contains exploration state.");
    }
}
