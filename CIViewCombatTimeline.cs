// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

using PhantomBrigade;

using UnityEngine;

namespace EchKode.PBMods.CombatTimelineFixes
{
	static partial class Patch
	{
		[HarmonyPatch(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.ConfigureActionPlanned))]
		[HarmonyPostfix]
		static void Civct_ConfigureActionPlannedPostfix(CIHelperTimelineAction helper)
		{
			var button = helper.button;
			if (button.tooltipUsed && !ModLink.Settings.showActionTooltips)
			{
				button.RemoveTooltip();
				return;
			}

			if (!button.tooltipUsed && ModLink.Settings.showActionTooltips)
			{
				button.tooltipUsed = true;
			}
		}

		[HarmonyPatch(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.ConfigureActionPlanned))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_ConfigureActionPlannedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Don't enable drag callback for locked actions.

			var cm = new CodeMatcher(instructions, generator);
			var isLockedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.isLocked));
			var onActionDragMethodInfo = AccessTools.DeclaredMethod(typeof(CIViewCombatTimeline), "OnActionDrag");
			var tooltipUsedFieldInfo = AccessTools.DeclaredField(typeof(CIButton), nameof(CIButton.tooltipUsed));
			var isLockedMatch = new CodeMatch(OpCodes.Callvirt, isLockedMethodInfo);
			var onActionDragMatch = new CodeMatch(OpCodes.Ldftn, onActionDragMethodInfo);
			var tooltipUsedMatch = new CodeMatch(OpCodes.Stfld, tooltipUsedFieldInfo);

			cm.End()
				.MatchStartBackwards(tooltipUsedMatch)
				.Advance(-2);
			cm.CreateLabel(out var skipDragLabel);
			var skipDrag = new CodeInstruction(OpCodes.Brtrue_S, skipDragLabel);

			cm.Start()
				.MatchStartForward(isLockedMatch)
				.Advance(-1);
			var isLocked = cm.Instructions(2);

			cm.MatchStartForward(onActionDragMatch)
				.Advance(-2);
			cm.InsertAndAdvance(isLocked)
				.InsertAndAdvance(skipDrag);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.ConfigureActionPlanned))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_ConfigureActionPlannedTranspiler2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Position the tooltip flyout at the end of the action but don't let the tooltip offset go beyond
			// the end of the timeline.

			var cm = new CodeMatcher(instructions, generator);
			var overshootLabel = generator.DeclareLocal(typeof(float));
			var roundToIntMethodInfo = AccessTools.DeclaredMethod(typeof(Mathf), nameof(Mathf.RoundToInt));
			var vector3ConstructorInfo = AccessTools.Constructor(typeof(Vector3), new System.Type[]
			{
				typeof(float),
				typeof(float),
				typeof(float),
			});
			var getIsOnPrimaryTrackMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.isOnPrimaryTrack));
			var roundToIntMatch = new CodeMatch(OpCodes.Call, roundToIntMethodInfo);
			var vector3Match = new CodeMatch(OpCodes.Newobj, vector3ConstructorInfo);
			var negMatch = new CodeMatch(OpCodes.Neg);
			var getIsOnPrimaryTrackMatch = new CodeMatch(OpCodes.Callvirt, getIsOnPrimaryTrackMethodInfo);
			var convFloat = new CodeInstruction(OpCodes.Conv_R4);
			var loadLeftOffset = CodeInstruction.LoadField(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.timelineOffsetLeft));
			var loadTimelineSize = CodeInstruction.LoadField(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.timelineSize));
			var add = new CodeInstruction(OpCodes.Add);
			var loadThis = new CodeInstruction(OpCodes.Ldarg_0);
			var dupe = new CodeInstruction(OpCodes.Dup);
			var sub = new CodeInstruction(OpCodes.Sub);
			var storeOvershoot = new CodeInstruction(OpCodes.Stloc_S, overshootLabel);
			var loadOvershoot = new CodeInstruction(OpCodes.Ldloc_S, overshootLabel);

			cm.MatchEndForward(roundToIntMatch)
				.Advance(1);
			var loadOffset = new CodeInstruction(OpCodes.Ldloc_S, cm.Operand);

			cm.End()
				.MatchStartBackwards(vector3Match)
				.MatchStartBackwards(negMatch)
				.Advance(-1);
			var loadX = cm.Instruction.Clone();

			cm.MatchStartBackwards(vector3Match)
				.MatchStartBackwards(getIsOnPrimaryTrackMatch)
				.Advance(-2)
				.SetInstructionAndAdvance(loadOffset);
			cm.CreateLabel(out var skipLabel);
			var skip = new CodeInstruction(OpCodes.Blt_Un_S, skipLabel);

			cm.InsertAndAdvance(convFloat)
				.InsertAndAdvance(loadX)
				.InsertAndAdvance(loadOffset)
				.InsertAndAdvance(convFloat)
				.InsertAndAdvance(add)
				.InsertAndAdvance(loadThis)
				.InsertAndAdvance(loadLeftOffset)
				.InsertAndAdvance(loadThis)
				.InsertAndAdvance(loadTimelineSize)
				.InsertAndAdvance(add)
				.InsertAndAdvance(convFloat)
				.InsertAndAdvance(skip)
				.InsertAndAdvance(dupe)
				.InsertAndAdvance(loadX)
				.InsertAndAdvance(loadOffset)
				.InsertAndAdvance(convFloat)
				.InsertAndAdvance(add)
				.InsertAndAdvance(loadThis)
				.InsertAndAdvance(loadLeftOffset)
				.InsertAndAdvance(loadThis)
				.InsertAndAdvance(loadTimelineSize)
				.InsertAndAdvance(add)
				.InsertAndAdvance(convFloat)
				.InsertAndAdvance(sub)
				.InsertAndAdvance(storeOvershoot)
				.InsertAndAdvance(loadOvershoot)
				.Insert(loadOvershoot.Clone());
			cm.CreateLabel(out var skipToSubLabel);
			var skipToSub = new CodeInstruction(OpCodes.Bge_Un_S, skipToSubLabel);

			cm.InsertAndAdvance(skipToSub)
				.InsertAndAdvance(loadOffset)
				.InsertAndAdvance(storeOvershoot)
				.Advance(1)
				.InsertAndAdvance(sub);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CIViewCombatTimeline), "OnActionDrag")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_OnActionDragTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Prevent dragging locked actions.

			var cm = new CodeMatcher(instructions, generator);
			var hasActionOwnerMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.hasActionOwner));
			var isLockedMethodInfo = AccessTools.DeclaredPropertyGetter(typeof(ActionEntity), nameof(ActionEntity.isLocked));
			var hasActionOwnerMatch = new CodeMatch(OpCodes.Callvirt, hasActionOwnerMethodInfo);
			var isLocked = new CodeInstruction(OpCodes.Callvirt, isLockedMethodInfo);
			var ret = new CodeInstruction(OpCodes.Ret);

			cm.MatchStartForward(hasActionOwnerMatch)
				.Advance(-1);
			var loadAction = cm.Instruction.Clone();
			cm.CreateLabel(out var skipRetLabel);
			var skipRet = new CodeInstruction(OpCodes.Brfalse_S, skipRetLabel);
			cm.InsertAndAdvance(loadAction)
				.InsertAndAdvance(isLocked)
				.InsertAndAdvance(skipRet)
				.InsertAndAdvance(ret);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.OnActionSelected))]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_OnActionSelectedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Remove cancel when an action that isn't cancelled is selected.

			var cm = new CodeMatcher(instructions, generator);
			var retMatch = new CodeMatch(OpCodes.Ret);
			var loadTimeline = CodeInstruction.LoadField(typeof(CIViewCombatTimeline), nameof(CIViewCombatTimeline.ins));
			var loadLastRemoveID = CodeInstruction.LoadField(typeof(CIViewCombatTimeline), "idOfActionToRemoveLast");
			var loadActionID = new CodeInstruction(OpCodes.Ldarg_0);
			var removeCancel = CodeInstruction.Call(typeof(CIViewCombatTimeline), "OnActionRemoveCancel");

			cm.MatchEndForward(retMatch)
				.Advance(1);
			var labels = new List<Label>(cm.Labels);
			cm.Labels.Clear();
			cm.CreateLabel(out var skipCancelLabel);
			var skipCancel = new CodeInstruction(OpCodes.Beq_S, skipCancelLabel);
			cm.Insert(loadTimeline.Clone())
				.AddLabels(labels)
				.Advance(1)
				.InsertAndAdvance(loadLastRemoveID)
				.InsertAndAdvance(loadActionID)
				.InsertAndAdvance(skipCancel)
				.InsertAndAdvance(loadTimeline)
				.InsertAndAdvance(removeCancel);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CIViewCombatTimeline), "OnTimelineClick")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_OnTimelineClickTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// If there is a pending action cancel, remove the cancel.

			var cm = new CodeMatcher(instructions, generator);
			var loadTimeline = new CodeInstruction(OpCodes.Ldarg_0);
			var removeCancel = CodeInstruction.Call(typeof(CIViewCombatTimeline), "OnActionRemoveCancel");

			cm.End()
				.InsertAndAdvance(loadTimeline)
				.InsertAndAdvance(removeCancel);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CIViewCombatTimeline), "UpdateScrubbing")]
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Civct_UpdateScrubbingTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			// Cut out extraneous call to CombatUIUtility.IsIntervalOverlapped().

			var cm = new CodeMatcher(instructions, generator);
			var getDurationMethodInfo = AccessTools.DeclaredMethod(typeof(CombatUIUtility), nameof(CombatUIUtility.GetPaintedTimePlacementDuration));
			var getDurationMatch = new CodeMatch(OpCodes.Call, getDurationMethodInfo);
			var popMatch = new CodeMatch(OpCodes.Pop);

			cm.MatchEndForward(getDurationMatch)
				.Advance(2);
			var deletePos = cm.Pos;
			cm.MatchStartForward(popMatch);
			var offset = deletePos - cm.Pos;
			cm.RemoveInstructionsWithOffsets(offset, 0);

			return cm.InstructionEnumeration();
		}

		[HarmonyPatch(typeof(CIViewCombatTimeline), "UpdateWarningTimeouts")]
		[HarmonyPostfix]
		static void Civct_UpdateWarningTimeoutsPostfix()
		{
			// The UILinkPainting patches may show the warning late toast so code needs to be added to
			// hide it when the painted action is canceled.

			var t = new Traverse(CIViewCombatTimeline.ins);
			if (t.Field<bool>("warningTimeoutLock").Value)
			{
				return;
			}

			var warningTimeoutField = t.Field<float>("warningTimeoutLate");
			if (warningTimeoutField.Value > 0f)
			{
				warningTimeoutField.Value -= TimeCustom.unscaledDeltaTime;
				CIViewCombatTimeline.ins.hideableWarningLate.SetVisible(true);
			}
			else
			{
				CIViewCombatTimeline.ins.hideableWarningLate.SetVisible(false);
			}
		}
	}
}
