using System;
using UnityEngine;
namespace Game
{
    public sealed class RiderResult
    {
        public int Slot;
        public string Nick;
        public ulong SteamId;
        public int Points, Pickups;
        public float Distance;
    }
    /// <summary>Individual contributions on the shared board, frozen when the run ends.</summary>
    public sealed class RunLeaderboard
    {
        readonly RiderResult[] _riders=new RiderResult[6];
        public RiderResult[] Results { get; private set; }=Array.Empty<RiderResult>();
        public RunLeaderboard(){for(int i=0;i<6;i++)_riders[i]=new RiderResult{Slot=i,Nick=FallbackName(i)};}
        public string Nick(int slot)=>_riders[slot].Nick;
        static readonly string[] BotNames={"Mavi", "Kızıl", "Yeşil", "Sarı", "Mor", "Turuncu"};
        static string FallbackName(int slot)=>slot==0?"Yerel oyuncu":"Bot · "+BotNames[slot];
        // The lobby adapter supplies Steam persona names; these are never editable menu fields.
        public void SetSteamIdentity(int slot,ulong steamId,string personaName)
        {
            if(slot<0||slot>=_riders.Length)throw new ArgumentOutOfRangeException(nameof(slot));
            _riders[slot].SteamId=steamId;
            _riders[slot].Nick=string.IsNullOrWhiteSpace(personaName)?FallbackName(slot):personaName.Trim();
        }
        public void Reset(){foreach(var rider in _riders){rider.Points=rider.Pickups=0;rider.Distance=0;}Results=Array.Empty<RiderResult>();}
        public void Travel(int slot,float distance){if(distance>0)_riders[slot].Distance+=distance;}
        public void Award(int slot,int points){if(slot<0||slot>=6)return;_riders[slot].Points+=points;_riders[slot].Pickups++;}
        public int Score(int slot)=>Mathf.FloorToInt(_riders[slot].Distance)+_riders[slot].Points;
        public void Finish(int count)
        {
            Results=new RiderResult[count];
            for(int i=0;i<count;i++)Results[i]=new RiderResult{Slot=i,SteamId=_riders[i].SteamId,Nick=_riders[i].Nick,Points=Score(i),Pickups=_riders[i].Pickups,Distance=_riders[i].Distance};
            Array.Sort(Results,(a,b)=>{int score=b.Points.CompareTo(a.Points);return score!=0?score:a.Slot.CompareTo(b.Slot);});
        }
    }
}
