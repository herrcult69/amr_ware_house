# Autonomous Pallet Stacker AMR — Unity Warehouse Simulation

> **Unity Version**: Unity 2022.3 LTS (Tested on 2022.3.62f3)  
> **Physics Engine**: Unity PhysX (ArticulationBody with 0.01s Fixed Timestep)  
> **ROS Connectivity**: Unity Robotics Hub `com.unity.robotics.ros-tcp-connector` (Port 10000 loopback)  
> **Course**: 61CSE326 – Robot Obstacle-Avoidance Planning Software (WS2026)  
> **Institution**: Vietnamese-German University (VGU) — Computer Science & Engineering  
> **Companion ROS Repository**: `CollaborativeAMRStockerization` (ROS 1 Noetic in Docker)

---

## 1. Project Overview

This Unity project simulates a high-fidelity digital twin of an indoor warehouse environment for an autonomous pallet stacker mobile robot (**XStack AMR**). It communicates bidirectionally with a containerized ROS 1 Noetic brain over TCP loopback:

* **Inbound from ROS**: `/cmd_vel` (`geometry_msgs/Twist`) drives the differential-drive wheels; `/lift_cmd` (`std_msgs/Float32`) actuates the vertical mast prismatic joint.
* **Outbound to ROS**: `/odom` (`nav_msgs/Odometry`) streams ground-truth planar position and orientation; `/scan` (`sensor_msgs/LaserScan`) streams 2D LiDAR raycast distance arrays; `/clock` (`rosgraph_msgs/Clock`) synchronizes simulation time.

---

## 2. Quickstart: Opening & Running the Simulation

### Prerequisites
1. **Unity Hub** installed.
2. **Unity 2022.3 LTS** installed (e.g. `2022.3.62f3` or compatible `2022.3.x`).
3. Companion ROS Docker container running (`ros1_amr_core`).

### Step-by-Step Launch
1. Open **Unity Hub** $\rightarrow$ Click **Add** $\rightarrow$ **Add project from disk**.
2. Select the `amr_ware_house` folder. Ensure the Editor Version shows `2022.3.x`.
3. Open the project. *(First-time load will take 1–2 minutes to compile packages).*
4. In the **Project** panel, open the primary warehouse scene:
   ```
   Assets/Scenes/Milestone_1.unity
   ```
   *(Or `Milestone_2.unity` for expanded multi-aisle obstacle navigation).*
5. Press the **Play (`▶`)** button at the top center of the editor.
6. Check the Unity Console or Game window: You will see the connection handshake established with the ROS bridge (`127.0.0.1:10000`).

---

## 3. Scenes Catalog

| Scene Path | Primary Robot | Description |
| :--- | :--- | :--- |
| `Assets/Scenes/Milestone_1.unity` | `xstack_amr` | **Primary Milestone 1 Scene**: Single warehouse corridor with pallet rack bay at $(4.0, 5.0)$ and $25\text{ cm}$ staging station at $(13.12, 0.0)$. Used for Milestone 1 replenishment and mapping. |
| `Assets/Scenes/Milestone_2.unity` | `xstack_amr` | **Primary Milestone 2 Scene**: Multi-bay warehouse layout featuring static and dynamic obstacle configurations for `move_base` DWA obstacle avoidance. |
| `Assets/Scenes/Milestone_3.unity` | `xstack_amr` | Extended multi-agent replenishment evaluation scene. |
| `Assets/Scenes/WarehouseTraining.unity` | `reverse_stacker_amr` | Early training scene using the legacy robot model (`robot_profile:=legacy`). |

---

## 4. Robot Prefabs & Kinematics

### 4.1 XStack AMR (`Assets/xstack_amr.prefab`) — Default
The modern differential-drive pallet stacker robot:
* **Chassis Articulation**: ArticulationBody hierarchy using 32 solver iterations and 8 solver velocity iterations for contact stability.
* **Axle Reference (`drive_centre`)**: Ground projection of drive axle midpoint ($X = -0.10\text{ m}$ relative to `base_link`). Tracked by `PlanarOdometryPublisher.cs` to eliminate in-place yaw alignment drift.
* **Track Width**: $0.40\text{ m}$ wheel-to-wheel spacing ($r_{\text{wheel}} = 0.075\text{ m}$).
* **Vertical Mast Prismatic Joint**: Range $0.00\text{ m} \to 0.75\text{ m}$ reach.
* **Collision Envelope**: $X \in [-0.54\text{ m}, +0.37\text{ m}]$, $Y \in [-0.23\text{ m}, +0.23\text{ m}]$.
* **Attached Components**:
  * `StackerController.cs`: Subscribes to `/cmd_vel` and `/lift_cmd`, drives joint torques, and supports WASD/RF keyboard overrides.
  * `PlanarOdometryPublisher.cs`: Tracks `drive_centre`, publishes `/odom` (child frame: `drive_center`), `/tf` (`odom -> drive_center`), and `/clock`.
  * `PlanarLaserScanPublisher.cs`: Attached to `laser_link`, casts 181 rays across $180^\circ$ at 10 Hz ($0.05\text{ m} \to 10.0\text{ m}$).

### 4.2 Legacy Stacker (`Assets/reverse_stacker_amr.prefab`)
* The original robot prototype. Retained for regression tests using `robot_profile:=legacy`.
* Tracks `base_link` and publishes odometry with child frame `base_footprint`.

---

## 5. Keyboard Manual Driving Controls (Editor Play Mode)

When the scene is running in Play mode, click inside the **Game** view to focus your keyboard:

| Key | Action | Function |
| :---: | :---: | :--- |
| **`W`** | Drive Forward | Commands linear $+X$ motion ($0.8\text{ m/s}$) |
| **`S`** | Drive Reverse | Commands linear $-X$ reverse motion ($-0.8\text{ m/s}$) |
| **`A`** | Steer Left | Counter-clockwise in-place rotation ($+1.5\text{ rad/s}$) |
| **`D`** | Steer Right | Clockwise in-place rotation ($-1.5\text{ rad/s}$) |
| **`R`** | Raise Forks | Smoothly elevates forklift carriage ($+0.15\text{ m/s}$) |
| **`F`** | Lower Forks | Smoothly lowers forklift carriage ($-0.15\text{ m/s}$) |

*(Note: Manual keyboard driving takes momentary precedence over ROS commands for quick manual testing and physics debugging).*

---

## 6. ROS Topic Interfaces

| Topic | Direction | Type | Rate | Description |
| :--- | :---: | :--- | :---: | :--- |
| `/cmd_vel` | Unity $\leftarrow$ ROS | `geometry_msgs/Twist` | Event / 20 Hz | Linear $X$ and angular $Z$ velocity setpoints. |
| `/lift_cmd` | Unity $\leftarrow$ ROS | `std_msgs/Float32` | Event | Target mast elevation height ($0.00\text{ m} \to 0.75\text{ m}$). |
| `/odom` | Unity $\rightarrow$ ROS | `nav_msgs/Odometry` | 20 Hz | Ground-truth planar pose of `drive_center` + velocity. |
| `/tf` | Unity $\rightarrow$ ROS | `tf2_msgs/TFMessage` | 20 Hz | Dynamic coordinate transform `odom -> drive_center`. |
| `/scan` | Unity $\rightarrow$ ROS | `sensor_msgs/LaserScan` | 10 Hz | 181-ray 2D planar LiDAR distance readings. |
| `/clock` | Unity $\rightarrow$ ROS | `rosgraph_msgs/Clock` | 20 Hz | Simulation time clock (requires ROS `/use_sim_time: true`). |

---

## 7. Connecting to ROS 1 in Docker

1. Ensure the Docker container is running and has launched the bridge:
   ```bash
   roslaunch amr_navigation unity_bridge.launch
   ```
   *(Or launch the complete stack with `roslaunch amr_navigation warehouse_autonav.launch`).*
2. In Unity, check the **ROS Connection Settings** (Unity Top Menu: **Robotics** $\rightarrow$ **ROS Settings**):
   * **ROS IP Address**: `127.0.0.1` (or your Docker host IP)
   * **ROS Port**: `10000`
   * **Connect on Startup**: Checked
3. Press **Play (`▶`)** in Unity. The blue ROS network indicator in the top-right of the Game window will turn active with green arrows indicating live bidirectional traffic.

---

## 8. Physical Properties & Warehouse Metrics

For accurate docking and collision avoidance, the Unity scene conforms to the following standardized dimensions:
* **Ground Double-Stack Pallet Cavity**: $0.00\text{ m} \to 0.060\text{ m}$ entry window; fork bottom enters at $0.030\text{ m}$ ($q = -0.020\text{ m}$).
* **Second-Tier Rack Shelf Deck**: $0.650\text{ m}$ elevation; fork cavity $0.650\text{ m} \to 0.710\text{ m}$ ($q = +0.640\text{ m}$).
* **Staging Station Deck**: Located at $(X = 13.12\text{ m}, Y = 0.0\text{ m})$, deck height $0.250\text{ m}$.
* **Transit Height**: Mast elevated to $q = +0.280\text{ m}$ provides $40\text{ mm}$ clearance over the $0.25\text{ m}$ staging station.
* **Detailed Dimensions**: See [`CollaborativeAMRStockerization/docs/WAREHOUSE_METRICS_AND_CLEARANCE.md`](../CollaborativeAMRStockerization/docs/WAREHOUSE_METRICS_AND_CLEARANCE.md) for full physical schematics and kinematic lift windows.
