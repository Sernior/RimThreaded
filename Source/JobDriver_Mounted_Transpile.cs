﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using System.Reflection.Emit;
using RimWorld;

namespace RimThreaded
{
    public class JobDriver_Mounted_Transpile
    {
        public static IEnumerable<CodeInstruction> WaitForRider(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
        {
            List<CodeInstruction> searchInstructions = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(RimThreadedHarmony.giddyUpCoreJobsJobDriver_Mounted, "get_Rider")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Pawn), "get_CurJob")),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Job), "def")),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(JobDefOf), "Mount")),
                new CodeInstruction(OpCodes.Beq_S)
            }; 
            List<CodeInstruction> searchInstructions2 = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(RimThreadedHarmony.giddyUpCoreJobsJobDriver_Mounted, "get_Rider")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Pawn), "get_CurJob")),
            };
            List<CodeInstruction> instructionsList = instructions.ToList();
            int currentInstructionIndex = 0;
            bool matchFound = false;
            LocalBuilder curJob = iLGenerator.DeclareLocal(typeof(Job));
            while (currentInstructionIndex < instructionsList.Count)
            {
                if (RimThreadedHarmony.IsCodeInstructionsMatching(searchInstructions, instructionsList, currentInstructionIndex))
                {
                    matchFound = true;
                    for (int i = 0; i < 3; i++)
                    {
                        yield return instructionsList[currentInstructionIndex];
                        currentInstructionIndex++;
                    }
                    
                    yield return new CodeInstruction(OpCodes.Stloc, curJob.LocalIndex);
                    yield return new CodeInstruction(OpCodes.Ldloc, curJob.LocalIndex);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, 
                        instructionsList[currentInstructionIndex + 2].operand); //this may need to be a jump to a line 5 lines above this
                    yield return new CodeInstruction(OpCodes.Ldloc, curJob.LocalIndex);
                    for (int i = 0; i < 3; i++)
                    {
                        yield return instructionsList[currentInstructionIndex];
                        currentInstructionIndex++;
                    }
                }
                else if (RimThreadedHarmony.IsCodeInstructionsMatching(searchInstructions2, instructionsList, currentInstructionIndex))
                {
                    matchFound = true;
                    yield return new CodeInstruction(OpCodes.Ldloc, curJob.LocalIndex);
                    for (int i = 0; i < 3; i++)
                    {
                        yield return instructionsList[currentInstructionIndex];
                        currentInstructionIndex++;
                    }
                }
                else
                {
                    yield return instructionsList[currentInstructionIndex];
                    currentInstructionIndex++;
                }
            }
            if (!matchFound)
            {
                Log.Error("IL code instructions not found");
            }
        }
    }
}
