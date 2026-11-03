using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
// Original compile-only interface probe; not a registered or implemented game feature.
public sealed class DrawClassificationProbe : PowerModel {
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override bool ShouldDraw(Player player, bool fromHandDraw) => fromHandDraw;
}
