﻿using GameNetcodeStuff;
using HarmonyLib;
using LC_API.GameInterfaceAPI.Events.EventArgs.Player;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

namespace LC_API.GameInterfaceAPI.Events.Patches.Player
{
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.BeginGrabObject))]
    internal class GrabbingItem
    {
        internal static StartGrabbingItemEventArgs CallStartGrabbingItem(PlayerControllerB playerController, GrabbableObject grabbableObject)
        {
            if (Plugin.configVanillaSupport.Value) return null;

            StartGrabbingItemEventArgs ev = new StartGrabbingItemEventArgs(Features.Player.GetOrAdd(playerController),
                Features.Item.Get(grabbableObject));

            Handlers.Player.OnStartGrabbingItem(ev);

            return ev;
        }

        internal static GrabbingItemEventArgs CallGrabbingItem(PlayerControllerB playerController, GrabbableObject grabbableObject)
        {
            if (Plugin.configVanillaSupport.Value) return null;

            GrabbingItemEventArgs ev = new GrabbingItemEventArgs(Features.Player.GetOrAdd(playerController),
                Features.Item.Get(grabbableObject));

            Handlers.Player.OnGrabbingItem(ev);

            return ev;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);

            {
                const int offset = 15;

                int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Stfld &&
                    i.OperandIs(AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.currentlyGrabbingObject)))) + offset;

                Label nullLabel = generator.DefineLabel();
                Label notAllowedLabel = generator.DefineLabel();
                Label skipLabel = generator.DefineLabel();

                CodeInstruction[] inst = new CodeInstruction[]
                {
                // StartGrabbingItemEventArgs ev = GrabbingItem.CallStartGrabbingItem(PlayerControllerB, GrabbableObject)
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(newInstructions[index]),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.currentlyGrabbingObject))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GrabbingItem), nameof(GrabbingItem.CallStartGrabbingItem))),

                // if (ev is null) -> base game code
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brfalse_S, nullLabel),

                // if (!ev.IsAllowed) return
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(StartGrabbingItemEventArgs), nameof(StartGrabbingItemEventArgs.IsAllowed))),
                new CodeInstruction(OpCodes.Brfalse_S, notAllowedLabel),

                new CodeInstruction(OpCodes.Br, skipLabel),
                new CodeInstruction(OpCodes.Pop).WithLabels(nullLabel),
                new CodeInstruction(OpCodes.Br, skipLabel),
                new CodeInstruction(OpCodes.Ret).WithLabels(notAllowedLabel)
                };

                newInstructions.InsertRange(index, inst);

                newInstructions[index + inst.Length].labels.Add(skipLabel);
            }

            {
                const int offset = -2;

                int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Ldstr &&
                    i.OperandIs("GrabInvalidated")) + offset;

                Label nullLabel = generator.DefineLabel();
                Label notAllowedLabel = generator.DefineLabel();
                Label skipLabel = generator.DefineLabel();

                CodeInstruction[] inst = new CodeInstruction[]
                {
                    // GrabbingItemEventArgs ev = GrabbingItem.CallGrabbingItem(PlayerControllerB, GrabbableObject)
                    new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(newInstructions[index]),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.currentlyGrabbingObject))),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GrabbingItem), nameof(GrabbingItem.CallGrabbingItem))),
                    
                    // if (ev is null) -> base game code
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Brfalse_S, nullLabel),

                    // if (!ev.IsAllowed) return
                    new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(StartGrabbingItemEventArgs), nameof(StartGrabbingItemEventArgs.IsAllowed))),
                    new CodeInstruction(OpCodes.Brfalse_S, notAllowedLabel),

                    new CodeInstruction(OpCodes.Br, skipLabel),
                    new CodeInstruction(OpCodes.Pop).WithLabels(nullLabel),
                    new CodeInstruction(OpCodes.Br, skipLabel),
                    new CodeInstruction(OpCodes.Ret).WithLabels(notAllowedLabel)
                };

                newInstructions.InsertRange(index, inst);

                newInstructions[index + inst.Length].labels.Add(skipLabel);
            }

            for (int i = 0; i < newInstructions.Count; i++) yield return newInstructions[i];
        }
    }
}
