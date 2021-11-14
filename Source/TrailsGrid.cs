using System.Collections.Generic;
using Verse;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Linq;
using System;
using static Verse.AI.PathFinder;
using UnityEngine;
namespace RimThreaded
{
    public enum Trail_Direction
    {
        ToDest,
        ToSouce
    }
    public class Trail : IExposable, ILoadReferenceable, ICellBoolGiver
    {

        private struct PathFinderTrailNode
        {
            public int Prev;

            public int Curr;

            public int Next;
        }
        public int Source;

        public int ID = -1;

        public int Destination;

        private List<PathFinderTrailNode> trail = new List<PathFinderTrailNode>();

        private Map map;

        private Color colorInt = Color.red;

        private Texture2D colorTextureInt;

        private CellBoolDrawer drawer;

        private BoolGrid innerGrid;

        public Color Color => colorInt;
        public Trail(Map map)
        {
            this.map = map;
            colorInt = new Color(Rand.Value, Rand.Value, Rand.Value);
            colorInt = Color.Lerp(colorInt, Color.gray, 0.5f);
            innerGrid = new BoolGrid(map);
            ID = Find.UniqueIDsManager.GetNextAreaID();//it should be fine to use a preexisting IDManager
        }
        public void ExposeData()
        {
            Scribe_Values.Look(ref ID, "ID", -1);
            Scribe_Deep.Look(ref innerGrid, "innerGrid");
            Scribe_Values.Look(ref colorInt, "color");
            Scribe_Collections.Look(ref trail, "trail");
            Scribe_Values.Look(ref Source, "source");
            Scribe_Values.Look(ref Destination, "destination");
        }
        public string GetUniqueLoadID()
        {
            return "Trail_" + ID;
        }
        public Color GetCellExtraColor(int index)
        {
            return Color.white;
        }
        public bool GetCellBool(int index)
        {
            return innerGrid[index];
        }
        public void TrailUpdate() // must go in the Map.MapUpdate
        {
            Drawer.CellBoolDrawerUpdate();
        }
        private CellBoolDrawer Drawer
        {
            get
            {
                if (drawer == null)
                {
                    drawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3650);
                }
                return drawer;
            }
        }
        public Texture2D ColorTexture
        {
            get
            {
                if (colorTextureInt == null)
                {
                    colorTextureInt = SolidColorMaterials.NewSolidColorTexture(colorInt);
                }
                return colorTextureInt;
            }
        }
        private List<IntVec3> OrderTrail(List<IntVec3> nodes)// will loop for ever of cyclic trails... that should be forbidden
        {
            bool isBlind;
            IntVec3 prev = IntVec3.Invalid;
            IntVec3 v = nodes.RandomElement();
            for (; ; )
            {
                isBlind = true;
                foreach (IntVec3 n in nodes)
                {
                    if (v.AdjacentTo8Way(n) && n != prev)
                    {
                        prev = v;
                        v = n;
                        isBlind = false;
                    }
                }
                if (isBlind)
                    break;
            }
            List<IntVec3> result = new List<IntVec3>();
            prev = IntVec3.Invalid;
            result.Add(v);
            for (; ; )
            {
                isBlind = true;
                foreach (IntVec3 n in nodes)
                {
                    if (v.AdjacentTo8Way(n) && n != prev)
                    {
                        result.Add(n);
                        prev = v;
                        v = n;
                        isBlind = false;
                    }
                }
                if (isBlind)
                    break;
            }
            return result;
        }
        public void InitializeTrail(List<IntVec3> nodes)
        {
            if (nodes is null)
                return;
            nodes = OrderTrail(nodes);
            for (int i = 0; i != nodes.Count; i++)
            {

                PathFinderTrailNode TrailNode = new PathFinderTrailNode();

                TrailNode.Curr = map.cellIndices.CellToIndex(nodes[i]);

                innerGrid.Set(TrailNode.Curr, true);

                if (i == 0)//you are the first element
                {
                    Source = TrailNode.Curr;
                    TrailNode.Prev = TrailNode.Curr;
                }
                if (i == nodes.Count - 1)//you are the last
                {
                    Destination = TrailNode.Curr;
                    TrailNode.Prev = trail[i - 1].Curr;
                    TrailNode.Next = TrailNode.Curr;
                }
                else
                    TrailNode.Prev = trail[i - 1].Curr;

                trail.Add(TrailNode);
            }

            for (int i = nodes.Count - 1; i != 0; i--)
            {
                PathFinderTrailNode CurrNode = trail[i];
                PathFinderTrailNode PrevNode = trail[i-1];
                PrevNode.Next = CurrNode.Curr;
            }
        }

        public float GetDestDistanceFrom(int cellIndexDest)
        {
            return map.cellIndices.IndexToCell(Destination).DistanceToSquared(map.cellIndices.IndexToCell(cellIndexDest));
        }
        public float GetSourceDistanceFrom(int cellIndexDest)
        {
            return map.cellIndices.IndexToCell(Source).DistanceToSquared(map.cellIndices.IndexToCell(cellIndexDest));
        }

        public IEnumerable<int> GetCellIndexes()
        {
            foreach (PathFinderTrailNode n in trail)
            {
                yield return n.Curr;
            }
        }
        public int GetBestExit(int destination)
        {
            PathFinderTrailNode n = trail.Aggregate((x, y) =>
           (map.cellIndices.IndexToCell(x.Curr).DistanceToSquared(map.cellIndices.IndexToCell(destination)) < map.cellIndices.IndexToCell(y.Curr).DistanceToSquared(map.cellIndices.IndexToCell(destination))) ?
           x : y);
            return n.Curr;
        }
    }
    public class TrailsGrid
    {
        private List<List<Trail>> Trails; // map cellindex to trail.
        private Map map;
        public TrailsGrid(Map map)
        {
            this.map = map;
            Trails = new List<List<Trail>>(map.cellIndices.NumGridCells);
            for (int i = 0; i < map.cellIndices.NumGridCells; i++)
            {
                Trails[i] = new List<Trail>();
            }
        }
        public bool TryGetBestTrail(int CurIndex, int DestIndex, ref int NewCurIndex, ref CostNode costNode, ref PathFinderNodeFast[] CalcGrid)
        {
            if (!InTrail(CurIndex))
            {
                return false;
            }

            //lets' get the trails with the minimum source or destination distance to where we want to go.
            Trail t = Trails[CurIndex].Aggregate((x, y) =>
           (Math.Min(x.GetDestDistanceFrom(DestIndex), x.GetSourceDistanceFrom(DestIndex)) < Math.Min(y.GetDestDistanceFrom(DestIndex), y.GetSourceDistanceFrom(DestIndex))) ?
           x : y);

            //if the minimum trail distance to the destination is bigger than our current destination to the destination then we don't follow it

            if (Math.Min(t.GetDestDistanceFrom(DestIndex), t.GetSourceDistanceFrom(DestIndex)) >
                map.cellIndices.IndexToCell(CurIndex).DistanceToSquared(map.cellIndices.IndexToCell(DestIndex)))
                return false;

            if ((t.GetSourceDistanceFrom(DestIndex) > t.GetDestDistanceFrom(DestIndex)))
            {
                NewCurIndex = t.Destination;
                //t.MergePath(CalcGrid, CurIndex, Trail_Direction.ToDest);
            }
            else
            {
                NewCurIndex = t.Source;
                //t.MergePath(CalcGrid, CurIndex, Trail_Direction.ToSouce);
            }

            costNode = new CostNode(NewCurIndex, CalcGrid[CurIndex].costNodeCost);
            return true;

        }
        public bool TryGetBestTrailAndBestExit(int CurIndex, int DestIndex, ref int NewCurIndex, ref CostNode costNode, ref PathFinderNodeFast[] CalcGrid)
        {
            if (!InTrail(CurIndex))
            {
                return false;
            }

            //lets' get the trails with the minimum source or destination distance to where we want to go.
            Trail t = Trails[CurIndex].Aggregate((x, y) =>
           (Math.Min(x.GetDestDistanceFrom(DestIndex), x.GetSourceDistanceFrom(DestIndex)) < Math.Min(y.GetDestDistanceFrom(DestIndex), y.GetSourceDistanceFrom(DestIndex))) ?
           x : y);

            //if the minimum trail distance to the destination is bigger than our current destination to the destination then we don't follow it

            if (Math.Min(t.GetDestDistanceFrom(DestIndex), t.GetSourceDistanceFrom(DestIndex)) >
                map.cellIndices.IndexToCell(CurIndex).DistanceToSquared(map.cellIndices.IndexToCell(DestIndex)))
                return false;

            if ((t.GetSourceDistanceFrom(DestIndex) > t.GetDestDistanceFrom(DestIndex)))
            {
                NewCurIndex = t.GetBestExit(DestIndex);
                //t.MergePath(CalcGrid, CurIndex, Trail_Direction.ToDest, NewCurIndex);
            }
            else
            {
                NewCurIndex = t.GetBestExit(DestIndex);
                //t.MergePath(CalcGrid, CurIndex, Trail_Direction.ToSouce, NewCurIndex);
            }

            costNode = new CostNode(NewCurIndex, CalcGrid[CurIndex].costNodeCost);
            return true;

        }
        private bool InTrail(int cellIndex)
        {
            return Trails[cellIndex].Count > 0;
        }
        public void CreateTrail(List<IntVec3> nodes)
        {
            Trail trail = new Trail(map);
            trail.InitializeTrail(nodes);
            foreach (int cellindex in trail.GetCellIndexes())
            {
                Trails[cellindex].Add(trail);
            }
        }
    }
    /*
public class LRUCache<K, V> //this implementation maybe can be decent with an heuristic of distances to curDest to CachedPathDest
{
   private int capacity;
   public Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>> cacheMap = new Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>>();
   private LinkedList<LRUCacheItem<K, V>> lruList = new LinkedList<LRUCacheItem<K, V>>();

   public LRUCache(int capacity)
   {
       this.capacity = capacity;
   }
   public void Clear()
   {
       cacheMap.Clear();
       lruList.Clear();
   }
   public bool ContainsKey(K key)
   {
       if (cacheMap.ContainsKey(key))
           return true;
       return false;
   }

   [MethodImpl(MethodImplOptions.Synchronized)]
   public V get(K key)
   {
       LinkedListNode<LRUCacheItem<K, V>> node;
       if (cacheMap.TryGetValue(key, out node))
       {
           V value = node.Value.value;
           lruList.Remove(node);
           lruList.AddLast(node);
           return value;
       }
       return default(V);
   }

   [MethodImpl(MethodImplOptions.Synchronized)]
   public void add(K key, V val)
   {
       if (cacheMap.TryGetValue(key, out var existingNode))
       {
           lruList.Remove(existingNode);
       }
       else if (cacheMap.Count >= capacity)
       {
           RemoveFirst();
       }

       LRUCacheItem<K, V> cacheItem = new LRUCacheItem<K, V>(key, val);
       LinkedListNode<LRUCacheItem<K, V>> node = new LinkedListNode<LRUCacheItem<K, V>>(cacheItem);
       lruList.AddLast(node);
       cacheMap[key] = node;
   }

   private void RemoveFirst()
   {
       LinkedListNode<LRUCacheItem<K, V>> node = lruList.First;
       lruList.RemoveFirst();

       cacheMap.Remove(node.Value.key);
   }
}

public class LRUCacheItem<K, V>
{
   public LRUCacheItem(K k, V v)
   {
       key = k;
       value = v;
   }
   public K key;
   public V value;
}


//we still need to remove PawnPaths that due to map changes become not usable
//maybe we could simply clear all the pawnPaths every 1k or 2k ticks
public static class Trails
{
   private static int MAXIMUMCACHABLEPATHS = 1000;

   private static ReaderWriterLockSlim PawnPathCacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);//still to finish

   private static Dictionary<Map, LRUCache<int, PawnPath>> MapTODestIndexTOPawnPath = new Dictionary<Map, LRUCache<int, PawnPath>>();


   public static void InitializePawnPathCache(Map map)// this must go in the pathfinder constructor
   {
       PawnPathCacheLock.EnterWriteLock();
       try
       {
           MapTODestIndexTOPawnPath[map] = new LRUCache<int, PawnPath>(capacity: MAXIMUMCACHABLEPATHS);
       }
       finally
       {
           PawnPathCacheLock.ExitWriteLock();
       }
   }
   public static void ClearAllTrails(Map map)
   {
       PawnPathCacheLock.EnterWriteLock();
       try
       {
           MapTODestIndexTOPawnPath[map].Clear();
       }
       finally
       {
           PawnPathCacheLock.ExitWriteLock();
       }
   }

   public static void Add(Map map, PawnPath path, int destIndex)
   {
       PawnPathCacheLock.EnterWriteLock();
       try
       {
           MapTODestIndexTOPawnPath[map].add(destIndex, path);
       }
       finally
       {
           PawnPathCacheLock.ExitWriteLock();
       }
   }

   public static bool TryCheckFromCurrentIndex(Map map, int destIndex, int currentIndex, PawnPath incompletePath, out PawnPath result)
   {
       result = null;
       PawnPathCacheLock.EnterReadLock();
       try
       {
           if (!MapTODestIndexTOPawnPath[map].ContainsKey(destIndex))
               return false;
           if (IndexContainedInPath(MapTODestIndexTOPawnPath[map].cacheMap[destIndex].Value.value, currentIndex, map))//the reason why I don't use a get here is because I don't want to consinder the element as used yet.
           {
               //this is where the magic should happen we need to merge the PawnPaths because at this point we know that we already have a path from the currentIndex to the same destination.
               result = PathMerge(incompletePath, MapTODestIndexTOPawnPath[map].get(destIndex), map, currentIndex);
               return true;
           }
       }
       finally
       {
           PawnPathCacheLock.ExitReadLock();
       }
       return false;
   }
   internal static PawnPath PathMerge(PawnPath incompletePath, PawnPath ToTarget, Map map , int currentIndex)
   {
       PawnPath emptyPawnPath = map.pawnPathPool.GetEmptyPawnPath();
       emptyPawnPath.nodes.AddRange(incompletePath.nodes);
       foreach (IntVec3 pos in ToTarget.nodes) // assuming they got added from dest to start... that is what I am understanding from PathFinder.FinalizedPath
       {
           if (pos == map.cellIndices.IndexToCell(currentIndex))
               break;
           emptyPawnPath.AddNode(pos);
       }
       emptyPawnPath.SetupFound(incompletePath.totalCostInt + ToTarget.totalCostInt, incompletePath.UsedRegionHeuristics || ToTarget.UsedRegionHeuristics);
       return emptyPawnPath;
   }

   internal static bool IndexContainedInPath(PawnPath path, int index, Map map)
   {
       if (path.nodes.Contains(map.cellIndices.IndexToCell(index)))
           return true;
       return false;
   }

}*/
}
