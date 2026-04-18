using BossMod.AI;
using BossMod.Autorotation;
using BossMod.Pathfinding;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BossMod;

sealed class IPCProvider : IDisposable
{
    private Action? _disposeActions;

    public IPCProvider(BossModuleManager bossmod, AIHints hints, RotationModuleManager autorotation, ActionManagerEx amex, MovementOverride movement, AIManager ai)
    {
        Register("HasModuleByDataId", (uint dataId) => BossModuleRegistry.FindByOID(dataId) != null);

        // Timeline IPC endpoints for external plugin integration (e.g. RotationSolverReborn)
        Register("HasActiveModule", () => bossmod.ActiveModule?.StateMachine.ActiveState != null);
        Register("ActiveModuleName", () => bossmod.ActiveModule?.PrimaryActor.Name.ToString());

        // Debug endpoint: walks the state machine and reports what it finds
        Register("Debug.TimelineWalk", () =>
        {
            var module = bossmod.ActiveModule;
            if (module == null)
                return "No active module";
            var sm = module.StateMachine;
            if (sm.ActiveState == null)
                return "ActiveState is null";

            var sb = new StringBuilder();
            sb.Append($"Phase={sm.ActivePhaseIndex} State={sm.ActiveState.ID:X}({sm.ActiveState.Name}) Dur={sm.ActiveState.Duration:F1}s Hint={sm.ActiveState.EndHint}");
            var count = 0;
            var next = sm.ActiveState;
            var foundRW = false;
            var foundTB = false;
            while (next != null && count < 20)
            {
                if (!foundRW && next.EndHint.HasFlag(StateMachine.StateHint.Raidwide))
                {
                    foundRW = true;
                    sb.Append($" | RW@{next.ID:X}({next.Name})");
                }
                if (!foundTB && next.EndHint.HasFlag(StateMachine.StateHint.Tankbuster))
                {
                    foundTB = true;
                    sb.Append($" | TB@{next.ID:X}({next.Name})");
                }
                next = next.NextStates?.Length == 1 ? next.NextStates[0] : null;
                count++;
            }
            if (!foundRW)
                sb.Append(" | RW=NONE");
            if (!foundTB)
                sb.Append(" | TB=NONE");
            if (next == null && count < 20)
                sb.Append($" | Chain ended at {count} states");
            if (count >= 20)
                sb.Append(" | Walked 20+ states");
            return sb.ToString();
        });

        Register("Timeline.NextRaidwideIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
                return float.MaxValue;
            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.Raidwide);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextTankbusterIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
                return float.MaxValue;
            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.Tankbuster);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextKnockbackIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
                return float.MaxValue;
            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.Knockback);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextDowntimeIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
                return float.MaxValue;
            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.DowntimeStart);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextDowntimeEndIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
                return float.MaxValue;
            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.DowntimeEnd);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextVulnerableIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
                return float.MaxValue;
            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.VulnerableStart);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Timeline.NextVulnerableEndIn", () =>
        {
            var module = bossmod.ActiveModule;
            if (module?.StateMachine.ActiveState == null)
                return float.MaxValue;
            var next = module.StateMachine.NextTransitionWithFlag(StateMachine.StateHint.VulnerableEnd);
            return next == DateTime.MaxValue ? float.MaxValue : (float)(next - DateTime.Now).TotalSeconds;
        });

        Register("Hints.NextDamageIn", () =>
        {
            var predicted = hints.PredictedDamage;
            return predicted.Count == 0 ? float.MaxValue : (float)(predicted[0].Activation - DateTime.Now).TotalSeconds;
        });

        Register("Hints.NextDamageType", () =>
        {
            var predicted = hints.PredictedDamage;
            return predicted.Count == 0 ? 0 : (int)predicted[0].Type;
        });

        // Type-specific damage prediction endpoints — search ALL entries for the first matching type
        Register("Hints.NextRaidwideDamageIn", () =>
        {
            var predicted = hints.PredictedDamage;
            var now = DateTime.Now;
            for (var i = 0; i < predicted.Count; ++i)
            {
                if (predicted[i].Type == AIHints.PredictedDamageType.Raidwide)
                    return (float)(predicted[i].Activation - now).TotalSeconds;
            }
            return float.MaxValue;
        });

        Register("Hints.NextTankbusterDamageIn", () =>
        {
            var predicted = hints.PredictedDamage;
            var now = DateTime.Now;
            for (var i = 0; i < predicted.Count; ++i)
            {
                if (predicted[i].Type == AIHints.PredictedDamageType.Tankbuster)
                    return (float)(predicted[i].Activation - now).TotalSeconds;
            }
            return float.MaxValue;
        });

        Register("Hints.SpecialModeIn", () =>
        {
            return hints.ImminentSpecialMode == default
                ? float.MaxValue
                : (float)(hints.ImminentSpecialMode.activation - DateTime.Now).TotalSeconds;
        });

        Register("Hints.SpecialModeType", () =>
        {
            return hints.ImminentSpecialMode == default ? 0 : (int)hints.ImminentSpecialMode.mode;
        });
        Register("Configuration", (List<string> args, bool save) => Service.Config.ConsoleCommand(args.AsSpan(), save));

        var lastModified = DateTime.Now;
        Service.Config.Modified.Subscribe(() => lastModified = DateTime.Now);
        Register("Configuration.LastModified", () => lastModified);

        Register("Rotation.ActionQueue.HasEntries", () =>
        {
            var entries = CollectionsMarshal.AsSpan(autorotation.Hints.ActionsToExecute.Entries);
            var len = entries.Length;
            for (var i = 0; i < len; ++i)
            {
                ref readonly var e = ref entries[i];
                if (!e.Manual)
                {
                    return true;
                }
            }
            return false;
        });

        Register("Presets.Get", (string name) =>
        {
            var preset = autorotation.Database.Presets.FindPresetByName(name);
            return preset != null ? JsonSerializer.Serialize(preset, Serialization.BuildSerializationOptions()) : null;
        });
        Register("Presets.Create", (string presetSerialized, bool overwrite) =>
        {
            var node = JsonNode.Parse(presetSerialized, documentOptions: new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (node == null)
                return false;

            // preset converter operates on array of presets; plan converter doesn't but expects an `Encounter` key in the object
            node = new JsonArray(node);

            var version = 0;
            var finfo = new FileInfo("<in-memory preset>");

            foreach (var conv in PlanPresetConverter.PresetSchema.Converters)
                node = conv(node, version, finfo);

            var p = node.AsArray()[0].Deserialize<Preset>(Serialization.BuildSerializationOptions());
            if (p == null)
                return false;
            var index = autorotation.Database.Presets.UserPresets.FindIndex(x => x.Name == p.Name);
            if (index >= 0 && !overwrite)
                return false;
            autorotation.Database.Presets.Modify(index, p);
            return true;
        });
        Register("Presets.Delete", (string name) =>
        {
            var index = autorotation.Database.Presets.UserPresets.FindIndex(x => x.Name == name);
            if (index < 0)
                return false;
            autorotation.Database.Presets.Modify(index, null);
            return true;
        });

        Register("Presets.GetActive", () => autorotation.Preset?.Name);
        Register("Presets.SetActive", (string name) =>
        {
            var preset = autorotation.Database.Presets.FindPresetByName(name);
            if (preset == null)
                return false;
            autorotation.Preset = preset;
            return true;
        });
        Register("Presets.ClearActive", () =>
        {
            if (autorotation.Preset == null)
                return false;
            autorotation.Preset = null;
            return true;
        });
        Register("Presets.GetForceDisabled", () => autorotation.Preset == RotationModuleManager.ForceDisable);
        Register("Presets.SetForceDisabled", () =>
        {
            if (autorotation.Preset == RotationModuleManager.ForceDisable)
                return false;
            autorotation.Preset = RotationModuleManager.ForceDisable;
            return true;
        });

        bool addTransientStrategy(string presetName, string moduleTypeName, string trackName, string value, StrategyTarget target = StrategyTarget.Automatic, int targetParam = 0)
        {
            var mt = Type.GetType(moduleTypeName);
            if (mt == null || !RotationModuleRegistry.Modules.TryGetValue(mt, out var md))
                return false;
            var iTrack = md.Definition.Configs.FindIndex(td => td.InternalName == trackName);
            if (iTrack < 0)
                return false;

            StrategyValue tempValue;

            switch (md.Definition.Configs[iTrack])
            {
                case StrategyConfigTrack tr:
                    var iOpt = tr.Options.FindIndex(od => od.InternalName == value);
                    if (iOpt < 0)
                        return false;
                    tempValue = new StrategyValueTrack() { Option = iOpt, Target = target, TargetParam = targetParam };
                    break;
                case StrategyConfigFloat sc:
                    tempValue = new StrategyValueFloat() { Value = Math.Clamp(float.Parse(value), sc.MinValue, sc.MaxValue) };
                    break;
                case StrategyConfigInt si:
                    tempValue = new StrategyValueInt() { Value = Math.Clamp(long.Parse(value), si.MinValue, si.MaxValue) };
                    break;
                case var x:
                    throw new ArgumentException($"unhandled config type {x.GetType()}");
            }

            var ms = autorotation.Database.Presets.FindPresetByName(presetName)?.Modules.Find(m => m.Type == mt);
            if (ms == null)
                return false;
            var setting = new Preset.ModuleSetting(default, iTrack, tempValue);
            var index = ms.TransientSettings.FindIndex(s => s.Track == iTrack);
            if (index < 0)
                ms.TransientSettings.Add(setting);
            else
                ms.TransientSettings[index] = setting;
            return true;
        }
        Register("Presets.AddTransientStrategy", (string presetName, string moduleTypeName, string trackName, string value) => addTransientStrategy(presetName, moduleTypeName, trackName, value));
        Register("Presets.AddTransientStrategyTargetEnemyOID", (string presetName, string moduleTypeName, string trackName, string value, int oid) => addTransientStrategy(presetName, moduleTypeName, trackName, value, StrategyTarget.EnemyByOID, oid));

        Register("Presets.ClearTransientStrategy", (string presetName, string moduleTypeName, string trackName) =>
        {
            var mt = Type.GetType(moduleTypeName);
            if (mt == null || !RotationModuleRegistry.Modules.TryGetValue(mt, out var md))
                return false;
            var iTrack = md.Definition.Configs.FindIndex(td => td.InternalName == trackName);
            if (iTrack < 0)
                return false;
            var ms = autorotation.Database.Presets.FindPresetByName(presetName)?.Modules.Find(m => m.Type == mt);
            if (ms == null)
                return false;
            var index = ms.TransientSettings.FindIndex(s => s.Track == iTrack);
            if (index < 0)
                return false;
            ms.TransientSettings.RemoveAt(index);
            return true;
        });
        Register("Presets.ClearTransientModuleStrategies", (string presetName, string moduleTypeName) =>
        {
            var mt = Type.GetType(moduleTypeName);
            if (mt == null || !RotationModuleRegistry.Modules.TryGetValue(mt, out var md))
                return false;
            var ms = autorotation.Database.Presets.FindPresetByName(presetName)?.Modules.Find(m => m.Type == mt);
            if (ms == null)
                return false;
            ms.TransientSettings.Clear();
            return true;
        });
        Register("Presets.ClearTransientPresetStrategies", (string presetName) =>
        {
            var preset = autorotation.Database.Presets.FindPresetByName(presetName);
            if (preset == null)
                return false;
            foreach (var ms in preset.Modules)
                ms.TransientSettings.Clear();
            return true;
        });

        Register("AI.SetPreset", (string name) => ai.SetAIPreset(autorotation.Database.Presets.AllPresets.FirstOrDefault(x => x.Name.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))));
        Register("AI.GetPreset", () => ai.GetAIPreset);

        // --- Custom IPC: expose AIHints mechanic data for external consumers (e.g. RSR) ---

        // Returns JSON array of predicted damage events: [{ "Players": <ulong bitmask>, "Activation": <ISO8601>, "Type": "Raidwide"|"Tankbuster"|"Shared"|"None" }, ...]
        Register("Hints.PredictedDamage", () =>
        {
            var list = autorotation.Hints.PredictedDamage;
            if (list.Count == 0)
                return "[]";
            var arr = new JsonArray();
            foreach (var d in list)
            {
                arr.Add(new JsonObject
                {
                    ["Players"] = d.Players.Raw,
                    ["Activation"] = d.Activation.ToString("o"),
                    ["Type"] = d.Type.ToString()
                });
            }
            return arr.ToJsonString();
        });

        // Simple convenience: is a raidwide predicted within the next N seconds (default 5)?
        Register("Hints.IsRaidwideImminent", (float seconds) =>
        {
            var deadline = DateTime.Now.AddSeconds(seconds);
            foreach (var d in autorotation.Hints.PredictedDamage)
                if (d.Type == AIHints.PredictedDamageType.Raidwide && d.Activation <= deadline)
                    return true;
            return false;
        });

        // Simple convenience: is a tankbuster predicted within the next N seconds?
        Register("Hints.IsTankbusterImminent", (float seconds) =>
        {
            var deadline = DateTime.Now.AddSeconds(seconds);
            foreach (var d in autorotation.Hints.PredictedDamage)
                if (d.Type == AIHints.PredictedDamageType.Tankbuster && d.Activation <= deadline)
                    return true;
            return false;
        });

        // Simple convenience: is a shared/stack predicted within the next N seconds?
        Register("Hints.IsSharedImminent", (float seconds) =>
        {
            var deadline = DateTime.Now.AddSeconds(seconds);
            foreach (var d in autorotation.Hints.PredictedDamage)
                if (d.Type == AIHints.PredictedDamageType.Shared && d.Activation <= deadline)
                    return true;
            return false;
        });

        // Returns JSON array of forbidden directions: [{ "Center": <float rad>, "HalfWidth": <float rad>, "Activation": <ISO8601> }, ...]
        Register("Hints.ForbiddenDirections", () =>
        {
            var list = autorotation.Hints.ForbiddenDirections;
            if (list.Count == 0)
                return "[]";
            var arr = new JsonArray();
            foreach (var (center, halfWidth, activation) in list)
            {
                arr.Add(new JsonObject
                {
                    ["Center"] = center.Rad,
                    ["HalfWidth"] = halfWidth.Rad,
                    ["Activation"] = activation.ToString("o")
                });
            }
            return arr.ToJsonString();
        });

        // Returns the current special mode as string: "Normal", "Pyretic", "NoMovement", "Freezing", "Misdirection"
        Register("Hints.SpecialMode", () => autorotation.Hints.ImminentSpecialMode.mode.ToString());

        // Returns the activation time of the special mode (or DateTime.MaxValue if Normal)
        Register("Hints.SpecialModeActivation", () =>
            autorotation.Hints.ImminentSpecialMode.mode != AIHints.SpecialMode.Normal
                ? autorotation.Hints.ImminentSpecialMode.activation.ToString("o")
                : null);

        // --- Encounters IPC: list all registered boss encounters ---

        // Returns JSON array of all registered boss encounters:
        // [{ "OID": uint, "BossName": string, "GroupName": string, "Category": string, "Expansion": string, "GroupType": string, "SortOrder": int }]
        Register("Encounters.GetAll", () =>
        {
            var arr = new JsonArray();
            foreach (var (oid, info) in BossModuleRegistry.RegisteredModules)
            {
                // Include all modules (WIP, Contributed, Verified, AISupport)

                string bossName;
                string groupName;
                try
                {
                    bossName = ResolveBossName(info);
                    groupName = ResolveGroupName(info);
                }
                catch
                {
                    bossName = info.ModuleType.Name;
                    groupName = info.GroupType.ToString();
                }

                arr.Add(new JsonObject
                {
                    ["OID"] = oid,
                    ["BossName"] = bossName,
                    ["GroupName"] = groupName,
                    ["Category"] = info.Category.ToString(),
                    ["Expansion"] = info.Expansion.ToString(),
                    ["GroupType"] = info.GroupType.ToString(),
                    ["SortOrder"] = info.SortOrder
                });
            }
            return arr.ToJsonString();
        });

        // Returns timeline phases for a specific encounter OID (offline, no active fight needed)
        Register("Encounters.GetPhasesForOID", (uint oid) =>
        {
            var module = BossModuleRegistry.CreateModuleForTimeline(oid);
            if (module == null) return "[]";
            var tree = new StateMachineTree(module.StateMachine);
            tree.ApplyTimings(null);
            var arr = new JsonArray();
            foreach (var phase in tree.Phases)
            {
                arr.Add(new JsonObject
                {
                    ["Name"] = phase.Name,
                    ["StartTime"] = phase.StartTime,
                    ["Duration"] = phase.Duration,
                    ["MaxTime"] = phase.MaxTime
                });
            }
            return arr.ToJsonString();
        });

        // Returns timeline states with mechanic flags for a specific encounter OID (offline)
        Register("Encounters.GetStatesForOID", (uint oid) =>
        {
            var module = BossModuleRegistry.CreateModuleForTimeline(oid);
            if (module == null) return "[]";
            var tree = new StateMachineTree(module.StateMachine);
            tree.ApplyTimings(null);
            var arr = new JsonArray();
            foreach (var phase in tree.Phases)
            {
                foreach (var node in phase.BranchNodes(0))
                {
                    var hint = node.State.EndHint;
                    arr.Add(new JsonObject
                    {
                        ["ID"] = node.State.ID,
                        ["PhaseID"] = node.PhaseID,
                        ["Time"] = phase.StartTime + node.Time,
                        ["Duration"] = node.State.Duration,
                        ["Name"] = node.State.Name,
                        ["Comment"] = node.State.Comment,
                        ["IsRaidwide"] = hint.HasFlag(StateMachine.StateHint.Raidwide),
                        ["IsTankbuster"] = hint.HasFlag(StateMachine.StateHint.Tankbuster),
                        ["IsKnockback"] = hint.HasFlag(StateMachine.StateHint.Knockback),
                        ["IsDowntime"] = node.IsDowntime,
                        ["IsPositioning"] = node.IsPositioning,
                        ["IsVulnerable"] = node.IsVulnerable,
                        ["BossIsCasting"] = node.BossIsCasting
                    });
                }
            }
            return arr.ToJsonString();
        });

        // Returns total duration for a specific encounter OID (offline)
        Register("Encounters.GetTotalDuration", (uint oid) =>
        {
            var module = BossModuleRegistry.CreateModuleForTimeline(oid);
            if (module == null) return 0f;
            var tree = new StateMachineTree(module.StateMachine);
            tree.ApplyTimings(null);
            return tree.TotalMaxTime;
        });

        // --- Timeline IPC: expose fight state machine data for rotation planners ---

        // Whether a boss module with an active state machine is currently loaded
        Register("Timeline.IsActive", () => autorotation.Bossmods.ActiveModule?.StateMachine.ActiveState != null);

        // Returns JSON with encounter info: { "OID": uint, "Name": string, "TotalDuration": float }
        // Returns null if no active module
        Register("Timeline.GetEncounter", () =>
        {
            var module = autorotation.Bossmods.ActiveModule;
            if (module == null)
                return null;
            var sm = module.StateMachine;
            var tree = new StateMachineTree(sm);
            tree.ApplyTimings(null);
            var obj = new JsonObject
            {
                ["OID"] = module.PrimaryActor.OID,
                ["Name"] = module.Info?.GroupType.ToString() ?? module.GetType().Name,
                ["TotalDuration"] = tree.TotalMaxTime
            };
            return obj.ToJsonString();
        });

        // Returns JSON array of phases: [{ "Name", "StartTime", "Duration", "MaxTime" }]
        Register("Timeline.GetPhases", () =>
        {
            var module = autorotation.Bossmods.ActiveModule;
            if (module == null)
                return "[]";
            var tree = new StateMachineTree(module.StateMachine);
            tree.ApplyTimings(null);
            var arr = new JsonArray();
            foreach (var phase in tree.Phases)
            {
                arr.Add(new JsonObject
                {
                    ["Name"] = phase.Name,
                    ["StartTime"] = phase.StartTime,
                    ["Duration"] = phase.Duration,
                    ["MaxTime"] = phase.MaxTime
                });
            }
            return arr.ToJsonString();
        });

        // Returns JSON array of states with mechanic flags:
        // [{ "ID", "PhaseID", "Time", "Duration", "Name", "Comment", "IsRaidwide", "IsTankbuster", "IsKnockback", "IsDowntime", "IsPositioning", "IsVulnerable", "BossIsCasting" }]
        Register("Timeline.GetStates", () =>
        {
            var module = autorotation.Bossmods.ActiveModule;
            if (module == null)
                return "[]";
            var tree = new StateMachineTree(module.StateMachine);
            tree.ApplyTimings(null);
            var arr = new JsonArray();
            foreach (var phase in tree.Phases)
            {
                foreach (var node in phase.BranchNodes(0))
                {
                    var hint = node.State.EndHint;
                    arr.Add(new JsonObject
                    {
                        ["ID"] = node.State.ID,
                        ["PhaseID"] = node.PhaseID,
                        ["Time"] = phase.StartTime + node.Time,
                        ["Duration"] = node.State.Duration,
                        ["Name"] = node.State.Name,
                        ["Comment"] = node.State.Comment,
                        ["IsRaidwide"] = hint.HasFlag(StateMachine.StateHint.Raidwide),
                        ["IsTankbuster"] = hint.HasFlag(StateMachine.StateHint.Tankbuster),
                        ["IsKnockback"] = hint.HasFlag(StateMachine.StateHint.Knockback),
                        ["IsDowntime"] = node.IsDowntime,
                        ["IsPositioning"] = node.IsPositioning,
                        ["IsVulnerable"] = node.IsVulnerable,
                        ["BossIsCasting"] = node.BossIsCasting
                    });
                }
            }
            return arr.ToJsonString();
        });
    }

    public void Dispose() => _disposeActions?.Invoke();

    private void Register<TRet>(string name, Func<TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, TRet>(string name, Func<T1, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, TRet>(string name, Func<T1, T2, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, T3, TRet>(string name, Func<T1, T2, T3, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, T3, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, T3, T4, TRet>(string name, Func<T1, T2, T3, T4, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, T3, T4, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    private void Register<T1, T2, T3, T4, T5, TRet>(string name, Func<T1, T2, T3, T4, T5, TRet> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, T2, T3, T4, T5, TRet>("BossMod." + name);
        p.RegisterFunc(func);
        _disposeActions += p.UnregisterFunc;
    }

    //private void Register(string name, Action func)
    //{
    //    var p = Service.PluginInterface.GetIpcProvider<object>("BossMod." + name);
    //    p.RegisterAction(func);
    //    _disposeActions += p.UnregisterAction;
    //}

    private void Register<T1>(string name, Action<T1> func)
    {
        var p = Service.PluginInterface.GetIpcProvider<T1, object>("BossMod." + name);
        p.RegisterAction(func);
        _disposeActions += p.UnregisterAction;
    }

    private static string ResolveBossName(BossModuleRegistry.Info info)
    {
        if (info.NameID == 0)
            return info.ModuleType.Name;

        return info.GroupType switch
        {
            BossModuleInfo.GroupType.CriticalEngagement or BossModuleInfo.GroupType.BozjaDuel
                => Service.LuminaRow<Lumina.Excel.Sheets.DynamicEvent>(info.NameID)?.Name.ToString() ?? info.ModuleType.Name,
            BossModuleInfo.GroupType.EurekaNM or BossModuleInfo.GroupType.ForayFATE
                => Service.LuminaRow<Lumina.Excel.Sheets.Fate>(info.NameID)?.Name.ToString() ?? info.ModuleType.Name,
            _ => FixCase(Service.LuminaRow<Lumina.Excel.Sheets.BNpcName>(info.NameID)?.Singular.ToString() ?? info.ModuleType.Name)
        };
    }

    private static string ResolveGroupName(BossModuleRegistry.Info info)
    {
        return info.GroupType switch
        {
            BossModuleInfo.GroupType.CFC or BossModuleInfo.GroupType.MaskedCarnivale or BossModuleInfo.GroupType.RemovedUnreal
            or BossModuleInfo.GroupType.BaldesionArsenal or BossModuleInfo.GroupType.CastrumLacusLitore
            or BossModuleInfo.GroupType.TheDalriada or BossModuleInfo.GroupType.TheForkedTowerBlood
            or BossModuleInfo.GroupType.CriticalEngagement or BossModuleInfo.GroupType.BozjaDuel
            or BossModuleInfo.GroupType.EurekaNM
                => FixCase(Service.LuminaRow<Lumina.Excel.Sheets.ContentFinderCondition>(info.GroupID)?.Name.ToString() ?? info.GroupType.ToString()),
            BossModuleInfo.GroupType.Quest
                => Service.LuminaRow<Lumina.Excel.Sheets.Quest>(info.GroupID)?.Name.ToString() ?? "Quest",
            BossModuleInfo.GroupType.Fate or BossModuleInfo.GroupType.ForayFATE
                => Service.LuminaRow<Lumina.Excel.Sheets.Fate>(info.GroupID)?.Name.ToString() ?? "FATE",
            BossModuleInfo.GroupType.Hunt
                => $"{info.Expansion.ShortName()} Hunt {(BossModuleInfo.HuntRank)info.GroupID}",
            BossModuleInfo.GroupType.GoldSaucer
                => Service.LuminaRow<Lumina.Excel.Sheets.GoldSaucerTextData>(info.GroupID)?.Text.ToString() ?? "Gold Saucer",
            _ => info.Category.ToString()
        };
    }

    private static string FixCase(string str)
        => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(str);
}
