using System;

namespace LastTide.Core
{
    /// <summary>What a watched advert can pay for. Always the player's choice, never shown unasked.</summary>
    public enum AdReward { Energy, Bag, Travel }

    /// <summary>
    /// Rewarded adverts: a free way to the small things doubloons buy (some energy, the lost backpack, the end of a
    /// short voyage), a few times a day. The rules are here; showing the advert is <c>Game/Platform/Ads</c>.
    /// </summary>
    public sealed partial class Session
    {
        public const int AdsPerDay = 5;
        /// <summary>Energy from one advert: a quarter of the bar (a refill bought with doubloons fills it).</summary>
        public const int AdEnergy = World.EnergyMax / 4;
        /// <summary>The longest voyage left that an advert will finish, in seconds.</summary>
        public const double AdTravelSeconds = 600;

        public int AdsLeft { get { return State.adDay == Today ? Math.Max(0, AdsPerDay - State.adCount) : AdsPerDay; } }

        /// <summary>Whether this reward can be offered right now (something to gain, and adverts left today).</summary>
        public bool AdOffered(AdReward reward)
        {
            if (AdsLeft <= 0) return false;
            switch (reward)
            {
                case AdReward.Energy: return State.energy < World.EnergyMax;
                case AdReward.Bag: return NewestBag != null;
                case AdReward.Travel: return State.travel != null && !Arrived && State.travel.arriveAt - RealNow() <= AdTravelSeconds;
            }
            return false;
        }

        /// <summary>Pays out a watched advert. False (and nothing counted) if there was nothing to give.</summary>
        public bool GrantAd(AdReward reward)
        {
            if (!AdOffered(reward)) return false;
            switch (reward)
            {
                case AdReward.Energy:
                    UpdateEnergy();       // bring the bar up to date before adding to it
                    State.energy = Math.Min(World.EnergyMax, State.energy + AdEnergy);
                    if (State.energy >= World.EnergyMax) State.energyStamp = RealNow();
                    break;
                case AdReward.Bag:
                {
                    var bag = NewestBag;
                    bag.location = State.location; bag.x = State.px; bag.z = State.pz;
                    TakeBag(bag);
                    break;
                }
                case AdReward.Travel: State.travel.arriveAt = RealNow(); break;
            }
            if (State.adDay != Today) { State.adDay = Today; State.adCount = 0; }
            State.adCount++;
            Changed?.Invoke();
            return true;
        }
    }
}
