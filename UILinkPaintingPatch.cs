// Copyright (c) 2024 EchKode
// SPDX-License-Identifier: BSD-3-Clause

using HarmonyLib;

namespace EchKode.PBMods.CombatTimelineFixes
{
	static class UILinkPaintingPatch
	{
		internal static void ShowWarningLate()
		{
			CIViewCombatTimeline.ins.hideableWarningLate.SetVisible(true);
			var t = new Traverse(CIViewCombatTimeline.ins);
			t.Field<bool>("warningTimeoutLock").Value = true;
		}
	}
}
