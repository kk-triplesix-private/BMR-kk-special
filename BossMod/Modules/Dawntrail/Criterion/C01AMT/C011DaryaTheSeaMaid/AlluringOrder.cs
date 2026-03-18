namespace BossMod.Dawntrail.Criterion.C01AMT.C011DaryaTheSeaMaid;

class AlluringOrder(BossModule module) : Components.RaidwideCast(module, (uint)AID.AlluringOrder);

class AlluringOrderForcedMarch(BossModule module) : Components.StatusDrivenForcedMarch(module, 3.0f, (uint)SID.ForwardMarch, (uint)SID.AboutFace, (uint)SID.LeftFace, (uint)SID.RightFace);

// TODO add priority order to this - DPS flex, but add configuration to module so it can be picked by the player
//  - No need to decide who goes with what stack, just highlight their safe spot? - always 2 spots -> this way we can just check if one side has two stacks or not
class Tidalspout(BossModule module) : Components.StatusStackSpread(module, (uint)SID.TidalspoutTarget, 0, 6, 0) {
    private int NumCasts = 0;
    
    public override void OnStatusGain(Actor actor, ref ActorStatus status) {
        if (status.ID == (uint)SID.TidalspoutTarget) {
            AddStack(actor, status.ExpireAt);
        }
    }
    
    public override void OnStatusLose(Actor actor, ref ActorStatus status) {
        if (status.ID == (uint)SID.TidalspoutTarget) {
            Stacks.Clear();
            NumCasts++;
        }
    }
}

// TODO refer to Tidalspout TODO 
class SwimmingInTheAir(BossModule module) : Components.GenericAOEs(module) {
    private List<AOEInstance> aoes = [];

    public override void OnActorCreated(Actor actor) {
        if ((OID)actor.OID == OID.BlueOrb) {
            aoes.Add(new(new AOEShapeCircle(12f), actor.Position, default, default, Colors.AOE));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell) {
        if (spell.Action.ID == (uint)AID.Hydrofall) {
            NumCasts++;
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) {
        return CollectionsMarshal.AsSpan(aoes);
    }
}

class SunkenTreasure(BossModule module) : Components.GenericAOEs(module) {
    private List<AOEInstance> aoes = [];
    private List<(ulong id, int eAnims)> BlueObjects = [];
    
    private static readonly AOEShapeCircle SphereAOE = new(18f);
    private static readonly AOEShapeDonut DonutAOE = new(6f, 20f);
    
    public override void OnActorEAnim(Actor actor, uint state) {
        if ((OID)actor.OID == OID.BlueSphere || (OID)actor.OID == OID.DonutSphere) {
            int id = BlueObjects.FindIndex(t => t.id == actor.InstanceID);
            if (id < 0) {
                BlueObjects.Add((actor.InstanceID, 1));
                return;
            }

            if (++BlueObjects.Ref(id).eAnims >= 3) {
                BlueObjects.RemoveAt(id);
                NumCasts++;
            }
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) {
        aoes.Clear();
        
        int shown = 0;
        foreach (var t in BlueObjects) {
            if (shown >= 5) {
                break;
            }
            
            var blueObject = WorldState.Actors.Find(t.id);
            if (blueObject == null) {
                continue;
            }

            var shape = ((OID)blueObject.OID == OID.BlueSphere) ? (AOEShape)SphereAOE : (AOEShape)DonutAOE;
            aoes.Add(new AOEInstance(shape, blueObject.Position));
            shown++;
        }
        
        return CollectionsMarshal.AsSpan(aoes);
    }
}