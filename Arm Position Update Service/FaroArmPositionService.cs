using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

using Debug = System.Diagnostics.Debug;

using FaroArm.Interfaces;   // from FaroArm USB driver
using NetMQ;  // C# zmq bindings

/// <summary>
/// FaroArmPositionService: A standalone C# .NET console app that creates a ZeroMQ publisher socket,
/// and continually ingests arm position/angle updates from a Faro Arm (directly from the API
/// exposed by the Faro Windows driver DLLs) and publishes them to all connected subscribers.
/// 
/// Intended to be used alongside a simple Unity game engine scene which visualizes the arm movement
/// in realtime by adjusting the joint angles of a 3D model to match those measured by the physical
/// Faro arm.
/// 
/// The FaroArm API is not publicly documented, but as there is official third-party support
/// for applications such as Polyworks, clearly the driver is intended to provide at least the key
/// bits of arm data downstream.
/// 
/// Note: the .NET assemblies and DLLs provided with the Faro USB Arm driver target a recent version
/// of the .NET framework (4.6.1) which I do not expect to work directly within Unity, thus this
/// application serves as a proxy to ferry the arm updates via ZMQ to any network-accessible application
/// running on any OS and tech stack.
/// 
/// Author: Blake Harris (bmh@blakemharris.com)
/// July 2020
/// 
/// Requirements
///   Faro Arm -- Tested with an 7-axis FaroArm Quantum (8 foot, Gen 3), but expected to work with:
///         FaroArm Prime, Advantage, Platinum, Titanium, Fusion, Gage (Gen 3) and likely with the
///         Generation 4 and Generation 5 hard probes as well. Laser line probing is not supported.
/// 
///   Windows 10 (Faro driver is only available on Windows)
///   
///   NetMQ - C# implementation of zeromq: Install via Tools->NuGet in Visual Studio, and manually include AsyncIO.dll
///          and NetMQ.dll in your Unity project manually (create an Assets\Plugins dir).
///
///   System.Text.Json, Microsoft's JSON serialization library. Install via NuGet as with NetMQ.
///
///   Faro Arm Driver (a recent version as of mid-2020)
///   
///   Faro Arm Manager, UI software installed with the driver. Start first and keep running once arm is connected.
///   
///   Faro Arm Position Service (FaroArmPositionService.cs), a .NET Console app which connects to the arm via
///       .NET DLLs from the official driver and very frequently publishes a JSON object via zeromq including
///       arm movement, button presses, encoder joint angles, etc.
///
///   Faro Arm Driver DLL files
///       NOTE: I had a lot of difficulty getting the Visual Studio project to properly reference the FARO Driver DLLs
///             in their originally installed location (C:\Program Files\Common Files\FARO Shared\), but I was able to
///             get things working by copying all the DLL files from that location to the project's \bin\x64\Debug
///             directory. Someone more familiar should be able to resolve this the correct way, but for now,
///             I believe THIS IS A MANDATORY STEP or the app will not be able to load some of the native dependencies
///             referenced inside the .NET referenced DLLs at runtime (or something like that). Those Faro DLLs will
///             not be distributed as part of this project and are installed with the manufacturer's Windows driver
///             from faro.com.
///             
///   To visualize this data in Unity (it's amazing! do it!), you'll need:
///     Unity 2019.4.4f1 (or similar, no special version requirements as long as NetMQ works)
///     ArmJointController.cs and the "Faro Arm Quantum" Unity scene and project assets.
/// </summary>

namespace FaroArmPositionService
{
    class ArmReader
    {
        public double lastX = 0, lastY = 0, lastZ = 0;
        public int msgCount = 0;

        NetMQ.Sockets.PublisherSocket publisherSocket;

        public ArmReader()
        {
            this.OpenZMQConnections();
            this.ConnectToFaroArm();
        }

        void OpenZMQConnections()
        {
            string connectionString = "tcp://*:5457";

            this.publisherSocket = new NetMQ.Sockets.PublisherSocket();
            this.publisherSocket.Bind(connectionString);
            // remember that ZMQ Publishers must start *before* any Subscribers connect

            Debug.WriteLine($"ZMQ Publisher Started on {connectionString}.");
            
        }

        void ConnectToFaroArm()
        {
            // This init sequence is a complete guess based on
            // poking around at the public API exposed in the Faro driver DLLs,
            // particularly FaroArm.Net and FaroArm.Interfaces.
            // Ensure those two DLLs are selected in:
            // Project -> Add Reference -> Browse...
            // A key item was identifying the FaroArmUpdate event delegate,
            // which luckily worked right out of the box, and provides an
            // IFaroArmUpdate-compatible object to your handler, so you can
            // just pick out which attributes you care to pass on (or serialize
            // and publish the entire object as shown here).
            //
            // Note: the FaroArm Manager app  must already running and be
            // connected to the arm (USB recommended for latency reasons)
            // ... and of course overall system accuracy is extremely
            // dependent on probe compensation procedure and mounting.
            // Fully review Faro Arm manual for important details.
            // This is just a demo intended to help you decide next steps or
            // understand overall system capabilities before planning an
            // integration project.

            FaroArm.FaroArmInterfaceFactory faif;
            FaroArm.Interfaces.IFaroArmManager ifam;
            FaroArm.Interfaces.IFaroArm arm;

            // create an instance of some of the classes exposed in the driver DLLs
            faif = new FaroArm.FaroArmInterfaceFactory();
            ifam = faif.CreateIFaroArmManager();

            // this seems to be what "connects" to the Faro Arm Manager application
            ifam.Connect();

            arm = ifam.GetFirstDetectedFaroArm();
            Debug.WriteLine("FaroArmPositionService :: Connected to " + arm.SerialNumber);

            // add our event handler to the arm object's FaroArmUpdate delegate
            arm.FaroArmUpdate += HandleFaroArmUpdate;

            arm.EnableFaroArmUpdates(true); // not sure if this is mandatory
        }

        void PrintUpdate(FaroArm.Interfaces.IFaroArmUpdate u)
        {
            // remember: all positional units are in millimeters, divide by 25.4 to get inches
            double dX = this.lastX - u.X; double dY = this.lastY - u.Y; double dZ = this.lastZ - u.Z;

            Debug.WriteLine($"===== Arm Event #{this.msgCount} ======");
            Debug.WriteLine($"   X: {u.X:F3}mm, Y: {u.Y:F3}mm, Z: {u.Z:F3}mm    A: {u.A:F3}, B: {u.B:F3}, C: {u.C:F3} ");
            Debug.WriteLine($"   Delta: {dX:F3}, {dY:F3}, {dZ:F3}");
            Debug.WriteLine($"   Angles: 1:{u.Angle1:F3}, 2:{u.Angle2:F3}, 3:{u.Angle3:F3}, 4:{u.Angle4:F3}, 5:{u.Angle5:F3}, 6:{u.Angle6:F3}, 7:{u.Angle7:F3}");
            Debug.WriteLine($"   Buttons: {u.Buttons}, At Rest: {u.ArmAtRest}, Enc Ref'd: {u.EncodersReferenced}, End Stops: {u.EndStops}");
        }

        void SendZMQUpdate(FaroArm.Interfaces.IFaroArmUpdate u)
        {
            // serialize this IFaroArmUpdate object to JSON
            string msg = JsonSerializer.Serialize(u);
            // and send it out (topic first as plain string, then JSON payload)
            this.publisherSocket.SendMoreFrame("ArmUpdate").SendFrame(msg);
        }

        void HandleFaroArmUpdate(object sender, FaroArm.Interfaces.FaroArmUpdateEventArgs e)
        {
            FaroArm.Interfaces.IFaroArmUpdate u = e.FaroArmUpdate;

            // NOTE: intentionally overriding the Faro timestamp with the msg #
            // TODO: create a class which implements IFaroArmUpdate and provides both
            //       faro timestamp and msg number.
            this.msgCount += 1;
            u.TimeStamp = this.msgCount;

            this.SendZMQUpdate(u);

            // if user has pressed the arm trigger button, print details for that update
            // (instead of printing every message and overwhelming the logs)
            if (u.Buttons > 0)
                this.PrintUpdate(u);

            this.lastX = u.X; this.lastY = u.Y; this.lastZ = u.Z;
        }
    }

    class Program
    {
        static void Main()
        {
            new ArmReader();

            // surely there are better ways to keep the process alive while the "service" is running
            // but this is an easy solution
            Console.WriteLine("FaroArmPositionService :: Service running. Press any key to exit.");
            Console.ReadLine();
        }
    }
}
