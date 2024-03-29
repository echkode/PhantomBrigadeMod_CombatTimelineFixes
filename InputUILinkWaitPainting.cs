﻿// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;
using PhantomBrigade.Combat.Components;
using PhantomBrigade.Data;
using PhantomBrigade.Input.Systems;

namespace EchKode.PBMods.CombatTimelineFixes
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(InputUILinkWaitPainting), "Execute", new System.Type[] { typeof(List<InputEntity>) })]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Iuilwp_ExecuteTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// If action start time is past max placement time, show late placement warning toast.

			var cm = new CodeMatcher(instructions, generator);
			var getLastActionEndTimeMethodInfo = AccessTools.DeclaredMethod(
				typeof(ActionUtility),
				nameof(ActionUtility.GetLastActionTime),
				new System.Type[]
				{
					typeof(CombatEntity),
					typeof(bool),
				});
			var getCurrentTurnMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatContext), nameof(CombatContext.currentTurn));
			var getTurnLengthMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(CombatContext), nameof(CombatContext.turnLength));
			var getSimMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(DataShortcuts), nameof(DataShortcuts.sim));
			var getLastActionEndTimeMatch = new CodeMatch(OpCodes.Call, getLastActionEndTimeMethodInfo);
			var loadThis = new CodeInstruction(OpCodes.Ldarg_0);
			var loadCombatContext = CodeInstruction.LoadField(typeof(InputUILinkWaitPainting), "combat");
			var getCurrentTurn = new CodeInstruction(OpCodes.Callvirt, getCurrentTurnMethodInfo);
			var loadCurrentTurn = CodeInstruction.LoadField(typeof(CurrentTurn), nameof(CurrentTurn.i));
			var getTurnLength = new CodeInstruction(OpCodes.Callvirt, getTurnLengthMethodInfo);
			var loadTurnLength = CodeInstruction.LoadField(typeof(TurnLength), nameof(TurnLength.i));
			var mul = new CodeInstruction(OpCodes.Mul);
			var convertFloat = new CodeInstruction(OpCodes.Conv_R4);
			var getSim = new CodeInstruction(OpCodes.Call, getSimMethodInfo);
			var loadMaxPlacementTime = CodeInstruction.LoadField(typeof(DataContainerSettingsSimulation), nameof(DataContainerSettingsSimulation.maxActionTimePlacement));
			var add = new CodeInstruction(OpCodes.Add);
			var showToast = CodeInstruction.Call(typeof(UILinkPaintingPatch), nameof(UILinkPaintingPatch.ShowWarningLate));

			cm.MatchEndForward(getLastActionEndTimeMatch)
				.Advance(3);
			var loadActionStartTime = cm.Instruction.Clone();

			cm.Advance(-1);
			cm.CreateLabel(out var skipToastLabel);
			var skipToast = new CodeInstruction(OpCodes.Ble_Un_S, skipToastLabel);

			cm.InsertAndAdvance(loadActionStartTime)
				.InsertAndAdvance(loadThis)
				.InsertAndAdvance(loadCombatContext)
				.InsertAndAdvance(getCurrentTurn)
				.InsertAndAdvance(loadCurrentTurn)
				.InsertAndAdvance(loadThis)
				.InsertAndAdvance(loadCombatContext)
				.InsertAndAdvance(getTurnLength)
				.InsertAndAdvance(loadTurnLength)
				.InsertAndAdvance(mul)
				.InsertAndAdvance(convertFloat)
				.InsertAndAdvance(getSim)
				.InsertAndAdvance(loadMaxPlacementTime)
				.InsertAndAdvance(add)
				.InsertAndAdvance(skipToast)
				.InsertAndAdvance(showToast);


			return cm.InstructionEnumeration();
		}
	}
}
