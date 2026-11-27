using System.Numerics;

using Arch.Core;

using FlickerVoid.Engine.Runtime.Components;

using Jolt = JoltPhysicsSharp;

namespace FlickerVoid.Engine.Physics;

/// <summary>The wheeled bodies of the simulation, on Jolt's vehicle constraint: each a dynamic chassis with wheels cast onto the ground.</summary>
public sealed partial class PhysicsSystem
{
    /// <summary>The suspension's shortest length; its rest length is this plus half the travel.</summary>
    private const float SuspensionMinLength = 0.05f;

    private static readonly QueryDescription NewWheeledBodies = new QueryDescription().WithAll<WheeledBody, Transform>().WithNone<SimulatedBody>();
    private static readonly QueryDescription DrivenBodies = new QueryDescription().WithAll<WheeledBody, DriveInput, SimulatedBody>();
    private static readonly QueryDescription WheeledBodies = new QueryDescription().WithAll<Transform, SimulatedBody, WheeledBody>();

    private readonly Dictionary<Entity, Wheels> _vehicles = [];

    public bool TryGetWheelPose(Entity entity, int wheel, out Transform pose)
    {
        pose = new Transform();
        if (!_vehicles.TryGetValue(entity, out Wheels? wheels) || wheel < 0 || wheel >= wheels.Constraint.WheelsCount)
        {
            return false;
        }

        // The wheel's own axes: it turns about its X, and its Y points up.
        Vector3 right = Vector3.UnitX;
        Vector3 up = Vector3.UnitY;
        Matrix4x4 local = wheels.Constraint.GetWheelLocalTransform(wheel, in right, in up);
        Matrix4x4.Decompose(local, out _, out Quaternion rotation, out Vector3 position);
        pose = new Transform { Position = position + wheels.Offset, Rotation = rotation };
        return true;
    }

    public bool IsWheelOnGround(Entity entity, int wheel)
    {
        if (!_vehicles.TryGetValue(entity, out Wheels? wheels) || wheel < 0 || wheel >= wheels.Constraint.WheelsCount)
        {
            return false;
        }

        return wheels.Constraint.GetWheel(wheel).HasContact;
    }

    private void AddWheeledBodies()
    {
        CollectPending(NewWheeledBodies);
        foreach (Entity entity in _pending)
        {
            WheeledBody wheeled = World.Get<WheeledBody>(entity);
            Transform transform = World.Get<Transform>(entity);
            Vector3 position = wheeled.Collider.GetBodyPosition(transform.Position, transform.Rotation);
            using Jolt.Shape chassis = CreateShape(entity, wheeled.Collider);
            Vector3 drop = new(0, -wheeled.WeightDrop, 0);
            using Jolt.OffsetCenterOfMassShape shape = new(in drop, chassis);
            using Jolt.BodyCreationSettings settings = new(shape, position, transform.Rotation, Jolt.MotionType.Dynamic, MovingLayer)
            {
                Friction = Friction,
                Restitution = 0,
                LinearDamping = Damping,
                AngularDamping = Damping,
                OverrideMassProperties = Jolt.OverrideMassProperties.CalculateInertia,
                MassPropertiesOverride = new Jolt.MassProperties { Mass = wheeled.Mass },
            };
            Jolt.Body body = _bodies.CreateBody(settings);
            Jolt.BodyID id = body.ID;
            _bodies.AddBody(in id, Jolt.Activation.Activate);
            Jolt.VehicleConstraintSettings vehicle = CreateVehicleSettings(wheeled);
            Jolt.VehicleConstraint constraint = new(body, vehicle);
            Jolt.VehicleCollisionTesterCastCylinder tester = new(MovingLayer, 0.05f);
            constraint.SetVehicleCollisionTester(tester);
            _system.AddConstraint(constraint);
            _system.AddStepListener(constraint);
            _vehicles.Add(entity, new Wheels(constraint, tester, wheeled.Collider.Offset));

            SimulatedBody simulated = new() { Id = id, PlacedPosition = transform.Position, PlacedRotation = transform.Rotation };
            World.Add(entity, in simulated);
            _ids.Add(entity, (id, false));
            _entities.Add(id, entity);
            _added++;
        }
    }

    private static Jolt.VehicleConstraintSettings CreateVehicleSettings(in WheeledBody wheeled)
    {
        float restLength = SuspensionMinLength + (wheeled.SuspensionTravel / 2);
        Jolt.WheelSettings[] wheels = new Jolt.WheelSettings[wheeled.Wheels.Length];
        for (int index = 0; index < wheels.Length; index++)
        {
            BodyWheel wheel = wheeled.Wheels[index];
            Vector3 top = wheel.Position - wheeled.Collider.Offset + new Vector3(0, restLength, 0);
            float handBrake = wheel.HandBrake ? wheeled.BrakeTorque * 2 : 0;
            wheels[index] = new Jolt.WheelSettingsWV
            {
                Position = top,
                SuspensionDirection = -Vector3.UnitY,
                SteeringAxis = Vector3.UnitY,
                WheelUp = Vector3.UnitY,
                WheelForward = Vector3.UnitZ,
                SuspensionMinLength = SuspensionMinLength,
                SuspensionMaxLength = SuspensionMinLength + wheeled.SuspensionTravel,
                SuspensionSpring = new Jolt.SpringSettings(Jolt.SpringMode.FrequencyAndDamping, wheeled.SuspensionFrequency, wheeled.SuspensionDamping),
                Radius = wheel.Radius,
                Width = wheel.Width,
                MaxSteerAngle = float.DegreesToRadians(wheel.MaxSteer),
                MaxBrakeTorque = wheeled.BrakeTorque,
                MaxHandBrakeTorque = handBrake,
            };
        }

        Jolt.WheeledVehicleControllerSettings controller = wheeled.Leans ? new Jolt.MotorcycleControllerSettings() : new Jolt.WheeledVehicleControllerSettings();
        Jolt.VehicleEngineSettings engine = controller.Engine;
        engine.MaxTorque = wheeled.EngineTorque;
        controller.Engine = engine;
        AddDifferentials(controller, wheeled.Wheels);
        return new Jolt.VehicleConstraintSettings
        {
            Up = Vector3.UnitY,
            Forward = Vector3.UnitZ,
            Wheels = wheels,
            Controller = controller,
        };
    }

    /// <summary>
    /// The engine's torque is shared evenly among differentials: one for each pair of driven wheels that follow each other (an axle),
    /// and one for a driven wheel alone, as a motorcycle's rear.
    /// </summary>
    private static void AddDifferentials(Jolt.WheeledVehicleControllerSettings controller, BodyWheel[] wheels)
    {
        List<(int Left, int Right)> pairs = [];
        for (int index = 0; index < wheels.Length; index++)
        {
            if (!wheels[index].Driven)
            {
                continue;
            }

            bool paired = index + 1 < wheels.Length && wheels[index + 1].Driven && wheels[index + 1].Position.Z == wheels[index].Position.Z;
            if (paired)
            {
                pairs.Add((index, index + 1));
                index++;
            }
            else
            {
                pairs.Add((index, -1));
            }
        }

        for (int index = 0; index < pairs.Count; index++)
        {
            controller.AddDifferential(pairs[index].Left, pairs[index].Right);
            Jolt.VehicleDifferentialSettings differential = controller.GetDifferential(index);
            differential.EngineTorqueRatio = 1f / pairs.Count;
            controller.SetDifferential(index, differential);
        }
    }

    /// <summary>Hands each driver's input to its vehicle, and wakes a resting vehicle that is asked to move.</summary>
    private void DriveWheeledBodies()
    {
        World.Query(in DrivenBodies, (Entity entity, ref DriveInput input, ref SimulatedBody simulated) =>
        {
            if (!_vehicles.TryGetValue(entity, out Wheels? wheels))
            {
                return;
            }

            Jolt.WheeledVehicleController controller = wheels.Constraint.GetController<Jolt.WheeledVehicleController>();
            float forward = Math.Clamp(input.Forward, -1, 1);
            float right = Math.Clamp(input.Right, -1, 1);
            float brake = Math.Clamp(input.Brake, 0, 1);
            float handBrake = Math.Clamp(input.HandBrake, 0, 1);
            controller.SetDriverInput(forward, right, brake, handBrake);
            if (forward != 0 || right != 0)
            {
                _bodies.ActivateBody(simulated.Id);
            }
        });
    }

    private void CopyWheeledPosesToTransforms()
    {
        Jolt.BodyInterface bodies = _bodies;
        World.Query(in WheeledBodies, (ref Transform transform, ref SimulatedBody simulated, ref WheeledBody wheeled) =>
        {
            if (!bodies.IsActive(simulated.Id))
            {
                return;
            }

            Vector3 position = bodies.GetPosition(simulated.Id);
            Quaternion rotation = bodies.GetRotation(simulated.Id);
            transform.Position = wheeled.Collider.GetEntityPosition(position, rotation);
            transform.Rotation = rotation;
        });
    }

    /// <summary>Takes the entity's wheels out of the simulation before its body goes; does nothing for other bodies.</summary>
    private void RemoveWheels(Entity entity)
    {
        if (_vehicles.Remove(entity, out Wheels? wheels))
        {
            _system.RemoveStepListener(wheels.Constraint);
            _system.RemoveConstraint(wheels.Constraint);
            wheels.Constraint.Dispose();
            wheels.Tester.Dispose();
        }
    }

    private void DisposeWheeledBodies()
    {
        foreach (Entity entity in _vehicles.Keys.ToArray())
        {
            RemoveWheels(entity);
        }
    }

    /// <summary>A wheeled body's constraint, what casts its wheels, and its collider's offset from the entity.</summary>
    private sealed record Wheels(Jolt.VehicleConstraint Constraint, Jolt.VehicleCollisionTester Tester, Vector3 Offset);
}
