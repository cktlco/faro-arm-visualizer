using AsyncIO;
using NetMQ;
using NetMQ.Sockets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ArmJointController: Unity component which subscribes to a zeromq publisher and receives positional updates from a
/// Faro Quantum articulated Coordinate Measuring Machine arm.
/// 
/// The JSON update messages are deserialized, and the "encoder angle" attributes are used to set the angle of each of
/// the 3d meshes that represent the on-screen version of the Faro Quantum arm.
/// 
/// Zeromq is used as a very easy and low-latency method for publishing and consuming thousands of position updates
/// per second.
///
/// Author: Blake Harris (bmh@blakemharris.com)
/// July 2020
/// 
/// Requirements:
///   Windows 10
///   NetMQ - C# implementation of zeromq: Install via Tools->NuGet in Visual Studio, and manually include AsyncIO.dll
///          and NetMQ.dll in your Unity project manually (create an Assets\Plugins dir).
///   Faro Arm Driver (a recent version as of mid-2020)
///   Faro Arm Manager, UI software installed with the driver. Start first and keep running once arm is connected.
///   Faro Arm Position Service (FaroArmPositionService.cs), a .NET Console app which connects to the arm via
///       .NET DLLs from the official driver and very frequently publishes a JSON object via zeromq including
///       arm movement, button presses, encoder joint angles, etc.
///   Unity 2019.4.4f1 (no special version requirements as long as NetMQ works)
/// </summary>

[System.Serializable]
public class ArmUpdate
{
    public float X, Y, Z, A, B, C;
    public float Angle1, Angle2, Angle3, Angle4, Angle5, Angle6, Angle7;
    public int Buttons, EndStops, ArmAtRest, EncodersReferenced, TimeStamp;
}

public class ArmJointController : MonoBehaviour
{
    // to use: add this to the root object of a GameObject hierarchy "model" which represents a 7-axis robot arm
    public float armCheckFrequency = 0.01f; // seconds between checking the ZMQ queue for another arm update (and thus another point to create)
    public string armServerAddr = "tcp://localhost:5457";
    public bool debugMode = false;

    public Transform axis1Xform, axis2Xform, axis3Xform, axis4Xform, axis5Xform, axis6Xform, axis7Xform, probeTipXform;
    public float axis1RotAdj, axis2RotAdj, axis3RotAdj, axis4RotAdj, axis5RotAdj, axis6RotAdj, axis7RotAdj, probeTipAdj;
    public Transform driverReportedProbePoint;
    public Vector3 driverReportedProbePointAdj;
    public GameObject laserBeam;    // hoho

    private GameObject armRoot;
    private SubscriberSocket subSocket;
    private string msgString;
    
    void Start()
    {   // Start is called before the first frame update
        AsyncIO.ForceDotNet.Force();

        this.subSocket = new SubscriberSocket();
        this.subSocket.SubscribeToAnyTopic();
        this.subSocket.Connect(this.armServerAddr);
        Debug.Log("ArmJointController :: Connected to ZMQ Publisher at " + this.armServerAddr);

        Invoke("CheckForArmUpdate", 0.1f);
    }

    void CheckForArmUpdate()
    {
        while (this.subSocket.TryReceiveFrameString(out this.msgString))
        {   // messages will come in pairs, with the first being the topic "ArmUpdate" and second being the arm update payload
            if (this.msgString == "ArmUpdate")
            {
                string msg = this.subSocket.ReceiveFrameString();
                ArmUpdate u = JsonUtility.FromJson<ArmUpdate>(msg);
                ProcessArmUpdate(u);
            }
        }

        // now schedule the next check (otherwise we would stop checking)
        Invoke("CheckForArmUpdate", this.armCheckFrequency);
    }

    void ProcessArmUpdate(ArmUpdate u)
    {
        if (this.debugMode || u.Buttons == 2) // green button only
            Debug.Log($"ArmJointController :: Arm update #{u.TimeStamp} received");

        this.axis1Xform.localEulerAngles = new Vector3(0, u.Angle1 + this.axis1RotAdj, 0);
        this.axis2Xform.localEulerAngles = new Vector3(u.Angle2 + this.axis2RotAdj, 0, 0);
        this.axis3Xform.localEulerAngles = new Vector3(0, u.Angle3 + this.axis3RotAdj, 0);
        this.axis4Xform.localEulerAngles = new Vector3(0, 0, u.Angle4 + this.axis4RotAdj);
        this.axis5Xform.localEulerAngles = new Vector3(0, u.Angle5 + this.axis5RotAdj, 0);
        this.axis6Xform.localEulerAngles = new Vector3(0, 0, u.Angle6 + this.axis6RotAdj);
        this.axis7Xform.localEulerAngles = new Vector3(0, u.Angle7 + this.axis7RotAdj, 0);
        this.driverReportedProbePoint.position = new Vector3(u.X, u.Y, u.Z) + this.driverReportedProbePointAdj;

        if (this.laserBeam && u.Buttons == 1) // red button only
            EmitDevastation();
    }

    void EmitDevastation()
    {
        this.laserBeam.GetComponent<ParticleSystem>().Emit(1);
    }

    private void OnApplicationQuit()
    {
        NetMQConfig.Cleanup(false);
        Debug.Log("ArmJointController exited.");
    }
}
