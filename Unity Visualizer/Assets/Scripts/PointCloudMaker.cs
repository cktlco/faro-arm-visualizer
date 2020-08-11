using AsyncIO;
using NetMQ;
using NetMQ.Sockets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PointCloudMaker: Unity component which gets positional data from a Faro Arm (see ArmJointController.cs for
/// a full implementation) and creates a "point cloud" using a particle system. This is a stub example, so key
/// code will need to be added, but wanted to show how to easily manually manipulate a Unity Particle System.
/// Most developers would attempt this by spawning a new GameObject primitive for each new point, but that is not
/// a realistic solution at all, and will quickly causes stability/performance issues for many thousands of points.
/// A ParticleSystem is also an effective way of "shooting" very high numbers of 3d mesh-based projectiles as you can
/// freely inspect position, rotation, collisions (either manually via raycasting or builtin particle system colliders),
/// and you get the myriad other features of the Particle System otherwise for free -- all with efficient low-level
/// rendering of the points themselves, at least compared to other Unity alternatives.
///
/// Author: Blake Harris (bmh@blakemharris.com)
/// July 2020
/// </summary>

public class PointCloudMaker : MonoBehaviour
{
    // a stub example, to use: add this to a ParticleSystem object with "Emission" disabled (so it only emits when you say so)
    public float cubeScale = 10.0f;
    public float pointScale = 1.0f;
    public float pointLifetime = 100000f;
    public float armCheckFrequency = 0.01f; // seconds between checking the ZMQ queue for another arm update (and thus another point to create)
    public string armServerAddr = "tcp://*:5457";
    public GameObject pointRoot;

    private ParticleSystem ps;
    private SubscriberSocket subSocket;

    void Start()
    {
        // ... real code would go here
    }

    void EmitParticle(float x, float y, float z)
    {
        var emitParams = new ParticleSystem.EmitParams();
        emitParams.startColor = Color.red;
        emitParams.startSize = 0.2f;
        emitParams.velocity = Vector3.zero;
        emitParams.position = new Vector3(x, y, z);
        emitParams.startLifetime = this.pointLifetime;

        this.ps.Emit(emitParams, 1);

        // alternatively, to create a first-class GameObject instead
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(x, y, z);
        cube.transform.localScale = Vector3.one * this.cubeScale;
        cube.transform.parent = this.pointRoot.transform;
        Debug.Log("PointCloudMaker :: Point created at " + x + ", " + y + ", " + z);
    }
}
