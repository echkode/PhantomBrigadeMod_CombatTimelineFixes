// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Data;

namespace EchKode.PBMods.CombatTimelineFixes
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(ActionUtility), nameof(ActionUtility.CrashEntity))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Au_CrashEntityTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Don't create crash action if the unit is wrecked or concussed.

			var cm = new CodeMatcher(instructions, generator);
			var getLinkedEntityMethodInfo = AccessTools.DeclaredMethod(
				typeof(IDUtility),
				nameof(IDUtility.GetLinkedPersistentEntity),
				new System.Type[] { typeof(CombatEntity) });
			var instantiateActionMethodInfo = AccessTools.DeclaredMethod(
				typeof(DataHelperAction),
				nameof(DataHelperAction.InstantiateAction),
				new System.Type[]
				{
					typeof(CombatEntity),
					typeof(string),
					typeof(float),
					typeof(bool).MakeByRefType(),
					typeof(bool),
				});
			var isConcussedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatEntity), nameof(CombatEntity.isConcussed));
			var isWreckedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(PersistentEntity), nameof(PersistentEntity.isWrecked));
			var getLinkedEntityMatch = new CodeMatch(OpCodes.Call, getLinkedEntityMethodInfo);
			var instantiateActionMatch = new CodeMatch(OpCodes.Call, instantiateActionMethodInfo);
			var loadCombatUnitMatch = new CodeMatch(OpCodes.Ldarg_0);
			var isConcussed = new CodeInstruction(OpCodes.Callvirt, isConcussedMethodInfo);
			var isWrecked = new CodeInstruction(OpCodes.Callvirt, isWreckedMethodInfo);

			cm.Start();
			var loadCombatEntity = cm.Instruction.Clone();

			cm.MatchEndForward(getLinkedEntityMatch)
				.Advance(2);
			var loadUnitEntity = cm.Instruction.Clone();

			cm.MatchEndForward(instantiateActionMatch)
				.Advance(2);
			cm.CreateLabel(out var skipInstantiateLabel);
			var skipInstantiate = new CodeInstruction(OpCodes.Brtrue_S, skipInstantiateLabel);

			cm.Advance(-1)
				.MatchStartBackwards(loadCombatUnitMatch);
			cm.CreateLabel(out var skipWreckCheckLabel);
			var skipWreckCheck = new CodeInstruction(OpCodes.Brfalse_S, skipWreckCheckLabel);

			cm.InsertAndAdvance(loadCombatEntity)
				.InsertAndAdvance(isConcussed)
				.InsertAndAdvance(skipInstantiate)
				.InsertAndAdvance(loadUnitEntity)
				.InsertAndAdvance(skipWreckCheck)
				.InsertAndAdvance(loadUnitEntity)
				.InsertAndAdvance(isWrecked)
				.InsertAndAdvance(skipInstantiate);

			return cm.InstructionEnumeration();
		}
	}
}
