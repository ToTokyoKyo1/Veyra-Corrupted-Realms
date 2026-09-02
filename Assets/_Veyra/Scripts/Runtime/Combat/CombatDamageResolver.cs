using System;

namespace Veyra.Combat
{
    public readonly struct CombatDamageResolution
    {
        public CombatDamageResolution(int requestedDamage, int appliedDamage, bool blockedByGuard)
        {
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            BlockedByGuard = blockedByGuard;
        }

        public int RequestedDamage { get; }
        public int AppliedDamage { get; }
        public bool BlockedByGuard { get; }
    }

    /// <summary>
    /// Single authority for direct combat damage. A normal prepared guard always means zero
    /// damage. Future exceptional moves must opt in explicitly through ignoresGuard.
    /// </summary>
    public static class CombatDamageResolver
    {
        public static CombatDamageResolution Resolve(
            int requestedDamage,
            bool guardPrepared,
            bool ignoresGuard = false)
        {
            int safeDamage = Math.Max(0, requestedDamage);
            bool blocked = guardPrepared && !ignoresGuard;
            return new CombatDamageResolution(
                safeDamage,
                blocked ? 0 : safeDamage,
                blocked);
        }
    }
}
