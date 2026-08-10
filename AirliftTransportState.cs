using UnityEngine;

namespace KellysAirlift
{
    // Purpose-built transport navigation only. It never selects targets or invokes Pilot.Fire.
    internal sealed class AirliftTransportState : PilotBaseState
    {
        private readonly TransportOperation _operation;
        private NativeDefensiveCountermeasures _countermeasures;

        public AirliftTransportState(TransportOperation operation)
        {
            _operation = operation;
            stateDisplayName = "AIRLIFT";
        }

        public override void EnterState(Pilot enteringPilot)
        {
            stateDisplayName = "AIRLIFT " + _operation.Phase;
            if (aircraft != null)
            {
                aircraft.SetGear(_operation.GearDownForDrop);
                _countermeasures = new NativeDefensiveCountermeasures(aircraft);
            }
        }

        public override void UpdateState(Pilot updatingPilot)
        {
            stateDisplayName = "AIRLIFT " + _operation.Phase;
        }

        public override void FixedUpdateState(Pilot updatingPilot)
        {
            if (_operation == null || !_operation.IsActive || aircraft == null || aircraft.autopilot == null) return;
            destination = GlobalPositionExtensions.ToGlobalPosition(_operation.FlightDestination);
            aircraft.SetGear(_operation.GearDownForDrop);
            aircraft.autopilot.AutoAim(destination, false, false, false, 0.85f, 80f, true,
                _operation.TargetRadarAltitude, Vector3.zero);
            if (controlInputs != null)
            {
                controlInputs.throttle = Mathf.Max(controlInputs.throttle, _operation.CruiseThrottle);
                controlInputs.brake = 0f;
            }
            if (_countermeasures != null)
                _countermeasures.Update(_operation.CountermeasureHoldUntil);
        }

        public override void LeaveState()
        {
            if (_countermeasures != null) _countermeasures.Stop();
        }
    }
}
