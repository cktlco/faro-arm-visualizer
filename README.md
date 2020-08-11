# faro-arm-visualizer
**A suite of unofficial, open-source utilities enabling basic software integration with the FaroArm series of high-accuracy articulated measuring instruments, including a realtime 3D position tracking demonstration in Unity.**

**The FaroArm&reg; is Faro Technologies's product line of portable Coordinate Measuring Machines (CMM). The FaroArm is a high-accuracy 3D digital measuring instrument, engineered for two primary use cases:**

   1) CAD-based Inspection. For metal or plastic items that are designed with a computer then physically manufactured, using companion software, points on the virtual model can be compared with points physically measured with the arm to identify if the manufacturing process is able to match the required accuracy (ie, does the part you made match your original drawings, and if not, which dimensions are out of spec).

   2) 3D Laser Scanning. While 6-axis CMM arms are the core model, 7-axis arms provide the necessary range of motion to mount a lightweight 3D laser scanner. This is a device which, with significant software support, enables the user to "spray paint" a laser stripe over a physical part and capture a 3d point cloud on the computer. Each virtual point represents an individual laser triangulation or time-of-flight calculation of the physical distance between the target surface and the laser scanner head, which is mounted rigidly to the end of the coordinate system of the 7-axis arm -- thus, since you always know the position of the end of the arm, and since the laser scanner is mounted rigidly to the end of the arm, you know the position in arm-space of all the points captured by the scanner. Modern arm-mounted laser systems can capture hundreds of thousands or more points per minute from almost any type of surface, including optically reflective and dark, but all share a heavy and expensive software-based workflow which is subject to frequent change and reinterpretation by software vendors..

**... but of course, we may have other clever uses for an extremely high-accuracy bridge between physical and digital 3D coordinate systems**. Thus, I hope the software and documentation included here serves as a helpful starting point.

Additionally, there are many valuable resources available in the knowledge base at [faro.com](http://faro.com) or on the [Faro YouTube channel](https://www.youtube.com/user/FAROTechnologies), including many minor technical details that would be otherwise impossible to confirm without physical access to a FaroArm.


## Repo Structure

- `Arm Position Update Service\`    --  Service which publishes Arm Position events over zeromq, C# Visual Studio 2019 solution
- `Arm Position Update Service\FaroArmPositionService.cs`    --  all relevant code and documentation is in this file
- `Arm Position Update Service\bin\x64\Debug\Faro*.dll`    -- not included in repo, see detailed comments in FaroArmPositionService.cs 

- `Unity Visualizer\`   -- Unity project, Open in Unity 2019.4.4f1 or similar
- `Unity Visualizer\Assets\Scenes\Faro Arm Quantum.unity`    -- the main scene
- `Unity Visualizer\Assets\Scenes\Models\faro arm\`    -- 3d model of a FaroArm[^1], multiple 3d solid model and mesh formats
- `Unity Visualizer\Assets\Plugins\NetMQ.dll, AsyncIO.dll`    -- zeromq library. if needed, replace these with a more recent version (these were pulled from NuGet in July 2020)
- `Unity Visualizer\Assets\Scripts\ArmJointController.cs`    -- primary script, listens for zmq messages with arm position and updates arm 3d model. undocumented support for virtual laser weaponry.
- `Unity Visualizer\Assets\Scripts\PointCloudMaker.cs`    -- stub example script integrating arm position data with a Unity ParticleSystem

[^1]: External reference: [3D Model of a representative FARO Arm](https://grabcad.com/library/faro-arm) from GrabCAD user Joerg Schmit, circa Feb 2012. It doesn't match any FARO model exactly, but is very close, and extremely convenient so that I did not have to create a similar model myself. This mesh's arm tube lengths will need additional compensation/calibration procedures to sync with the real-world arm better than a few millimeters, but the model has been very valuable for visualization. Although it should work ok in theory, I do not expect this model to be accurate enough to rely on the result of any forward kinematics position calculations based on the model joints, as those will not exactly match the measured position calculation values (XYZ,ABC) returned by the arm or driver itself, which will represent the probe tip position more accurately (by multiple orders of magnitude) when mounted rigidly with a properly compensated probe.

## Requirements

- Faro Arm -- Tested with an 7-axis FaroArm Quantum (8 foot, Gen 3), but expected to work with:
      FaroArm Prime, Advantage, Platinum, Titanium, Fusion, Gage (Gen 3) and likely with the
      Generation 4 and Generation 5 hard probes as well. Laser line probing is not supported.

- Windows 10 (Faro driver is only available on Windows)

- NetMQ - C# implementation of zeromq: Install via Tools->NuGet in Visual Studio, and manually include AsyncIO.dll
       and NetMQ.dll in your Unity project manually (create an Assets\Plugins dir).
       
- Faro Arm Driver (a recent version as of mid-2020)

- Faro Arm Manager, UI software installed with the driver. Start first and keep running once arm is connected.

- Faro Arm Position Service (FaroArmPositionService.cs), a .NET Console app which connects to the arm via
    .NET DLLs from the official driver and very frequently publishes a JSON object via zeromq including
    arm movement, button presses, encoder joint angles, etc.
    
    NOTE: I had a lot of difficulty getting the Visual Studio project to properly reference the FARO Driver DLLs
          in their originally installed location (C:\Program Files\Common Files\FARO Shared\), but I was able to
          get things working by copying all the DLL files from that location to the project's \bin\x64\Debug
          directory. Someone more familiar should be able to resolve this the correct way, but for now,
          I believe THIS IS A MANDATORY STEP or the app will not be able to load some of the native dependencies
          referenced inside the .NET referenced DLLs at runtime (or something like that). Those Faro DLLs will
          not be distributed as part of this project and are installed with the manufacturer's Windows driver
          from faro.com.
          
- To visualize this data in Unity (it's amazing! do it!), you'll need:
  Unity 2019.4.4f1 (or similar, no special version requirements as long as NetMQ works)
  ArmJointController.cs and the "Faro Arm Quantum" Unity scene and project assets.

## Installation Steps

1. Mount, turn on, and otherwise prepare the FaroArm for normal use, including installing drivers from faro.com
2. Start the FaroArm Manager application (installed with the driver package), and connect to your arm. Verify that you are able to see arm position updates in the Diagnostics section of the application before proceeding.

2. Using git, clone this repo into a local directory

3. Open the Visual Studio solution in "Arm Position Update Service".
   Change the "Solution Platforms" dropdown from "Any CPU" to "x64", so we can match the native code Faro libraries.
4. In Visual Studio, Tools->NuGet Package Manager->Manage... and add (in no particular order):
    a. System.Text.Json
    b. NetMQ
    c. AsyncIO (probably already installed as part of NetMQ, but verify)
5. Copy the Faro Driver DLLs from their original installation directory directly into the C# project build directory (this is a workaround for a library reference problem I couldn't solve)
   For example, copy `C:\Program Files\Common Files\FARO Shared\*.*` into `Arm Position Update Service\bin\x64\Debug\`
6. Build and start the C# application (ie, click Play button in Visual Studio).
   You may be asked for Windows Firewall permission for the console app since we are binding to a TCP port.
   Verify that no critical errors are reported, and you should see the console log print your arm's serial number once connected.
   Press the arm probe buttons to print out probe tip coordinates.
7. Now attach any custom program you wish to the zeromq socket used by the C# service (on any machine, local or network)

... and if you wish to use the Unity Visualizer component:
8. Install Unity (tested w/2019.*)
9. Copy the "net40" version of the AsyncIO.dll and NetMQ.dll files from the Visual Studio project directory to the Unity project directory
    For example, copy `Arm Position Update Service\packages\AsyncIO.0.1.69\lib\net40\AsyncIO.dll` to `Unity Visualizer\Assets\Plugins\AsyncIO.dll`
                 and `Arm Position Update Service\packages\NetMQ.4.0.0.207\lib\net40\NetMQ.dll` to `Unity Visualizer\Assets\Plugins\NetMQ.dll`
    I believe these need to be .NET 4.0 build targets to match the rest of Unity's framework (as of July 2020). One way or another, just get NetMQ library support working in Unity -- it's a pretty common/documented topic.
10. Open Unity Hub and point to an existing project rooted in this repo's `Unity Visualizer` directory.
11. Unity will take a long time to start (5-10 min) as it imports and verifies all the project assets, packages, metadata, rebuilds caches, etc. This is a one-time operation, but necessary since including those thousands of local, build-specific files in the repo is not desirable.
12. Open the only Scene in the project: `\Assets\Scenes\Faro Arm Quantum.unity`
13. Click on the "Arm" GameObject in the Hierarchy panel and review the "Arm Joint Controller" component. These values, alongside the corresponding logic in `\Assets\Scripts\ArmJointController`, are what you will manipulate to make the on-screen 3D model move in sync with the physical Faro Arm instrument.
14. Click the "Play" button in the Unity Editor. You may wish to switch from the Game window back to the Scene window so you can move your view freely.
15. Positional updates will be applied smoothly in near real-time, providing an approximate representation of the arm's real world position.


## Troubleshooting

Q: Why should anyone care about this?

A: This repo provides a capability that does not exist elsewhere: enabling unofficial support for anyone with a FaroArm to read the arm's probe coordinates into custom-built software. Although the FaroArm USB driver is available freely from faro.com, there are no public examples of how to utilize the driver SDK or arm position data in a custom integration.

As the technology ages, more of these products are finding their way from their original industrial settings into secondary settings... but compatible software will rarely be included due to the expensive nature of per-seat user licensing.

So... since I was able to pull the key data out of the arm hardware in real-time and use it for my own nefarious purposes, and since there is essentially zero documentation on the subject... please experiment with the details herein if you are interested.


Q: Why does Unity freeze if I make changes on the C# side while the Unity Editor is in Play mode?

A: This is a known issue with NetMQ and Unity, similar issues occur without the AsyncIO.ForceDotNet.Force() and NetMQConfig.Cleanup(false) calls... seems like the multiple event loops and secondary threads need a lot to stay in sync without crashes/freezes, but all generally works ok as long as C# script edits are not made while the Editor is in Play mode. Alternately, you may be able to disable the automatic reimport when Unity regains focus, but that's a hassle for other workflow reasons.


Q: How do I resolve the Visual Studio "File Not Found" error for FaroArm.Net.dll during runtime?

A: I had a lot of issues with this until I copied all the Faro DLLs directly into the Visual Studio project's bin dir as described above. For several of the DLLs (the native code ones that are not .NET assemblies, I think), Visual Studio did not want to create the correct project references from the Set References menu item, and so they never got automatically copied into that bin dir by the build process like the other package libraries. Not sure why.


Q: ...and if I don't know how to use Unity?

A: Don't worry, just play around with the core C# publisher service. Once you can connect to your arm and read the positional event data, figure out next steps.


Q: Why does the arm model get all wonky when I hit Play in the Unity Editor? It doesn't match my arm movement at all!

A: Expect that you will have to play with all of *Adj values in the ArmJointController component in the demo scene, perhaps extensively. You may need to rewrite all the code and/or manipulate the "Arm" GameObject hierarchy in order to get things in sync. This may be difficult if you are not familiar with Unity, but in that case, don't worry so much about representing the joint angles and just focus on the X,Y,Z point returned with each arm update message, and try to visualize that in a simpler way in 3D space.


Q: Which CPU architectures does this support?

A: It has only been tested successfully with a Windows 10 x64 build of the C# Arm Position Update Service, but it's possible that with extended tinkering you could get 32-bit support working. All the external references (including the FARO driver itself) will have to use the corresponding 32-bit versions.
