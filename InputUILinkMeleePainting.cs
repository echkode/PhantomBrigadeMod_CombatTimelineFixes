// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Combat.Systems;
using PhantomBrigade.Data;

namespace EchKode.PBMods.CombatTimelineFixes
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(InputUILinkMeleePainting), "Redraw")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Iuilmp_RedrawTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Show the late placement warning if the melee action starts after max placement time.
			// Set prediction time target to start time regardless if the action starts before max
			// placement time so the rest of the UI painting routine can complete.

			var cm = new CodeMatcher(instructions, generator);
			var turnStartTimeLocal = generator.DeclareLocal(typeof(float));
			var getLastActionEndTimeMethodInfo = AccessTools.DeclaredMethod(
				typeof(ActionUtility),
				nameof(ActionUtility.GetLastActionTime),
				new System.Type[]
				{
					typeof(CombatEntity),
					typeof(bool),
				});
			var getTurnLengthMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatContext), nameof(CombatContext.turnLength));
			var getIsModifierUsedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(InputContext), nameof(InputContext.isModifierUsed));
			var replacePredictionTimeTargetMethodInfo = AccessTools.DeclaredMethod(typeof(CombatContext), nameof(CombatContext.ReplacePredictionTimeTarget));
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var getLastActionEndTimeMatch = new CodeMatch(OpCodes.Call, getLastActionEndTimeMethodInfo);
			var getTurnLengthMatch = new CodeMatch(OpCodes.Callvirt, getTurnLengthMethodInfo);
			var popMatch = new CodeMatch(OpCodes.Pop);
			var getIsModifierUsedMatch = new CodeMatch(OpCodes.Callvirt, getIsModifierUsedMethodInfo);
			var storeTurnStartTime = new CodeInstruction(OpCodes.Stloc_S, turnStartTimeLocal);
			var loadTurnStartTime = new CodeInstruction(OpCodes.Ldloc_S, turnStartTimeLocal);
			var loadThis = new CodeInstruction(OpCodes.Ldarg_0);
			var loadCombat = CodeInstruction.LoadField(typeof(InputUILinkMeleePainting), "combat");
			var replacePredictionTimeTarget = new CodeInstruction(OpCodes.Callvirt, replacePredictionTimeTargetMethodInfo);
			var getSim = new CodeInstruction(OpCodes.Call, getSimMethodInfo);
			var loadMaxTimePlacement = CodeInstruction.LoadField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.maxActionTimePlacement));
			var add = new CodeInstruction(OpCodes.Add);
			var showWarningLate = CodeInstruction.Call(typeof(UILinkPaintingPatch), nameof(UILinkPaintingPatch.ShowWarningLate));

			cm.MatchEndForward(getLastActionEndTimeMatch)
				.Advance(1);
			var loadActionEndTime = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.MatchEndForward(getTurnLengthMatch)
				.MatchStartForward(popMatch)
				.SetInstructionAndAdvance(storeTurnStartTime);

			cm.MatchStartForward(getIsModifierUsedMatch)
				.Advance(-2);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();

			cm.CreateLabel(out var skipWarningLabel);
			var skipWarning = new CodeInstruction(OpCodes.Blt_Un_S, skipWarningLabel);

			cm.Insert(loadThis)
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(loadCombat)
				.InsertAndAdvance(loadActionEndTime)
				.InsertAndAdvance(replacePredictionTimeTarget)
				.InsertAndAdvance(loadActionEndTime)
				.InsertAndAdvance(loadTurnStartTime)
				.InsertAndAdvance(getSim)
				.InsertAndAdvance(loadMaxTimePlacement)
				.InsertAndAdvance(add)
				.InsertAndAdvance(skipWarning)
				.InsertAndAdvance(showWarningLate);

			return cm.InstructionEnumeration();
		}
	}
}
