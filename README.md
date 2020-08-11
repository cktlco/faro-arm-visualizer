# faro-arm-visualizer
**A suite of unofficial, open-source utilities enabling basic software integration with the FaroArm series of high-accuracy articulated measuring instruments, including a realtime 3D position tracking demonstration in Unity.**

**The FaroArm&reg; is Faro Technologies's product line of portable Coordinate Measuring Machines (CMM). The FaroArm is a high-accuracy 3D digital measuring instrument, engineered for two primary use cases:**

   1) CAD-based Inspection. For metal or plastic items that are designed with a computer then physically manufactured, using companion software, points on the virtual model can be compared with points physically measured with the arm to identify if the manufacturing process is able to match the required accuracy (ie, does the part you made match your original drawings, and if not, which dimensions are out of spec).

   2) 3D Laser Scanning. While 6-axis CMM arms are the core model, 7-axis arms provide the necessary range of motion to mount a lightweight 3D laser scanner. This is a device which, with significant software support, enables the user to "spray paint" a laser stripe over a physical part and capture a 3d point cloud on the computer. Each virtual point represents an individual laser triangulation or time-of-flight calculation of the physical distance between the target surface and the laser scanner head, which is mounted rigidly to the end of the coordinate system of the 7-axis arm -- thus, since you always know the position of the end of the arm, and since the laser scanner is mounted rigidly to the end of the arm, you know the position in arm-space of all the points captured by the scanner. Modern arm-mounted laser systems can capture hundreds of thousands or more points per minute from almost any type of surface, including optically reflective and dark, but all share a heavy and expensive software-based workflow which is subject to frequent change and reinterpretation by software vendors..

**... but of course, we may have other clever uses for an extremely high-accuracy bridge between physical and digital 3D coordinate systems**. Thus, I hope the software and documentation included here serves as a helpful starting point.

Additionally, there are many valuable resources available in the knowledge base at [faro.com](http://faro.com) or on the [Faro YouTube channel](https://www.youtube.com/user/FAROTechnologies), including many minor technical details that would be otherwise impossible to confirm without physical access to a FaroArm.


## Repo Structure

- Arm Position Update Service\    --  Service which publishes Arm Position events over zeromq, C# Visual Studio 2019 solution
- Arm Position Update Service\FaroArmPositionService.cs    --  all relevant code and documentation is in this file
- Arm Position Update Service\bin\x64\Debug\Faro*.dll    -- not included in repo, see detailed comments in FaroArmPositionService.cs 

- Unity Visualizer\   -- Unity project, Open in Unity 2019.4.4f1 or similar
- Unity Visualizer\Assets\Scenes\Faro Arm Quantum.unity    -- the main scene
- Unity Visualizer\Assets\Scenes\Models\faro arm\    -- 3d model of a FaroArm[^1], multiple 3d solid model and mesh formats
- Unity Visualizer\Assets\Plugins\NetMQ.dll, AsyncIO.dll    -- zeromq library. if needed, replace these with a more recent version (these were pulled from NuGet in July 2020)
- Unity Visualizer\Assets\Scripts\ArmJointController.cs    -- primary script, listens for zmq messages with arm position and updates arm 3d model. undocumented support for virtual laser weaponry.
- Unity Visualizer\Assets\Scripts\PointCloudMaker.cs    -- stub example script integrating arm position data with a Unity ParticleSystem

[^1]: External reference: [3D Model of a representative FARO Arm](https://grabcad.com/library/faro-arm) from GrabCAD user Joerg Schmit, circa Feb 2012. It doesn't match any FARO model exactly, but is very close, and extremely convenient so that I did not have to create a similar model myself. This mesh's arm tube lengths will need additional compensation/calibration procedures to sync with the real-world arm better than a few millimeters, but the model has been very valuable for visualization. Although it should work ok in theory, I do not expect this model to be accurate enough to rely on the result of any forward kinematics position calculations based on the model joints, as those will not exactly match the measured position calculation values (XYZ,ABC) returned by the arm or driver itself, which will represent the probe tip position more accurately (by multiple orders of magnitude) when mounted rigidly with a properly compensated probe.


## Troubleshooting
Q: Why should anyone care about this?
A: This repo provides a capability that does not exist elsewhere: enabling unofficial support for anyone with a FaroArm to read the arm's probe coordinates into custom-built software. Although the FaroArm USB driver is available freely from faro.com, there are no publicly available examples of how to utilize the driver or arm position data in a custom integration.

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

