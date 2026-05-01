namespace BossMod.Dawntrail.Dungeon.D13Clyteum.D132Chort;

public enum OID : uint
{
    Chort = 0x4C3F,
    Helper = 0x233C,
}

public enum AID : uint
{
    _AutoAttack_Attack = 45308, // 4C3F->player, no cast, single-target
    _Weaponskill_RipplesOfGloom = 48884, // 4C3F->self, 5.0s cast, single-target
    _Weaponskill_RipplesOfGloom1 = 50408, // 233C->self, 5.7s cast, range 40 circle
    _Weaponskill_ = 48888, // 4C3F->location, no cast, single-target
    _Weaponskill_MortifyingFlesh = 48871, // 4C3F->self, 5.0s cast, single-target //From inside arena: Rectangle across half arena
    _Weaponskill_MortifyingFlesh1 = 50400, // 233C->self, 3.0s cast, range 40 width 16 rect //From outside arena.  rect across whole arena
    _Weaponskill_BodyweightExorcism = 48877, // 4C3F->self, no cast, single-target
    _Weaponskill_MortifyingFlesh2 = 48876, // 233C->self, 3.0s cast, range 40 width 16 rect //From outside arena.  rect across whole arena
    _Weaponskill_BodyweightExorcism1 = 48878, // 233C->self, 2.8s cast, range 20 circle : knockback from center
    _Weaponskill_BasicVomit = 50361, // 4C3F->self, 5.0s cast, range 50 120.000-degree cone
    _Weaponskill_BodyweightExorcism2 = 48879, // 4C3F->location, 5.0s cast, single-target : 4 towers cast animation
    _Weaponskill_BodyweightExorcismTower = 48882, // 233C->location, 6.0s cast, range 4 circle : soak tower spawns
    _Weaponskill_BodyweightExorcism4 = 48880, // 4C3F->location, no cast, single-target
    _Weaponskill_BodyweightExorcism5 = 48881, // 4C3F->self, no cast, single-target
    _Weaponskill_MortifyingFlesh3 = 48869, // 4C3F->self, 5.0s cast, single-target
    _Weaponskill_MortifyingFlesh4 = 48872, // 4C3F->self, no cast, single-target
    _Weaponskill_EvilEmission = 50417, // 4C3F->self, 4.5s cast, single-target
    _Weaponskill_EvilEmission1 = 48885, // 233C->player, 5.0s cast, range 5 circle
    _Weaponskill_ProfanePressure = 48886, // 4C3F->self, 4.5s cast, single-target
    _Weaponskill_ProfanePressure1 = 48887, // 233C->players, 5.0s cast, range 6 circle : stack marker
}

public enum IconID : uint
{
    EvilEmissionSpread = 558, // player->self : spread marker
    ProfanePressureIcon = 161 // stack marker

}

sealed class MortifyingFlesh(BossModule module) : Components.SimpleAOEs(module, (uint)AID._Weaponskill_MortifyingFlesh, new AOEShapeRect(40f, 4f));
sealed class MortifyingFlesh1(BossModule module) : Components.SimpleAOEs(module, (uint)AID._Weaponskill_MortifyingFlesh1, new AOEShapeRect(40f, 7f));

sealed class MortifyingFlesh2(BossModule module) : Components.SimpleAOEs(module, (uint)AID._Weaponskill_MortifyingFlesh2, new AOEShapeRect(40f, 7f));

//TODO Would prefer if it moves automatically to where it needs to be. Shows indicator, doesn't seem to move.
sealed class BodyWeightExorcism1(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID._Weaponskill_BodyweightExorcism1, 11, true);

sealed class BasicVomit(BossModule module) : Components.SimpleAOEs(module, (uint)AID._Weaponskill_BasicVomit, new AOEShapeCone(50f, 60f.Degrees()));

//Naive implementation.  Just runs to nearest tower instead of stacking with group.  Seems to live fine.
sealed class BodyweightTower(BossModule module) : Components.CastTowers(module, (uint)AID._Weaponskill_BodyweightExorcismTower, 4f, 4, 4);

sealed class EvilEmission(BossModule module) : Components.SpreadFromIcon(module, (uint)IconID.EvilEmissionSpread, (uint)AID._Weaponskill_EvilEmission1, 4, 5);

sealed class ProfanePressure(BossModule module) : Components.StackWithIcon(module,(uint)IconID.ProfanePressureIcon, (uint)AID._Weaponskill_ProfanePressure, 4, 0);





[SkipLocalsInit]
sealed class D132ChortStates : StateMachineBuilder
{
    public D132ChortStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<MortifyingFlesh>()
            .ActivateOnEnter<MortifyingFlesh1>()
            .ActivateOnEnter<MortifyingFlesh2>()
            .ActivateOnEnter<BodyWeightExorcism1>()
            .ActivateOnEnter<BasicVomit>()
            .ActivateOnEnter<BodyweightTower>()
            .ActivateOnEnter<ProfanePressure>()
            .ActivateOnEnter<EvilEmission>()
            ;
    }

}

[ModuleInfo(BossModuleInfo.Maturity.WIP,
    StatesType = typeof(D132ChortStates),
    ConfigType = null, // replace null with typeof(ChortConfig) if applicable
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID), // replace null with typeof(AID) if applicable
    StatusIDType = null, // replace null with typeof(SID) if applicable
    TetherIDType = null, // replace null with typeof(TetherID) if applicable
    IconIDType = null, // replace null with typeof(IconID) if applicable
    PrimaryActorOID = (uint)OID.Chort,
    Contributors = "Wen",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Dungeon,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1011u,
    NameID = 14734u,
    SortOrder = 1,
    PlanLevel = 0)]
[SkipLocalsInit]
public sealed class Chort(WorldState ws, Actor primary) : BossModule(ws, primary, new(660f, -142f), new ArenaBoundsCircle(16f));
