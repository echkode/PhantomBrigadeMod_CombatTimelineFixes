// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade.Combat.Systems;
using PhantomBrigade.Data;

namespace EchKode.PBMods.CombatTimelineFixes
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(CombatExecutionEndLateSystem), nameof(CombatExecutionEndLateSystem.SplitWaitActions))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Ceels_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Don't split locked actions like crashes.

			var cm = new CodeMatcher(instructions, generator);
			var paintingTypeFieldInfo = AccessTools.DeclaredField(typeof(DataBlockActionCore), nameof(DataBlockActionCore.paintingType));
			var paintingTypeMatch = new CodeMatch(OpCodes.Ldfld, paintingTypeFieldInfo);
			var loadArgMatch = new CodeMatch(OpCodes.Ldarg_1);
			var loadLocking = CodeInstruction.LoadField(typeof(DataBlockActionCore), nameof(DataBlockActionCore.locking));
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchStartForward(paintingTypeMatch)
				.Advance(-1);
			var loadDataCore = cm.Instruction.Clone();

			cm.MatchStartForward(loadArgMatch);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Brfalse_S, skipRetLabel);

			cm.Insert(loadDataCore)
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(loadLocking)
				.InsertAndAdvance(skipRet)
				.InsertAndAdvance(ret);

			return cm.InstructionEnumeration();
		}
	}
}
