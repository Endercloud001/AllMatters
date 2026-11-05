namespace AllMatters.Jigsaw;

public readonly record struct GridPoint(int X, int Y)
{
    public static GridPoint operator +(GridPoint a, GridPoint b) => new(a.X + b.X, a.Y + b.Y);
    public GridPoint Rotate() => new(-Y, X);
    public static GridPoint Snap(double x, double y) => new((int)Math.Round(x, MidpointRounding.AwayFromZero), (int)Math.Round(y, MidpointRounding.AwayFromZero));
}
public enum JigsawRoom { Ancient, Combat, Unknown, Elite, Shop, Rest, Treasure, Boss, SecondBoss }
public enum JigsawShape { I, O, T, S, Z, J, L }
public enum JigsawPhase { Editing, Inspecting, Generating, Ancient, Exploring }
public enum EndChoice { Manual, Automatic }
public enum JigsawFailure
{
    None, RuntimeError, UnknownPlayer, PieceUnavailable, NoHeldPiece, CannotEditPaths, InternalPathFixed, ReservedPath, CannotSelectPaths, UnknownNode, EditIncomplete, InspectionUnauthorized, BossUnreachable, BossOverBudget, DisconnectedLayout, BossNotTerminal, BossBlocksOrdinaryRooms, NotEditable, NotAncient, AncientIncomplete, StaleEntry, StaleCompletion, InsufficientClearance, MissingEndpoint, MisalignedPath, InvalidInternalPath, InvalidExternalEndpoint, InvalidExternalGap, OccupiedPort, IntersectingPaths, PathThroughNode, TooManyPairPaths
}
public readonly record struct JigsawResult(bool Success, string Reason = "", JigsawFailure Failure = JigsawFailure.None)
{
    public static JigsawResult Ok => new(true);
    public static JigsawResult Reject(JigsawFailure failure) => new(false, failure.ToString(), failure);
    public static JigsawResult Reject(string detail) => new(false, detail, JigsawFailure.RuntimeError);
}
[method: System.Text.Json.Serialization.JsonConstructor]
public sealed record JigsawPieceNode(int Id, GridPoint Offset, JigsawRoom Room, JigsawRoom OriginalRoom)
{
    public JigsawPieceNode(int id, GridPoint offset, JigsawRoom room) : this(id, offset, room, room) { }
}
public sealed record JigsawEdge(int First, int Second, bool Internal = false)
{
    public bool Connects(int a, int b) => First == a && Second == b || First == b && Second == a;
    public bool Touches(int id) => First == id || Second == id;
}
public sealed record JigsawPiece(int Id, JigsawShape Shape, IReadOnlyList<JigsawPieceNode> Nodes, IReadOnlyList<JigsawEdge> InternalEdges);
public sealed record JigsawPlacement(int PieceId, GridPoint Anchor, int Rotation);
public sealed record JigsawNode(int Id, int? PieceId, JigsawRoom Room, GridPoint Center)
{
    public int Radius => Room is JigsawRoom.Ancient or JigsawRoom.Boss or JigsawRoom.SecondBoss ? 1 : 0;
}
public sealed record JigsawHolding(int PieceId, int Rotation);
public sealed record JigsawEnterAttempt(long Id, int NodeId);
public sealed record JigsawSave(JigsawPiece[] Pool, JigsawPlacement[] Placements, JigsawEdge[] ExternalEdges,
    [property: System.Text.Json.Serialization.JsonRequired] JigsawPhase Phase, int Budget, ulong[] Players, int Steps, int FirstProgress, int[] Visited, int[] Completed,
    int? CurrentNode, JigsawEnterAttempt? PendingEntry, long NextEntry, ulong[] AncientCompleted)
{
    public JigsawLayout? AutomaticWitness { get; init; }
    public JigsawAutomaticOperation? AutomaticOperation { get; init; }
    public ulong GenerationRandomState { get; init; } = 1;
}
public sealed record JigsawAutomaticOperation(string Algorithm,int ExpansionBudget,int[] CandidateOrder,ulong RandomState);
