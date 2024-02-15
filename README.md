# CombatTimelineFixes

This is a collection of bug fixes and other corrections for [Phantom Brigade](https://braceyourselfgames.com/phantom-brigade/). This is not a mod in the traditional sense of an extension to the game that adds a feature or brings in new content. Instead, this is a collection of spot fixes to small bugs I've found in the code around the combat timeline.

These fixes are for release version **1.2.1**.

Many of the fixes use Harmony transpiler patching. All the transpiler patches have at the beginning to describe what is being patched because transpilers work directly with IL and are hard to read and reason about. There are a few fixes that have more in-depth explanations here.

- [CIViewCombatTimeline.ConfigureActionPlanned](#civiewcombattimelineconfigureactionplanned)
- [InputCombatMeleeUtility.AttemptTargeting](#inputcombatmeleeutilityattempttargeting)
- [InputCombatWaitDrawingUtility.AttemptFinish](#inputcombatwaitdrawingutilityattemptfinish)
- [PathUtility.TrimPastMovement](#pathutilitytrimpastmovement)

## CIViewCombatTimeline.ConfigureActionPlanned

When you hover over actions in the combat timeline, a tooltip flyout describing the action will appear. This flyout seems positioned wrong to me. Here are a few screenshots showing how sometimes it's confusing to see the tail hanging off the bottom left corner of the flyout pointing to a different action than the tooltip text is describing.

![Tooltip appears OK for an action near the beginning of the timeline](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/bbd58edc-1d00-49a4-93fa-316395ad3319)
![It's also OK but not great when there's only one action on the track](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/3c1d20fa-da9d-417e-a8e9-369a1e9f2ee7)
![Looks like the tooltip is for the first action when in fact it's for the wait after it](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/bc30a428-f36d-4279-9bf8-fdad77d5647b)
![More confusion with a run action after the wait](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/87cca320-f372-49f1-aa5e-cf723f1cc54e)

Here's how this mod shows the same sequence of actions and tooltips. It's much clearer which action the tooltip is describing.

![Tooltip tail aligns itself nicely with the bevel on the end of the action](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/40d3d1a1-cbc2-4923-bf93-90bdf9966ec7)
![Works on actions on the secondary (upper) track, too](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/6d4d010e-3bba-4a10-9e0f-c143182e8f53)
![No more confusion with the wait action](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/e59ab647-d020-41a7-bee0-a0da7d0a6592)
![Looks great for the last action](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/d11aa88f-f672-4f9e-9708-67aa2ad59e27)

There's only a small bit of jank when an action extends into the next turn. I constrain the tooltip to appear in the area of the combat timeline and add a small buffer to keep it away from the right edge.

![Tooltip on extended action not so great](https://github.com/echkode/PhantomBrigadeMod_CombatTimelineFixes/assets/48565771/c1bc270d-a448-4445-8ca1-5d850bcf9ec8)

## InputCombatMeleeUtility.AttemptTargeting

Melee actions can be placed after the max placement time in a turn and that can cause the game to exit unexpectedly if the player drags an action. Here's a short video demonstrating the melee action being placed far enough into the next turn that only a sliver of it is visible in the timeline.

<video controls src="https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/0eedd238-75d6-491f-98da-a5f712cbf1a5">
  <p>melee action placed in next turn with only a sliver visible in timeline</p>
</video>

Melee actions are double-track actions and there's an algorithm that tries to place them on the timeline without overlapping existing actions in either track. The algorithm first finds the end time of the last action on the primary track (run/wait) and then scans forward from that point on the secondary track (attack/shield). If it finds that the melee action overlaps an action on the secondary track, it will jump ahead to the end time of the overlapped action and continue its scan. Here's a screenshot of the algorithm working correctly with a couple of gapped attack actions.

![Melee action placed after last attack action](https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/e6fc98f0-fdcf-4d98-8431-d59435470ad6)

This algorithm appears in several other places that deal with double-track actions and only in one case does it correctly check that the action is not placed after the max placement time. Here's the code for the algorithm as it appears in this method (remember, this has been translated from IL by a dissambler so the original code probably looks a bit different).
```
float num = ActionUtility.GetLastActionTime(selectedCombatEntity, true);
DataContainerAction entry = DataMultiLinker<DataContainerAction>.GetEntry(input.selectedAction.lookupKey);
float placementDuration = CombatUIUtility.GetPaintedTimePlacementDuration();
if (CombatUIUtility.IsIntervalOverlapped(selectedCombatEntity.id.id, entry, num + 1f / 1000f, placementDuration, out int _, skipSecondaryTrack: false))
{
    for (int index = 0; index < 4; ++index)
    {
        int actionIDIntersected;
        if (CombatUIUtility.IsIntervalOverlapped(selectedCombatEntity.id.id, entry, num + 1f / 1000f, placementDuration, out actionIDIntersected, skipSecondaryTrack: false))
        {
            ActionEntity actionEntity = IDUtility.GetActionEntity(actionIDIntersected);
            if (actionEntity != null)
            {
                num = actionEntity.startTime.f + actionEntity.duration.f;
                if (index == 3)
                {
                    num = ActionUtility.GetLastActionTime(selectedCombatEntity, false);
                    Contexts.sharedInstance.combat.ReplacePredictionTimeTarget(num);
                }
            }
        }
        else
        {
            Contexts.sharedInstance.combat.ReplacePredictionTimeTarget(num);
            break;
        }
    }
}
PaintedPath paintedPath = input.paintedPath;
```
What's interesting is the for loop which has a fixed iteration count. Normally it's a good practice in game code to iterate over known quantities so you can get a rough estimate about how much time your loops will take. However, this situation appears to be a small hack to avoid recursion. Each time you find an overlapped action, you have to change the start time (`num`) and try again.

The problem with this algorithm is its use of `CombatUIUtility.IsIntervalOverlapped()`. That function is used in a number of places to detect overlapping actions. However, it's intended to be a one-off check so it is not suitable to be used in a loop like this where you're walking forward through the timeline. A better way to write this algorithm would be to get the list of actions, sort them by start time and then walk the list checking for overlap.

To fix this bug I would normally add the check for max placement time right before the last line of the code snippet above and be done with it. That's what appears to have happened with dash actions in `InputCombatDashUtilityAttemptTargeting()`. Since this is the second time that the same fix has to be applied to the algorithm in a different area of the code, the proper fix is to put the algorithm into a function and replace all the places where it's used in the code with a call to the new function. I made the new function for the algorithm but I only patched the code in `InputCombatMeleeUtility.AttemptTargeting()` because I'm doing it in IL which is difficult enough to understand without a lot of extra noise. Here's what the patch looks like in C#:
```
float num = ActionUtility.GetLastActionTime(selectedCombatEntity, true);
DataContainerAction entry = DataMultiLinker<DataContainerAction>.GetEntry(input.selectedAction.lookupKey);
float placementDuration = CombatUIUtility.GetPaintedTimePlacementDuration();
(bool ok, float startTime) = CombatUIUtility.TryPlaceAction(selectedCombatEntity.id.id, entry, num + 1f / 1000f, placementDuration, CombatUIUtility.ActionOverlapCheck.SecondaryTrack);
if (!ok)
{
    return;
}
Contexts.sharedInstance.combat.ReplacePredictionTimeTarget(startTime);
PaintedPath paintedPath = input.paintedPath;
```
I folded the max placement time check into the new function (`CombatUIUtility.TryPlaceAction()`) to centralize this algorithm and prevent the bug from reoccurring if the algorithm is needed somewhere else in the future. It makes the intention at the call site clearer as well.

If I were working on the code base directly, I would also make this change in the following places:

- InputUILinkMeleePainting.Redraw()
- InputUILinkDashPainting.Execute()
- InputCombatDashUtility.AttemptTargeting() -- max placement checked correctly here

## InputCombatWaitDrawingUtility.AttemptFinish

Wait actions have the same issue as run actions in that they can be placed after the max time placement in a round. Similar to run actions, this has the potential to cause the game to exit unexpectedly if such a wait action is placed and prior actions are dragged.

Wait actions also have one more trick. If a wait action spans the turn boundary (that is, starts in one turn and finishes in the next), the action is split into two actions with the first action lasting up to the turn boundary and the second action starting on the turn boundary. This creates two problems, one of which is the same as above with an action being created after the max time placement. The second is that runt wait actions can be created with the same issues as runt run actions. This fix prevents splitting a wait action when it spans turns.

## PathUtility.TrimPastMovement

Prevent runt runs from being created. Runt runs are small run actions that appear at the start of a new turn when there is a prior run action that spills over into the new turn.

![Small runt run action at beginning of the turn](https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/4b4ad7bd-b3ce-4892-9345-c50aba30031c)

There are several problems with these runts. When a runt is selected, the selection is shown offset from the action.

![Offset selection of a runt run](https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/c7b10695-7d39-4525-8447-5e338f4f5f33)

Subsquent run actions overlap these runts.

![Overlap of run by a second run action](https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/e7a80191-6dae-44bb-8128-8a6967c9dd44)

The overlap is hard to see because the runts are so small they don't contain the action name so here's the same sequence with the second run moved a bit so you can compare the two.

![Same sequence of runs moved so the overlapped image can be compared to this one](https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/d94c68b0-dffb-4ff7-a47a-87456a3cda30)

The runts don't prevent other run actions from working correctly. That is, only the runt gets overlapped, the rest don't.

![Third run action doesn't overlap the second one](https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/9e1c32c1-3a21-49a2-9e09-6fd6309d4ab3)

Selection is still wonky, however.

![Three selected actions with runt selection offset](https://github.com/echkode/PhantomBrigadeMod_Fixes/assets/48565771/d125084e-ed3a-4dbf-b370-bb7373b6fabf)

The fix looks at the duration of the run action in the new turn and leaves it as a continuation of the action from the previous turn if the duration is less than 0.25s. This constant was found in `ActionUtility.CreatePathAction()` which logs a warning and doesn't create the action when its duration is less than that constant.
