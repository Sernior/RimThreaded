using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using RimWorld;
using Verse;
using PN = Priority_Queue.FastPriorityQueueNode;

namespace RimThreaded
{
    using PQ = Priority_Queue.FastPriorityQueue<Tickable>;
    public class Tickable : PN
    {
        public Thing instance;
        public Tickable(Thing o)
        {
            instance = o;
        }
    }
    public class ThingTickLimiter // the diamond problem prevents me to put generics with constraints here
    {
        private PQ DeadTicks = new PQ(65536);
        private HashSet<Thing> InQueue = new HashSet<Thing>(); 
        private float timer = 0.0f;
        private const float TIMETOREFILL = 1.0f;

        public void Tick()
        {
            timer+=Time.deltaTime;
            if(timer > TIMETOREFILL)
            {
                timer = 0.0f;
                RimThreaded.Tokens = 200;
            }
        }
        public bool DeadLetterDequeue(out Thing result)
        {
            result = null;
            if (DeadTicks.Count == 0)
            {
                return false;
            }
            RimThreaded.Tokens -= 1;
            if (RimThreaded.Tokens < 0)
            {
                return false;
            }


            Thing returnValue = DeadTicks.Dequeue().instance;
            InQueue.Remove(returnValue);
            result = returnValue;

            return true;
        }
        public bool TickRequest(Thing requester)
        {
            RimThreaded.Tokens -= 1;
            bool result = RimThreaded.Tokens >= 0;
            if (!result)
            {
                Tickable n = new Tickable(requester);
                if (InQueue.Contains(requester)) return result;// we try to enqueue someone who is already in the queue so we do nothing. Otherwise we could increase his priority (to test).

                InQueue.Add(requester);
                DeadTicks.Enqueue(n,Find.TickManager.TicksGame);//500 t1:500 t2:501
            }
            return result;
        }
    }
}
