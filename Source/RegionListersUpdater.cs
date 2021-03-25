﻿using System;
using System.Collections.Generic;
using Verse;

namespace RimThreaded
{
    public class RegionListersUpdater_Patch
    {
		[ThreadStatic] public static List<Region> tmpRegions;

		public static void InitializeThreadStatics()
        {
			tmpRegions = new List<Region>();
		}

		public static bool DeregisterInRegions(Thing thing, Map map)
		{
			if (!ListerThings.EverListable(thing.def, ListerThingsUse.Region))
			{
				return false;
			}
			
			tmpRegions.Clear();		
			
			RegionListersUpdater.GetTouchableRegions(thing, map, tmpRegions, true);
			for (int i = 0; i < tmpRegions.Count; i++)
			{
				ListerThings listerThings = tmpRegions[i].ListerThings;
				lock (listerThings)
				{
					if (listerThings.Contains(thing))
					{
						listerThings.Remove(thing);
					}
				}
			}
			return false;
		}

		public static bool RegisterInRegions(Thing thing, Map map)
		{
			if (!ListerThings.EverListable(thing.def, ListerThingsUse.Region))
			{
				return false;
			}
			tmpRegions.Clear();
			
			RegionListersUpdater.GetTouchableRegions(thing, map, tmpRegions, false);
			for (int i = 0; i < tmpRegions.Count; i++)
			{
				ListerThings listerThings = tmpRegions[i].ListerThings;
				lock (listerThings)
				{
					if (!listerThings.Contains(thing))
					{
						listerThings.Add(thing);
					}
				}
			}
			return false;
		}
		public static bool RegisterAllAt(IntVec3 c, Map map, HashSet<Thing> processedThings = null)
		{
			List<Thing> thingList = c.GetThingList(map);
			int count = thingList.Count;
			for (int i = 0; i < count; i++)
			{
				Thing thing = thingList[i];
				if (processedThings == null || processedThings.Add(thing))
				{
					RegisterInRegions(thing, map);
				}
			}
			return false;
		}
	}
}