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
		[HarmonyPatch(typeof(InputUILinkDashPainting), "Execute", new System.Type[] { typeof(List<InputEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Iuildp_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Show the late placement warning if the dash action starts after max placement time.

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
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var getLastActionEndTimeMatch = new CodeMatch(OpCodes.Call, getLastActionEndTimeMethodInfo);
			var getTurnLengthMatch = new CodeMatch(OpCodes.Callvirt, getTurnLengthMethodInfo);
			var popMatch = new CodeMatch(OpCodes.Pop);
			var loadStrMatch = new CodeMatch(OpCodes.Ldstr, "melee_duration_dash_out");
			var storeTurnStartTime = new CodeInstruction(OpCodes.Stloc_S, turnStartTimeLocal);
			var loadTurnStartTime = new CodeInstruction(OpCodes.Ldloc_S, turnStartTimeLocal);
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

			cm.MatchStartForward(loadStrMatch)
				.Advance(-1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();

			cm.CreateLabel(out var skipWarningLabel);
			var skipWarning = new CodeInstruction(OpCodes.Blt_Un_S, skipWarningLabel);

			cm.Insert(loadActionEndTime)
				.AddLabels(labels)
				.Advance(1)
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
