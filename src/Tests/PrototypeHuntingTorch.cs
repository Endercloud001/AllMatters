// Exact-species, throwaway attack-loop model. Author art stays in optional
// external PCK; this file has no borrowed AI, persistence or spawn coordinator.
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

#if HUNTING_QUEEN_MOD
public sealed class PrototypeHuntingLiveTorch : MonsterModel
#else
public sealed class PrototypeHuntingTorch : MonsterModel
#endif
{
    protected override string VisualsPath=>"res://ActsFromThePast/monsters/torch_head/torch_head.tscn";
    public override CreatureAnimator GenerateAnimator(MegaSprite controller)=>PrototypeHuntingVisualAnimator.Create(controller,"idle","halberd_slam","idle");
    public override LocString Title
    {
        get
        {
            // Probe-only text in the current in-memory table; never writes settings
            // or assets. New locale table gets its own entry on the next read.
            const string key="PROTOTYPE_HUNTING_TORCH.name";
            var loc=LocManager.Instance;var table=loc.GetTable("monsters");
            if(!table.IsLocalKey(key))table.MergeWith(new Dictionary<string,string>{{key,loc.Language=="zhs"?"火炬头（实验）":"Torch Head (prototype)"}});
            return new LocString("monsters",key);
        }
    }
    public override int MinInitialHp=>50;
    public override int MaxInitialHp=>50;
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var hit=new MoveState("HIT",async _=>await DamageCmd.Attack(10).FromMonster(this).Execute(null),new SingleAttackIntent(10));
        hit.FollowUpState=hit;return new MonsterMoveStateMachine([hit],hit);
    }
}
internal static class PrototypeHuntingVisualAnimator
{
    internal static CreatureAnimator Create(MegaSprite sprite,string idleName,string attackName,string hitName)
    {
        var idle=new AnimState(idleName,true);var attack=new AnimState(attackName){NextState=idle};var hit=new AnimState(hitName){NextState=idle};
        var animator=new CreatureAnimator(idle,sprite);animator.AddAnyState("Idle",idle);animator.AddAnyState("Attack",attack);animator.AddAnyState("Hit",hit);animator.AddAnyState("Cast",attack);return animator;
    }
}
