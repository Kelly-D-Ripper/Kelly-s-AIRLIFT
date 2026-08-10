using System.Collections.Generic;
using UnityEngine;

namespace KellysAirlift
{
    // Uses the aircraft's native missile-warning list and countermeasure manager.
    // No scene search or combat-state target selection is performed here.
    internal sealed class NativeDefensiveCountermeasures
    {
        private readonly Aircraft _aircraft;
        private float _burstUntil;
        private float _nextBurstAt;

        public NativeDefensiveCountermeasures(Aircraft aircraft)
        {
            _aircraft = aircraft;
        }

        public void Update(float externalHoldUntil)
        {
            if (_aircraft == null || _aircraft.disabled
                || _aircraft.countermeasureManager == null)
                return;
            float now = Time.unscaledTime;
            if (now < externalHoldUntil) return;

            Missile nearest = NearestNativeWarning();
            if (nearest != null && now >= _nextBurstAt)
            {
                string countermeasure =
                    _aircraft.countermeasureManager.ChooseCountermeasure(nearest);
                _nextBurstAt = now + 4f;
                if (string.Equals(countermeasure, "IR",
                        System.StringComparison.OrdinalIgnoreCase))
                    _burstUntil = now + 2f;
            }

            bool shouldFire = nearest != null && now < _burstUntil;
            if (shouldFire != _aircraft.countermeasureTrigger)
                _aircraft.Countermeasures(shouldFire,
                    _aircraft.countermeasureManager.activeIndex);
        }

        public void Stop()
        {
            if (_aircraft != null && !_aircraft.disabled
                && _aircraft.countermeasureManager != null
                && _aircraft.countermeasureTrigger)
                _aircraft.Countermeasures(false,
                    _aircraft.countermeasureManager.activeIndex);
        }

        private Missile NearestNativeWarning()
        {
            MissileWarning warning = _aircraft.GetMissileWarningSystem();
            List<Missile> missiles = warning == null ? null : warning.knownMissiles;
            if (missiles == null) return null;
            Missile nearest = null;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < missiles.Count; index++)
            {
                Missile missile = missiles[index];
                if (missile == null || missile.disabled) continue;
                float distance = (missile.transform.position
                    - _aircraft.transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = missile;
                nearestDistance = distance;
            }
            return nearest;
        }
    }
}
