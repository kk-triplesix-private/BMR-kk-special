namespace BossMod.Dawntrail.Criterion.C01AMT.C011DaryaTheSeaMaid;

[SkipLocalsInit]
sealed class DaryaTheSeaMaidStates : StateMachineBuilder {
    public DaryaTheSeaMaidStates(BossModule module) : base(module) {
        DeathPhase(default, SinglePhase);
    }

    private void SinglePhase(uint id) {
        FamilarCall(id, 7.1f);
        AlluringOrder1(id + 0x200, 6.3f);
        CeaselessCurrent(id + 0x300, 7.3f);
        AlluringOrder2(id + 0x400, 7.3f);
        AquaSpear1(id + 0x500, 6.7f);
        SimpleState(id + 0xFF0000, 9999, "Rest of Fight");
    }

    private void FamilarCall(uint id, float delay) {
        Cast(id, (uint)AID.PiercingPlunge, delay, 5, "Raidwide")
            .ActivateOnEnter<PiercingPlunge>()
            .DeactivateOnExit<PiercingPlunge>();
            
        Cast(id + 0x10, (uint)AID.FamiliarCall, 10.3f, 3, "Adds spawn")
            .ActivateOnEnter<EchoedSerenade>();
        Cast(id + 0x50, (uint)AID.EchoedSerenade, 5.1f, 8.5f)
            .ActivateOnEnter<Hydrobullet>();
        ComponentCondition<EchoedSerenade>(id + 0x60, 3.5f, o => o.NumCasts > 0, "First add");
        ComponentCondition<EchoedSerenade>(id + 0x70, 3.1f, o => o.NumCasts > 1, "Second add")
            .ActivateOnEnter<SurgingCurrent>();
        ComponentCondition<Hydrobullet>(id + 0x80, 0.2f, hydrobullet => hydrobullet.NumFinishedSpreads > 0, "Spreads");
        ComponentCondition<EchoedSerenade>(id + 0x90, 3.1f, o => o.NumCasts > 2, "Third add");
        ComponentCondition<EchoedSerenade>(id + 0x100, 3.1f, o => o.NumCasts > 3, "Fourth add")
            .DeactivateOnExit<EchoedSerenade>();
        ComponentCondition<Hydrobullet>(id + 0x110, 0.2f, hydrobullet => hydrobullet.NumFinishedSpreads > 0, "Spreads")
            .DeactivateOnExit<Hydrobullet>();
        ComponentCondition<SurgingCurrent>(id + 0x120, 8.1f, o => o.NumCasts >= 4, "SurgingCurrent")
            .DeactivateOnExit<SurgingCurrent>();
    }

    private void AlluringOrder1(uint id, float delay) {
        Cast(id, (uint)AID.AlluringOrder, delay, 4, "Raidwide")
            .ActivateOnEnter<AlluringOrder>()
            .DeactivateOnExit<AlluringOrder>()
            .ActivateOnEnter<AlluringOrderForcedMarch>()
            .ActivateOnEnter<Tidalspout>();
        Cast(id + 0x10, (uint)AID.SwimmingInTheAir, 5.4f, 4, "SwimmingInTheAir")
            .ActivateOnEnter<SwimmingInTheAir>();
        ComponentCondition<AlluringOrderForcedMarch>(id + 0x20, 13, o => o.NumActiveForcedMarches > 0, "Controlled Walk");
        ComponentCondition<SwimmingInTheAir>(id + 0x30, 4.0f, o => o.NumCasts > 0, "Puddles")
            .DeactivateOnExit<SwimmingInTheAir>();
        ComponentCondition<Tidalspout>(id + 0x30, 0.2f, o => !o.Active, "Stacks")
            .DeactivateOnExit<Tidalspout>()
            .DeactivateOnExit<AlluringOrderForcedMarch>();
    }

    private void CeaselessCurrent(uint id, float delay) {
        Cast(id + 0x10, (uint)AID.CeaselessCurrent, delay, 4, "CeaselessCurrent")
            .ActivateOnEnter<CeaselessCurrent>()
            .ActivateOnEnter<SurgingCurrent>()
            .ActivateOnEnter<CrossCurrent>();
        ComponentCondition<CeaselessCurrent>(id + 0x20, 8.2f, o => o.NumCasts > 0, "Exaflares start");
        ComponentCondition<CeaselessCurrent>(id + 0x30, 8.1f, o => o.NumCasts >= 10, "Exaflares end")
            .DeactivateOnExit<CeaselessCurrent>()
            .DeactivateOnExit<SurgingCurrent>();
    }
    
    private void AlluringOrder2(uint id, float delay) {
        Cast(id, (uint)AID.PiercingPlunge, delay, 5, "Raidwide")
            .ActivateOnEnter<PiercingPlunge>()
            .DeactivateOnExit<PiercingPlunge>();
        
        Cast(id + 0x10, (uint)AID.AlluringOrder, 7.1f, 4, "Raidwide")
            .ActivateOnEnter<AlluringOrder>()
            .DeactivateOnExit<AlluringOrder>()
            .ActivateOnEnter<AlluringOrderForcedMarch>()
            .ActivateOnEnter<Tidalspout>();
        
        Cast(id + 0x20, (uint)AID.SunkenTreasure, 5.2f, 4, "Spawns Spheres/Donuts")
            .ActivateOnEnter<SunkenTreasure>()
            .ActivateOnEnter<SurgingCurrent>();
        
        ComponentCondition<AlluringOrderForcedMarch>(id + 0x30, 14, o => o.NumActiveForcedMarches > 0, "Controlled Walk");
        ComponentCondition<Tidalspout>(id + 0x40, 1.7f, o => !o.Active, "Sphere Shatter + Stack Resolves")
            .DeactivateOnExit<AlluringOrderForcedMarch>()
            .DeactivateOnExit<Tidalspout>();
        ComponentCondition<SurgingCurrent>(id + 0x50, 4.7f, o => o.NumCasts >= 4, "2nd SurgingCurrent Resolves")
            .DeactivateOnExit<SurgingCurrent>();
        ComponentCondition<SunkenTreasure>(id + 0x60, 2.7f, o => o.NumCasts >= 6, "2nd Sphere Shatter")
            .DeactivateOnExit<SunkenTreasure>();
    }

    private void AquaSpear1(uint id, float delay) {
        Cast(id, (uint)AID.AquaSpear, delay, 3, "Aqua Spear")
            .ActivateOnEnter<AquaSpear>();
        ComponentCondition<AquaSpear>(id + 0x10, 6.7f, o => o.NumCasts >= 4, "Spreads resolve");
        
        Cast(id + 0x20, (uint)AID.FamiliarCall, 4.7f, 3, "Adds spawn")
            .ActivateOnEnter<EchoedSerenade2>()
            .ActivateOnEnter<AquaBall>()
            .ActivateOnEnter<CrossCurrent>();
        Cast(id + 0x30, (uint)AID.EchoedSerenade, 5.1f, 8.5f);
        ComponentCondition<EchoedSerenade>(id + 0x40, 3.5f, o => o.NumCasts > 0, "First add");
        ComponentCondition<EchoedSerenade>(id + 0x50, 3.1f, o => o.NumCasts > 1, "Second add");
        ComponentCondition<EchoedSerenade>(id + 0x60, 3.1f, o => o.NumCasts > 2, "Third add");
        ComponentCondition<EchoedSerenade>(id + 0x70, 3.1f, o => o.NumCasts > 3, "Fourth add");

        Cast(id + 0x100, (uint)AID.FamiliarCall, 14.0f, 3, "Adds spawn")
            .DeactivateOnExit<AquaBall>();
        Cast(id + 0x110, (uint)AID.EchoedReprise, 6.2f, 3.7f);
        ComponentCondition<EchoedSerenade>(id + 0x120, 3.5f, o => o.NumCasts > 0, "First add");
        ComponentCondition<EchoedSerenade>(id + 0x130, 3.1f, o => o.NumCasts > 1, "Second add");
        ComponentCondition<EchoedSerenade>(id + 0x140, 3.1f, o => o.NumCasts > 2, "Third add");
        ComponentCondition<EchoedSerenade>(id + 0x150, 3.1f, o => o.NumCasts > 3, "Fourth add")
            .ActivateOnEnter<SurgingCurrent>()
            .DeactivateOnExit<EchoedSerenade2>();
        ComponentCondition<SurgingCurrent>(id + 0x160, 5.9f, o => o.NumCasts >= 4, "SurgingCurrent")
            .DeactivateOnExit<SurgingCurrent>()
            .DeactivateOnExit<AquaSpear>();
        
    }
}