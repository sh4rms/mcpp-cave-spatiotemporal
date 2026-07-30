Minimal sample for the CAVE, including LSL. Modified to include 3 talking ARKit faces, closely mimicking the MCPP task design.

> [!CAUTION]
> We are likely not allowed to redistribute the MiddleVR and Igloo packages to the public! Please keep in mind when sharing code.

The main branch has the core demo scene. There is a "ground" plane and a blue cube. The cube is displaced from the origin in X and Z by 1 unit (meter), and is one unit off the ground. It rotates at a constant rate, and streams the orientation data over LSL.

In terms of modifiable templates, there are two branches that are set up for the different CAVE environments:

 - [mvr](https://git.yale.edu/BrainWorks/cave-sample/tree/mvr) contains the MiddleVR implementation
 - [ce](https://git.yale.edu/BrainWorks/cave-sample/tree/ce) contains the Igloo Core Engine implementation

To download a specific sample, click one of the links above and then click the "<> Code" button, and "Download ZIP".

The extra dependencies for each of those branches are included in the "LocalPackages" directory.

Notes:
 - As of today (19 Feb 2026), the MiddleVR 3.2.0 beta does not seem fully compatible with Unity 6.3, despite targeting that version. So we're defaulting to Unity 6.0.
 - We require at least the MiddleVR 3.2.0 beta because some cluster synchronization things were solved since 3.1.
# mcpp-cave-spatiotemporal
