using AllMatters.Jigsaw;

internal static class JigsawDomainTests
{
    public static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw automatic original pool supplies a six piece fallback", AutomaticWitness),
        ("Jigsaw automatic failure and authorized restore preserve original input", AutomaticTransaction),
        ("Jigsaw domain full pool quotas precede terminal Boss replacement", Pool),
        ("Jigsaw domain single and double Boss manual inspection accepts partial pool", Manual),
        ("Jigsaw domain reserved movement cancel four rotations and real move preserve transactions", Movement),
        ("Jigsaw domain global diagonal clearance and actual empty bounding regions", Geometry),
        ("Jigsaw domain explicit paths are idempotent and failed selection retains first endpoint", Paths),
        ("Jigsaw domain paths reject crossings and third connection between same pieces", PathGeometry),
        ("Jigsaw domain reset clears editing transients and preserves exact pool", Reset),
        ("Jigsaw domain vote changes, retractions and late actions follow acceptance order", VoteOrder),
        ("Jigsaw domain failed inspection retracts isolated pieces and clears votes", FailedInspection),
        ("Jigsaw domain exploration waits for success and counts each entry once", Exploration),
        ("Jigsaw domain absent Ancient recipient survives checkpoint restore", AbsentRecipient),
        ("Jigsaw domain disconnect releases final Ancient continuation", DisconnectContinuation),
        ("Jigsaw domain disconnect clears only the departed editor state", DisconnectEditorState),
        ("Jigsaw domain save copy cancels preview without mutating live holding", Save),
        ("Jigsaw domain restore rejects malformed pool layout and exploration state", InvalidSave),
        ("Jigsaw domain shop revisit increments steps but not first progress", ShopRevisit),
        ("Jigsaw domain movement respects adjacency and forbids unexplored crossings", MovementBoundaries),
        ("Jigsaw domain multi-player completion requires all entries confirmed", MultiPlayerCompletion),
        ("Jigsaw domain save and restore preserve visit history without duplication", SaveRestoreVisitHistory),
        ("Jigsaw domain shop inventory tracks per node and player across revisits", ShopInventoryTracking),
        ("Jigsaw domain shop transaction checkpoint rolls back on exit without commit", ShopTransactionRollback),
        ("Jigsaw domain shop transaction checkpoint commits on room completion", ShopTransactionCommit),
    ];
    private static void Check(bool value,string text) => NativeEventTests.Check(value,text);
    private static Task AutomaticWitness()
    {
        foreach(bool doubled in new[]{false,true}) foreach(int seed in new[]{0,75,9999}) foreach(int budget in new[]{13,14,15})
        {
            var run=JigsawRun.Create(seed,budget,doubled);
            var given=new JigsawRun(run.Pool,15);
            Check(JigsawAutomatic.Solve(given,0)==null && JigsawAutomatic.Solve(given,1)==null,"An unwitnessed given pool stays UNKNOWN at zero or exhausted expansion budget.");
            var witness=JigsawAutomatic.Witness(run);
            Check(witness!=null && witness.Placements.Length==6,
                "The original pool must supply an independently accepted six-piece fallback.");
        }
        return Task.CompletedTask;
    }
    private static Task AutomaticTransaction()
    {
        var shared=JigsawRun.Create(75,players:[1UL,2UL]);
        Ok(shared.ChooseEnd(1,EndChoice.Automatic));Ok(shared.ChooseEnd(2,EndChoice.Automatic));
        var authorized=shared.AutomaticOperation!;shared.Disconnect(2);
        Check(shared.Phase==JigsawPhase.Generating&&ReferenceEquals(authorized,shared.AutomaticOperation),"Disconnect cannot revoke a fully authorized operation.");
        Ok(shared.FinishAutomatic(authorized,JigsawAutomatic.Solve(shared,0)));
        foreach(bool doubled in new[]{false,true})foreach(int seed in new[]{0,75,9999})
        {
            var run=JigsawRun.Create(seed,15,doubled);
            Place(run,0,3,0);Ok(run.AddEdge(1,0,1));
            var before=run.CreateSave();
            string original=System.Text.Json.JsonSerializer.Serialize(before);
            Ok(run.ChooseEnd(1,EndChoice.Automatic));
            var saved=System.Text.Json.JsonSerializer.Deserialize<JigsawSave>(System.Text.Json.JsonSerializer.Serialize(run.CreateSave()))!;
            var restored=JigsawRun.Restore(saved);
            Check(restored.Phase==JigsawPhase.Generating && restored.AutomaticOperation!.CandidateOrder.SequenceEqual(run.AutomaticOperation!.CandidateOrder) && restored.AutomaticOperation.RandomState==run.AutomaticOperation.RandomState,"Authorized operation preserves exact random state and candidate order across a new object graph.");
            var candidate=JigsawAutomatic.Solve(restored,0);
            Check(candidate!=null,"Zero expansions retain a qualified same-pool fallback.");
            var order=JigsawAutomatic.ExplorationOrder(restored,candidate!);
            Check(order.Length==15 && order.Distinct().Count()==15,"Executable full-budget sequence contains fifteen distinct entries.");
            var explored=JigsawRun.Restore(restored.CreateSave());
            Ok(explored.FinishAutomatic(explored.AutomaticOperation!,candidate));Ok(explored.CompleteAncient(1));Ok(explored.Continue(1));
            foreach(int destination in order)
            {
                var attempt=explored.BeginEnter(destination);Check(attempt!=null,"Each generated entry is actually available from the completed region.");
                Ok(explored.ConfirmEnter(attempt!.Id));Ok(explored.CompleteRoom(attempt.Id));
            }
            Check(explored.Steps==15 && explored.Nodes.Single(n=>n.Id==order[^1]).Room==JigsawRoom.Boss,"The first Boss is entered on the budget's final step.");
            var badRotation=candidate! with { Placements=candidate.Placements.Select((p,i)=>i==0?p with {Rotation=-1}:p).ToArray() };
            Check(!JigsawAutomatic.Validate(restored,badRotation).Success,"Independent acceptance rejects negative rotations rather than normalizing them.");
            Check(!JigsawAutomatic.Validate(restored,candidate with { Edges=[..candidate.Edges,candidate.Edges[0]] }).Success,"Independent acceptance rejects duplicate edges before committing the original candidate.");
            Check(!restored.FinishAutomatic(run.AutomaticOperation!,candidate).Success,"An old execution cannot commit into the restored run.");
            Ok(restored.FinishAutomatic(restored.AutomaticOperation!,candidate));
            var success=JigsawRun.Restore(restored.CreateSave());
            Check(success.Phase==JigsawPhase.Ancient && success.Placements.SequenceEqual(restored.Placements),"Successful checkpoint restores the exact accepted map.");
            Check(!run.FinishAutomatic(run.AutomaticOperation!,new([],[])).Success,"Bad candidate is rejected independently.");
            Check(run.Phase==JigsawPhase.Editing && run.Choices.Count==0 && System.Text.Json.JsonSerializer.Serialize(run.CreateSave() with {AutomaticOperation=null,GenerationRandomState=before.GenerationRandomState})==original,"Rejected output preserves the original pool, placements and edges and clears votes.");
        }
        return Task.CompletedTask;
    }
    private static void Ok(JigsawResult result) => Check(result.Success,result.Reason);
    private static JigsawPiece Piece(int id,params (int X,int Y,JigsawRoom Room)[] nodes) => new(id,JigsawShape.I,nodes.Select((n,i) => new JigsawPieceNode(id*10+i+1,new(n.X,n.Y),n.Room)).ToArray(),[]);
    private static JigsawRun Fixture(params JigsawPiece[] pieces) => new(pieces,15,[1UL,2UL]);
    private static void Place(JigsawRun run,int piece,int x,int y) { Ok(run.Take(1,piece)); Ok(run.Drop(1,new(x,y))); }
    private static Task Pool()
    {
        foreach (bool doubled in new[] {false,true}) foreach (int seed in new[] {0,74,593,9999})
        {
            var run = JigsawRun.Create(seed,doubleBoss:doubled);
            Check(run.Pool.Count == 14 && run.Pool.GroupBy(p => p.Shape).All(g => g.Count()==2),"Seven shapes, twice each.");
            var before = run.Pool.SelectMany(p => p.Nodes).GroupBy(n => n.OriginalRoom).ToDictionary(g => g.Key,g => g.Count());
            Check(before[JigsawRoom.Combat]==20 && before[JigsawRoom.Unknown]==16 && before[JigsawRoom.Elite]==6 && before[JigsawRoom.Shop]==4 && before[JigsawRoom.Rest]==6 && before[JigsawRoom.Treasure]==4,"Full quota is assigned before replacement.");
            foreach(var piece in run.Pool)
            {
                Check(piece.Nodes.Count(n => n.OriginalRoom==JigsawRoom.Rest)<=1 && piece.Nodes.Select(n=>n.OriginalRoom).Distinct().Count()>1,"No duplicate rest or homogeneous piece before replacement.");
                foreach(var edge in piece.InternalEdges)
                {
                    var a=piece.Nodes.Single(n=>n.Id==edge.First).OriginalRoom; var b=piece.Nodes.Single(n=>n.Id==edge.Second).OriginalRoom;
                    Check(a!=b || a is not (JigsawRoom.Elite or JigsawRoom.Shop or JigsawRoom.Rest or JigsawRoom.Treasure),"Forbidden identical internal neighbors.");
                }
            }
            Check(run.Pool.SelectMany(p=>p.Nodes).Count(n=>n.Room is JigsawRoom.Boss or JigsawRoom.SecondBoss)==(doubled?2:1),"Boss count.");
        }
        return Task.CompletedTask;
    }
    private static JigsawRun Ready(bool doubled=false,int budget=15)
    {
        var run=JigsawRun.Create(74,budget,doubled); Place(run,0,3,0); Ok(run.AddEdge(1,0,1)); Ok(run.ChooseEnd(1,EndChoice.Manual)); Ok(run.Inspect()); return run;
    }
    private static Task Manual()
    {
        foreach(bool doubled in new[]{false,true})
        {
            var run=Ready(doubled,doubled?3:4); Check(run.Phase==JigsawPhase.Ancient && run.Placements.Count==1,"Manual needs neither full pool nor rest block nor full-budget detour.");
            Ok(run.CompleteAncient(1)); Ok(run.Continue(1)); Check(run.Phase==JigsawPhase.Exploring && run.Steps==0,"Ancient does not consume steps.");
        }
        return Task.CompletedTask;
    }
    private static Task Movement()
    {
        var run=Fixture(Piece(1,(0,0,JigsawRoom.Combat)),Piece(2,(0,0,JigsawRoom.Unknown)));
        Place(run,1,3,0); Place(run,2,5,0); Ok(run.AddEdge(1,0,11)); Ok(run.AddEdge(1,11,21));
        Ok(run.Take(1,1)); Check(!run.Take(2,2).Success,"Neighbor of reserved piece is locked.");
        Check(!run.RemoveEdge(2,11,21).Success,"Reserved edge cannot change.");
        for(int i=0;i<4;i++) Ok(run.Rotate(1)); Ok(run.Drop(1,new(3,0))); Check(run.Edges.Count==2,"Four turns retain links.");
        Ok(run.Take(1,1)); Check(!run.Drop(1,new(4,0)).Success && run.Holdings.Count==1,"Invalid drop remains held."); Ok(run.Cancel(1)); Check(run.Edges.Count==2,"Cancel retains all edges.");
        Ok(run.Take(1,1)); Ok(run.Drop(1,new(3,4))); Check(run.Edges.Count==0,"A successful real move removes exactly associated links.");
        return Task.CompletedTask;
    }
    private static Task Geometry()
    {
        var run=Fixture(Piece(1,(0,0,JigsawRoom.Combat),(4,0,JigsawRoom.Unknown),(0,4,JigsawRoom.Elite)),Piece(2,(0,0,JigsawRoom.Shop)));
        Place(run,1,4,4); Ok(run.Take(1,2)); Check(!run.Drop(1,new(5,5)).Success,"Diagonal adjacent centers violate global clearance."); Ok(run.Drop(1,new(6,6))); Check(run.Nodes.Count==5,"Actual empty space inside another piece bounding box remains usable.");
        Check(GridPoint.Snap(-0.5,0.5)==new GridPoint(-1,1),"Nearest integer snapping handles negative ties consistently.");
        var rotate=Fixture(Piece(1,(0,0,JigsawRoom.Combat),(2,0,JigsawRoom.Unknown))); Ok(rotate.Take(1,1)); Ok(rotate.Rotate(1)); Ok(rotate.Drop(1,new(4,4))); Check(rotate.Nodes.Single(n=>n.Id==12).Center==new GridPoint(4,6),"CCW rotation preserves first node anchor.");
        return Task.CompletedTask;
    }
    private static Task Paths()
    {
        var run=Fixture(Piece(1,(0,0,JigsawRoom.Combat)),Piece(2,(0,0,JigsawRoom.Unknown))); Place(run,1,3,0); Place(run,2,5,0);
        Ok(run.AddEdge(1,11,21)); Ok(run.AddEdge(2,11,21)); Check(run.Edges.Count==1,"Repeated add stays add."); Ok(run.RemoveEdge(1,11,21)); Ok(run.RemoveEdge(2,11,21)); Check(run.Edges.Count==0,"Repeated delete stays delete.");
        Ok(run.SelectEndpoint(1,0)); Ok(run.SelectEndpoint(1,null)); Check(run.SelectedEndpoints[1]==0,"One blank preserves first endpoint.");
        Check(!run.SelectEndpoint(1,21).Success && run.SelectedEndpoints[1]==0,"Failed link retains first endpoint."); Ok(run.SelectEndpoint(1,null)); Ok(run.SelectEndpoint(1,null)); Check(run.SelectedEndpoints.Count==0,"Two consecutive blanks clear selection.");
        Ok(run.SelectEndpoint(1,11)); Ok(run.SelectEndpoint(1,11)); Check(run.SelectedEndpoints.Count==0,"Same endpoint clears selection.");
        Ok(run.SelectEndpoint(1,11)); Ok(run.AddEdge(2,11,21));
        Ok(run.SelectEndpoint(1,21,desiredAdd:true));
        Check(run.Edges.Count==1,"A delayed add intention cannot delete an edge added by another player.");
        Ok(run.SelectEndpoint(1,11)); Ok(run.RemoveEdge(2,11,21));
        Ok(run.SelectEndpoint(1,21,desiredAdd:false));
        Check(run.Edges.Count==0,"A delayed delete intention cannot recreate an edge removed by another player.");
        var internalRun=JigsawRun.Create(74); Place(internalRun,0,3,0); Check(!internalRun.RemoveEdge(1,1,2).Success,"Internal paths cannot be deleted.");
        return Task.CompletedTask;
    }
    private static Task Reset()
    {
        var run=JigsawRun.Create(74,players:[1UL,2UL]); var pool=run.Pool; Place(run,0,3,0); Ok(run.AddEdge(1,0,1)); Ok(run.ChooseEnd(1,EndChoice.Manual)); Ok(run.Take(2,1));
        Ok(run.ResetLayout()); Ok(run.ResetLayout()); Check(ReferenceEquals(pool,run.Pool) && run.Nodes.Count==1 && run.Edges.Count==0 && run.Holdings.Count==0 && run.Choices.Count==0,"Reset returns same original pool and clears all edit state.");
        var authorized=Ready(); var save=authorized.CreateSave(); Check(!authorized.ResetLayout().Success && authorized.Placements.SequenceEqual(save.Placements),"Authorized end rejects reset without mutation."); return Task.CompletedTask;
    }
    private static Task VoteOrder()
    {
        var run=JigsawRun.Create(86,players:[1UL,2UL]);
        Ok(run.ChooseEnd(1,EndChoice.Manual));
        Ok(run.Take(2,0));
        Check(run.Choices.TryGetValue(1,out var retained)&&retained==EndChoice.Manual,"Another player's edit does not clear the first player's vote.");
        Ok(run.Cancel(2));
        Ok(run.ChooseEnd(2,EndChoice.Automatic));
        Check(run.Phase==JigsawPhase.Editing,"Different buttons cannot authorize an end operation.");
        Ok(run.ChooseEnd(2,EndChoice.Manual));
        Check(run.Phase==JigsawPhase.Inspecting,"The last accepted matching vote authorizes manual inspection.");
        Check(!run.ChooseEnd(2,EndChoice.Manual).Success&&run.Phase==JigsawPhase.Inspecting,"A retraction ordered after the final vote is rejected as late.");

        var retracted=JigsawRun.Create(87,players:[1UL,2UL]);
        Ok(retracted.ChooseEnd(2,EndChoice.Manual));Ok(retracted.ChooseEnd(2,EndChoice.Manual));
        Ok(retracted.ChooseEnd(1,EndChoice.Manual));
        Check(retracted.Phase==JigsawPhase.Editing&&retracted.Choices.Count==1,"A retraction ordered before the other player's last vote prevents premature authorization.");
        Ok(retracted.ChooseEnd(2,EndChoice.Manual));
        Check(retracted.Phase==JigsawPhase.Inspecting,"The later matching vote authorizes exactly once.");
        return Task.CompletedTask;
    }
    private static Task PathGeometry()
    {
        var first=Piece(1,(0,0,JigsawRoom.Combat),(8,0,JigsawRoom.Unknown)) with { InternalEdges = new[]{new JigsawEdge(11,12,true)} };
        var second=Piece(2,(0,0,JigsawRoom.Combat),(0,2,JigsawRoom.Unknown)) with { InternalEdges = new[]{new JigsawEdge(21,22,true)} };
        var cross=Fixture(first,second); Place(cross,1,6,10); Ok(cross.Take(1,2)); Check(!cross.Drop(1,new(10,9)).Success && cross.Holdings.Count==1,"Internal paths cannot cross even with legal separated endpoints.");
        var pair=Fixture(Piece(1,(0,0,JigsawRoom.Combat),(0,4,JigsawRoom.Combat),(0,8,JigsawRoom.Combat)),Piece(2,(0,0,JigsawRoom.Combat),(0,4,JigsawRoom.Combat),(0,8,JigsawRoom.Combat)));
        Place(pair,1,4,4); Place(pair,2,6,4); Ok(pair.AddEdge(1,11,21)); Ok(pair.AddEdge(1,12,22)); Check(!pair.AddEdge(1,13,23).Success && pair.Edges.Count==2,"A third connection between the same pair is rejected.");
        var ports=Fixture(Piece(1,(0,0,JigsawRoom.Combat))); Place(ports,1,4,0); Check(!ports.AddEdge(1,0,11).Success,"Two empty cells cannot be joined by an external path.");
        return Task.CompletedTask;
    }
    private static Task FailedInspection()
    {
        var run=JigsawRun.Create(74); Place(run,1,20,20); Ok(run.ChooseEnd(1,EndChoice.Manual)); Check(!run.Inspect().Success,"Absent Boss fails manual check."); Check(run.Placements.Count==0 && run.Pool.Count==14 && run.Phase==JigsawPhase.Editing && run.Choices.Count==0,"Failed inspection preserves retraction and original pool, clears vote.");
        var tooShort=JigsawRun.Create(74,3); Place(tooShort,0,3,0); Ok(tooShort.AddEdge(1,0,1)); Ok(tooShort.ChooseEnd(1,EndChoice.Manual)); Check(!tooShort.Inspect().Success && tooShort.Placements.Count==1,"Connected over-budget layout remains after failure."); return Task.CompletedTask;
    }
    private static Task Exploration()
    {
        var run=Ready(); Ok(run.CompleteAncient(1)); Ok(run.Continue(1)); Check(run.AvailableDestinations.SequenceEqual(new[]{1}),"Unexplored node blocks destination beyond it.");
        var first=run.BeginEnter(1)!; Check(run.Steps==0 && run.PendingEntry==first,"Loading without confirmation consumes nothing and preserves retry destination."); Ok(run.AbandonEntry(first.Id)); Check(!run.ConfirmEnter(first.Id).Success && run.Steps==0,"Abandoned attempt cannot confirm.");
        var next=run.BeginEnter(1)!; Ok(run.ConfirmEnter(next.Id)); Check(!run.ConfirmEnter(next.Id).Success && run.Steps==1 && run.FirstProgress==1,"Duplicate confirmed entry counts once."); Check(run.BeginEnter(2)==null,"Incomplete room cannot advance."); Ok(run.CompleteRoom(next.Id)); Check(!run.CompleteRoom(next.Id).Success && run.AvailableDestinations.Contains(2),"Completion exposes next room exactly once."); return Task.CompletedTask;
    }
    private static Task Save()
    {
        var run=JigsawRun.Create(74); Place(run,0,3,0); Ok(run.AddEdge(1,0,1)); Ok(run.Take(1,0)); Ok(run.Rotate(1));
        var save=run.CreateSave(); Check(run.Holdings.Count==1 && run.Holdings[1].Rotation==1,"Capturing cancelled preview copy leaves live holder untouched."); var restored=JigsawRun.Restore(save); Check(restored.Holdings.Count==0 && restored.Placements.Single().Rotation==0 && restored.Edges.Count==4,"Restoration uses committed orientation and all retained links.");
        ((JigsawPieceNode[])save.Pool[0].Nodes)[0] = new JigsawPieceNode(999,new(0,0),JigsawRoom.Rest);
        Check(run.Pool[0].Nodes[0].Id==1,"Save DTO has its own collection copy."); return Task.CompletedTask;
    }
    private static Task DisconnectContinuation()
    {
        var run=JigsawRun.Create(80,players:[1UL,2UL]);
        Ok(run.ChooseEnd(1,EndChoice.Automatic));Ok(run.ChooseEnd(2,EndChoice.Automatic));
        Ok(run.FinishAutomatic(run.AutomaticOperation!,JigsawAutomatic.Solve(run,0)));
        Ok(run.CompleteAncient(1));Ok(run.CompleteAncient(2));Ok(run.Continue(1));
        Check(run.Phase==JigsawPhase.Ancient,"One remaining continuation keeps the common map waiting.");
        run.Disconnect(2);
        Check(run.Phase==JigsawPhase.Exploring,"Disconnecting the final noncontinued player must release the already unanimous continuation.");
        Check(run.CreateSave().AncientCompleted.Contains(2UL),"Disconnect preserves the departed player's personal reward completion.");
        return Task.CompletedTask;
    }
    private static Task DisconnectEditorState()
    {
        var run=JigsawRun.Create(81,players:[1UL,2UL,3UL]);
        Ok(run.ChooseEnd(2,EndChoice.Manual));
        Ok(run.Take(3,0));
        run.Disconnect(3);
        Check(!run.Holdings.ContainsKey(3)&&run.ActivePlayers.SequenceEqual(new[]{1UL,2UL}),"A departed holder releases its piece and leaves the active roster.");
        Check(run.Choices.TryGetValue(2,out var retained)&&retained==EndChoice.Manual,"Disconnect leaves another active player's accepted vote intact.");
        var endpoint=JigsawRun.Create(82,players:[1UL,2UL]);
        Ok(endpoint.SelectEndpoint(2,0));
        Ok(endpoint.ChooseEnd(1,EndChoice.Manual));
        endpoint.Disconnect(2);
        Check(!endpoint.SelectedEndpoints.ContainsKey(2)&&!endpoint.Choices.ContainsKey(2),"A departed editor loses its temporary endpoint and vote.");
        Check(endpoint.Phase==JigsawPhase.Inspecting,"Removing the last required editor re-evaluates the remaining accepted vote.");
        return Task.CompletedTask;
    }
    private static Task AbsentRecipient()
    {
        var run=JigsawRun.Create(74,players:[1UL,2UL]);Place(run,0,3,0);Ok(run.AddEdge(1,0,1));
        Ok(run.ChooseEnd(1,EndChoice.Manual));Ok(run.ChooseEnd(2,EndChoice.Manual));Ok(run.Inspect());
        Ok(run.CompleteAncient(2));run.Disconnect(2);
        var saved=run.CreateSave();var restored=JigsawRun.Restore(saved);
        Check(saved.OriginalPlayers!.Order().SequenceEqual(new[]{1UL,2UL}),"Disconnect preserves the original saved roster.");
        Check(saved.Players.SequenceEqual(new[]{1UL})&&restored.CreateSave().AncientCompleted.Contains(2UL),"The inactive recipient is excluded from current decisions but keeps saved completion.");
        Check(!restored.CompleteAncient(2).Success,"An absent original player cannot submit a live completion.");
        var late=JigsawRun.Restore(saved with {AncientCompleted=[]});
        Check(!late.CompleteAncient(2).Success,"Absent owner cannot submit ordinary completion.");
        Ok(late.RecordNativeAncientCompletion(2));
        Check(late.CreateSave().AncientCompleted.SequenceEqual(new[]{2UL}),"Trusted native fact preserves genuine late completion for an absent original owner.");
        Check(!late.RecordNativeAncientCompletion(3).Success,"Trusted native facts still reject owners outside the original roster.");
        var rejoined=JigsawRun.Restore(saved,[1UL,2UL]);
        Check(rejoined.ActivePlayers.Order().SequenceEqual(new[]{1UL,2UL})&&rejoined.CreateSave().AncientCompleted.Contains(2UL),"A new lobby session reselects active original players without replaying completed rewards.");
        Ok(rejoined.CompleteAncient(1));Ok(rejoined.Continue(1));
        Check(rejoined.Phase==JigsawPhase.Ancient,"A rejoined original participant must submit a fresh continuation.");
        Ok(rejoined.Continue(2));Check(rejoined.Phase==JigsawPhase.Exploring,"The rejoined roster can finish continuation without another reward completion.");
        bool stranger=false;try {JigsawRun.Restore(saved,[1UL,3UL]);}catch(ArgumentException){stranger=true;}
        Check(stranger,"A new player cannot take over an absent saved role.");
        var legacy=Ready().CreateSave() with {OriginalPlayers=null};
        Check(JigsawRun.Restore(legacy).OriginalPlayers.SequenceEqual(new[]{1UL}),"Old single-player checkpoints retain their saved roster without migration guesses.");
        Ok(restored.CompleteAncient(1));Ok(restored.Continue(1));
        Check(restored.Phase==JigsawPhase.Exploring&&restored.Steps==0,"Only currently active players are required to continue.");
        return Task.CompletedTask;
    }

    private static Task InvalidSave()
    {
        var save=JigsawRun.Create(74).CreateSave();
        void Reject(JigsawSave broken)
        {
            bool rejected=false; try { JigsawRun.Restore(broken); } catch(ArgumentException) { rejected=true; }
            Check(rejected,"Malformed checkpoints must be rejected rather than silently normalized.");
        }
        Reject(save with { Pool=save.Pool.Skip(1).ToArray() });
        Reject(save with { Placements=[new JigsawPlacement(999,new(10,10),0)] });
        Reject(save with { FirstProgress=2,Steps=1 });
        Reject(save with { Visited=[0,999] });
        Reject(save with { Phase=JigsawPhase.Exploring });
        return Task.CompletedTask;
    }

    private static Task ShopRevisit()
    {
        // Test: Shop revisit increments Steps but not FirstProgress
        var piece1 = Piece(1, (0,0,JigsawRoom.Combat), (1,0,JigsawRoom.Shop));
        var piece2 = Piece(2, (0,0,JigsawRoom.Combat));
        var run = Fixture(piece1, piece2);
        Place(run, 1, 3, 0); Place(run, 2, 5, 0);
        Ok(run.AddEdge(1, 0, 11)); Ok(run.AddEdge(1, 12, 21));
        Ok(run.ChooseEnd(1, EndChoice.Manual)); Ok(run.ChooseEnd(2, EndChoice.Manual));
        Ok(run.Inspect());

        Ok(run.CompleteAncient(1)); Ok(run.CompleteAncient(2));
        Ok(run.Continue(1)); Ok(run.Continue(2));

        // First visit to shop
        var firstShop = run.BeginEnter(12)!;
        Ok(run.ConfirmEnter(firstShop.Id));
        Check(run.Steps == 1 && run.FirstProgress == 1, "First shop visit increments both Steps and FirstProgress.");
        Ok(run.CompleteRoom(firstShop.Id));

        // Visit combat node
        var combat = run.BeginEnter(21)!;
        Ok(run.ConfirmEnter(combat.Id));
        Check(run.Steps == 2 && run.FirstProgress == 2, "Combat visit increments both counters.");
        Ok(run.CompleteRoom(combat.Id));

        // Revisit shop
        Check(run.AvailableDestinations.Contains(12), "Shop is available for revisit.");
        var revisitShop = run.BeginEnter(12)!;
        Ok(run.ConfirmEnter(revisitShop.Id));
        Check(run.Steps == 3 && run.FirstProgress == 2, "Shop revisit increments Steps but not FirstProgress.");
        Check(revisitShop.Id != firstShop.Id, "Revisit creates new entry identity.");
        Ok(run.CompleteRoom(revisitShop.Id));

        return Task.CompletedTask;
    }

    private static Task MovementBoundaries()
    {
        // Test: 1-2-3 adjacency, 1-4 diagonal optional, cannot cross unexplored
        var piece1 = Piece(1, (0,0,JigsawRoom.Combat), (1,0,JigsawRoom.Combat), (2,0,JigsawRoom.Combat));
        var piece2 = Piece(2, (0,0,JigsawRoom.Combat), (0,1,JigsawRoom.Combat));
        var run = Fixture(piece1, piece2);
        Place(run, 1, 3, 0); Place(run, 2, 5, 1);
        Ok(run.AddEdge(1, 0, 11));

        // Don't connect 13 to 21, leaving 21 unreachable directly
        Ok(run.ChooseEnd(1, EndChoice.Manual)); Ok(run.ChooseEnd(2, EndChoice.Manual));
        Ok(run.Inspect());

        Ok(run.CompleteAncient(1)); Ok(run.CompleteAncient(2));
        Ok(run.Continue(1)); Ok(run.Continue(2));

        // Can reach adjacent nodes
        Check(run.AvailableDestinations.Contains(11), "Adjacent node is available.");
        var first = run.BeginEnter(11)!;
        Ok(run.ConfirmEnter(first.Id));
        Ok(run.CompleteRoom(first.Id));

        Check(run.AvailableDestinations.Contains(12), "Next adjacent node available from completed.");
        var second = run.BeginEnter(12)!;
        Ok(run.ConfirmEnter(second.Id));
        Ok(run.CompleteRoom(second.Id));

        // Node 21 is not reachable because there's no path from completed region
        Check(!run.AvailableDestinations.Contains(21), "Unconnected node cannot be reached.");
        Check(run.BeginEnter(21) == null, "Cannot begin entry to unreachable node.");

        return Task.CompletedTask;
    }

    private static Task MultiPlayerCompletion()
    {
        // Test: Multi-player requires all to confirm entry before advancing
        var piece1 = Piece(1, (0,0,JigsawRoom.Combat), (1,0,JigsawRoom.Combat));
        var run = new JigsawRun([piece1], 15, [1UL, 2UL]);
        Place(run, 1, 3, 0);
        Ok(run.AddEdge(1, 0, 11));
        Ok(run.ChooseEnd(1, EndChoice.Manual)); Ok(run.ChooseEnd(2, EndChoice.Manual));
        Ok(run.Inspect());

        Ok(run.CompleteAncient(1)); Ok(run.CompleteAncient(2));
        Ok(run.Continue(1)); Ok(run.Continue(2));

        // Both players must coordinate entry
        var entry = run.BeginEnter(11)!;
        Check(run.CurrentNode == null, "Entry not confirmed until ConfirmEnter.");

        Ok(run.ConfirmEnter(entry.Id));
        Check(run.CurrentNode == 11 && run.Steps == 1, "Entry confirmed increments steps once.");

        // Completion binding to this entry
        Ok(run.CompleteRoom(entry.Id));
        Check(run.CurrentNode == null, "Completion clears current node.");

        // Second entry to same node should work
        var secondEntry = run.BeginEnter(12)!;
        Ok(run.ConfirmEnter(secondEntry.Id));
        Check(run.Steps == 2, "Second entry increments steps.");

        return Task.CompletedTask;
    }

    private static Task SaveRestoreVisitHistory()
    {
        // Test: Save/restore preserves visit history without duplication
        var piece1 = Piece(1, (0,0,JigsawRoom.Combat), (1,0,JigsawRoom.Shop), (2,0,JigsawRoom.Combat));
        var run = Fixture(piece1);
        Place(run, 1, 3, 0);
        Ok(run.AddEdge(1, 0, 11));
        Ok(run.ChooseEnd(1, EndChoice.Manual)); Ok(run.ChooseEnd(2, EndChoice.Manual));
        Ok(run.Inspect());

        Ok(run.CompleteAncient(1)); Ok(run.CompleteAncient(2));
        Ok(run.Continue(1)); Ok(run.Continue(2));

        // Visit nodes
        var e1 = run.BeginEnter(11)!; Ok(run.ConfirmEnter(e1.Id)); Ok(run.CompleteRoom(e1.Id));
        var e2 = run.BeginEnter(12)!; Ok(run.ConfirmEnter(e2.Id));

        // Save while in shop
        var save1 = run.CreateSave();
        Check(save1.Steps == 2 && save1.FirstProgress == 2, "Save captures current progress.");
        Check(save1.Visited.Contains(11) && save1.Visited.Contains(12), "Save captures visited nodes.");
        Check(save1.CurrentNode == 12, "Save captures current location.");

        // Complete and revisit
        Ok(run.CompleteRoom(e2.Id));
        var e3 = run.BeginEnter(12)!; Ok(run.ConfirmEnter(e3.Id));

        // Save after revisit
        var save2 = run.CreateSave();
        Check(save2.Steps == 3 && save2.FirstProgress == 2, "Revisit increments Steps only.");

        // Restore from first save and verify no duplicate rewards
        var restored = JigsawRun.Restore(save1);
        Check(restored.Steps == 2 && restored.FirstProgress == 2, "Restored progress matches saved.");
        Check(restored.Visited.Contains(11) && restored.Visited.Contains(12), "Restored visited history intact.");
        Check(restored.CurrentNode == 12, "Restored to saved location.");

        // Complete from restored state
        Ok(restored.CompleteRoom(e2.Id));
        var e4 = restored.BeginEnter(13)!; Ok(restored.ConfirmEnter(e4.Id));
        Check(restored.Steps == 3 && restored.FirstProgress == 3, "New node after restore increments FirstProgress.");

        return Task.CompletedTask;
    }

    private static Task ShopInventoryTracking()
    {
        // Test: Shop inventory tracked per node and player, persists across save/restore
        var piece1 = Piece(1, (0,0,JigsawRoom.Shop), (1,0,JigsawRoom.Shop));
        var piece2 = Piece(2, (0,0,JigsawRoom.Combat));
        var run = Fixture(piece1, piece2);
        Place(run, 1, 3, 0); Place(run, 2, 5, 0);
        Ok(run.AddEdge(1, 0, 11)); Ok(run.AddEdge(1, 12, 21));
        Ok(run.ChooseEnd(1, EndChoice.Manual)); Ok(run.ChooseEnd(2, EndChoice.Manual));
        Ok(run.Inspect());

        Ok(run.CompleteAncient(1)); Ok(run.CompleteAncient(2));
        Ok(run.Continue(1)); Ok(run.Continue(2));

        // Enter first shop for player 1
        var shop1Player1 = run.BeginEnter(11)!;
        Ok(run.ConfirmEnter(shop1Player1.Id));

        // Mark that player 1 has inventory state for shop node 11
        run.RecordShopState(11, 1, new { Item = "Potion", Price = 50 });
        Check(run.GetShopState(11, 1) != null, "Shop state recorded for player 1 at node 11.");

        Ok(run.CompleteRoom(shop1Player1.Id));

        // Enter second shop for player 1
        var shop2Player1 = run.BeginEnter(12)!;
        Ok(run.ConfirmEnter(shop2Player1.Id));

        // Different inventory for second shop
        run.RecordShopState(12, 1, new { Item = "Relic", Price = 150 });
        Check(run.GetShopState(12, 1) != null, "Shop state recorded for player 1 at node 12.");
        Check(run.GetShopState(11, 1) != null, "Previous shop state preserved.");

        Ok(run.CompleteRoom(shop2Player1.Id));

        // Revisit first shop - inventory should be retrievable
        var revisitShop1 = run.BeginEnter(11)!;
        Ok(run.ConfirmEnter(revisitShop1.Id));
        Check(run.GetShopState(11, 1) != null, "Shop state retrieved on revisit.");
        Ok(run.CompleteRoom(revisitShop1.Id));

        // Save and restore
        var save = run.CreateSave();
        var restored = JigsawRun.Restore(save);

        Check(restored.GetShopState(11, 1) != null, "Shop state for node 11 player 1 restored.");
        Check(restored.GetShopState(12, 1) != null, "Shop state for node 12 player 1 restored.");
        Check(restored.GetShopState(11, 2) == null, "No shop state for player 2 at node 11.");

        return Task.CompletedTask;
    }

    private static Task ShopTransactionRollback()
    {
        // Test: Transaction checkpoint rolls back on explicit rollback
        var piece1 = Piece(1, (0,0,JigsawRoom.Shop));
        var run = Fixture(piece1);
        Place(run, 1, 3, 0);
        Ok(run.AddEdge(1, 0, 11));
        Ok(run.ChooseEnd(1, EndChoice.Manual)); Ok(run.ChooseEnd(2, EndChoice.Manual));
        Ok(run.Inspect());

        Ok(run.CompleteAncient(1)); Ok(run.CompleteAncient(2));
        Ok(run.Continue(1)); Ok(run.Continue(2));

        // Enter shop
        var enter = run.BeginEnter(11)!;
        Ok(run.ConfirmEnter(enter.Id));

        // Create initial inventory
        var initialInventory = new JigsawShopInventory(
            [new JigsawShopCard("Strike", 0, false, null, 50)],
            [new JigsawShopRelic("Lantern", 0, null, 150)],
            0, true,
            new Dictionary<string, int> { ["Strike"] = 50, ["Lantern"] = 150 },
            new Dictionary<string, bool>(),
            []);

        run.RecordShopInventory(11, 1, initialInventory);

        // Create checkpoint before transaction
        run.CreateShopTransactionCheckpoint(11, 1);

        // Modify inventory (simulate purchase)
        var modifiedInventory = initialInventory with
        {
            SoldOut = new HashSet<string> { "Strike" },
            PurgeUsedCount = 1
        };
        run.RecordShopInventory(11, 1, modifiedInventory);

        // Rollback transaction (simulating exit during purchase)
        run.RollbackShopTransaction();

        // Verify inventory was restored to checkpoint
        var restored = run.GetShopInventory(11, 1);
        Check(restored != null && restored.PurgeUsedCount == 0, "Inventory rolled back to checkpoint state.");
        Check(restored != null && !restored.SoldOut.Contains("Strike"), "Sold out state rolled back.");

        Ok(run.CompleteRoom(enter.Id));
        return Task.CompletedTask;
    }

    private static Task ShopTransactionCommit()
    {
        // Test: Transaction checkpoint commits on room completion
        var piece1 = Piece(1, (0,0,JigsawRoom.Shop));
        var run = Fixture(piece1);
        Place(run, 1, 3, 0);
        Ok(run.AddEdge(1, 0, 11));
        Ok(run.ChooseEnd(1, EndChoice.Manual)); Ok(run.ChooseEnd(2, EndChoice.Manual));
        Ok(run.Inspect());

        Ok(run.CompleteAncient(1)); Ok(run.CompleteAncient(2));
        Ok(run.Continue(1)); Ok(run.Continue(2));

        // Enter shop
        var enter = run.BeginEnter(11)!;
        Ok(run.ConfirmEnter(enter.Id));

        // Create initial inventory
        var initialInventory = new JigsawShopInventory(
            [new JigsawShopCard("Strike", 0, false, null, 50)],
            [],
            0, true,
            new Dictionary<string, int> { ["Strike"] = 50 },
            new Dictionary<string, bool>(),
            []);

        run.RecordShopInventory(11, 1, initialInventory);

        // Create checkpoint
        run.CreateShopTransactionCheckpoint(11, 1);

        // Complete purchase
        var afterPurchase = initialInventory with
        {
            SoldOut = new HashSet<string> { "Strike" }
        };
        run.RecordShopInventory(11, 1, afterPurchase);

        // Complete room - should commit transaction
        Ok(run.CompleteRoom(enter.Id));

        // Verify inventory persisted after completion
        var final = run.GetShopInventory(11, 1);
        Check(final != null && final.SoldOut.Contains("Strike"), "Purchase committed on room completion.");

        // Save and restore to verify persistence
        var save = run.CreateSave();
        var restored = JigsawRun.Restore(save);
        var restoredInventory = restored.GetShopInventory(11, 1);
        Check(restoredInventory != null && restoredInventory.SoldOut.Contains("Strike"), "Committed purchase persists across save/restore.");

        return Task.CompletedTask;
    }

    private static JigsawRun Ready()
    {
        var run = JigsawRun.Create(74);
        Place(run, 0, 3, 0);
        Ok(run.AddEdge(1, 0, 1));
        Ok(run.ChooseEnd(1, EndChoice.Manual));
        Ok(run.Inspect());
        return run;
    }
}
