// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Combat.Components;
using PhantomBrigade.Combat.Systems;
using PhantomBrigade.Data;

namespace EchKode.PBMods.CombatTimelineFixes
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(InputCombatMeleeUtility), nameof(InputCombatMeleeUtility.AttemptTargeting))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Icmu_AttemptTargetingTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Check that the melee action can be placed before max placement time. If not, cancel placement.

			var cm = new CodeMatcher(instructions, generator);
			var getLastActionEndTimeMethodInfo = AccessTools.DeclaredMethod(
				typeof(ActionUtility),
				nameof(ActionUtility.GetLastActionTime),
				new System.Type[]
				{
					typeof(CombatEntity),
					typeof(bool),
				});
			var getPaintedPathMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(InputContext), nameof(InputContext.paintedPath));
			var getCurrentTurnMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatContext), nameof(CombatContext.currentTurn));
			var getTurnLengthMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatContext), nameof(CombatContext.turnLength));
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var getLastActionEndTimeMatch = new CodeMatch(OpCodes.Call, getLastActionEndTimeMethodInfo);
			var getPaintedPathMatch = new CodeMatch(OpCodes.Callvirt, getPaintedPathMethodInfo);
			var loadCombat = new CodeInstruction(OpCodes.Ldloc_0);
			var getCurrentTurn = new CodeInstruction(OpCodes.Callvirt, getCurrentTurnMethodInfo);
			var loadTurnField = CodeInstruction.LoadField(typeof(CurrentTurn), nameof(CurrentTurn.i));
			var getTurnLength = new CodeInstruction(OpCodes.Callvirt, getTurnLengthMethodInfo);
			var loadLengthField = CodeInstruction.LoadField(typeof(TurnLength), nameof(TurnLength.i));
			var mul = new CodeInstruction(OpCodes.Mul);
			var conv = new CodeInstruction(OpCodes.Conv_R4);
			var getSim = new CodeInstruction(OpCodes.Call, getSimMethodInfo);
			var loadMaxTimePlacement = CodeInstruction.LoadField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.maxActionTimePlacement));
			var add = new CodeInstruction(OpCodes.Add);
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchEndForward(getLastActionEndTimeMatch)
				.Advance(1);
			var loadActionEndTime = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.MatchStartForward(getPaintedPathMatch)
				.Advance(-1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();

			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Blt_Un_S, skipRetLabel);

			cm.Insert(loadActionEndTime)
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(loadCombat)
				.InsertAndAdvance(getCurrentTurn)
				.InsertAndAdvance(loadTurnField)
				.InsertAndAdvance(loadCombat)
				.InsertAndAdvance(getTurnLength)
				.InsertAndAdvance(loadLengthField)
				.InsertAndAdvance(mul)
				.InsertAndAdvance(conv)
				.InsertAndAdvance(getSim)
				.InsertAndAdvance(loadMaxTimePlacement)
				.InsertAndAdvance(add)
				.InsertAndAdvance(skipRet)
				.InsertAndAdvance(ret);

			return cm.InstructionEnumeration();
		}
	}
}
