# 🛣️ EasyLine — Spline Mesh Deformation & Prefab Placement

**EasyLine** is a professional, performance-driven tool for drawing splines and automatically deforming 3D meshes and prefabs in Unity. It is perfectly suited for creating roads, pipes, vines, race tracks, barriers, and animated conveyors.

Draw paths directly onto your terrain, attach any 3D model, and watch it automatically bend, tile, and connect along the curve — all in real-time, right in the Scene View.

---

## 🎨 Pipeline Compatibility
EasyLine is **100% Render Pipeline Agnostic**. Since it generates standard Unity geometry and uses a standard `MeshRenderer`, it is fully compatible with:
*   **Built-in Render Pipeline**
*   **URP (Universal Render Pipeline)**
*   **HDRP (High Definition Render Pipeline)**

You can use any shaders, materials, or lighting systems provided by your chosen pipeline.

---

## 📖 User Guide & Documentation

### 1. Getting Started (Drawing Splines on Surfaces)

#### Basic Road Creation
1.  **Create Object**: Right Click in Hierarchy → Create Empty. Name it (e.g., "Main Road").
2.  **Add Component**: Add the `Bezier Spline` component. A small starting curve with 4 points will appear.

#### Fast Surface Drawing (The Draw Tool) 🖌️
Instead of manually moving each point, use the dedicated **Global Tool** to "paint" paths directly onto your terrain:
1.  **Select Spline**: Select your spline object in the Hierarchy.
2.  **Activate Tool**: Click the **EasyLine Spline Tool icon** in the top Unity toolbar (near Move/Rotate/Scale).
3.  **Draw**:
    *   **Left Click**: Place the next segment. It automatically snaps to surface height!
    *   **Click and Drag**: Hold the left mouse button while placing a point to define the Bezier curvature (tangents).
4.  **Smart Features**:
    *   **Auto-Extend**: Click near the last anchor point to extend the line.
    *   **Auto-New**: Click far away (>20m) to automatically create a *new* spline object for a new path.
    *   **Insert Point**: Hover over the line between anchors and click the green sphere to insert a new point.

---

### 2. Attaching 3D Models (Mesh / Prefab) 🧩

Be sure your object has the `SplineMeshDeformer` component (usually added automatically).

#### Source Modes:
*   **Mesh Mode**: Drag a raw `.fbx` or `.obj` into the `Source Mesh` field. 
    > [!IMPORTANT]
    > In Mesh Mode, you must manually assign materials in the `Materials` slots.
*   **Prefab Mode**: Assign your Prefab from the Project window. EasyLine extracts the geometry while preserving materials and hierarchy.
*   **Mixed Meshes**: Toggle the `Mixed Meshes` checkbox to sequence different models (e.g., *Road → Road with Lamp → Bridge*) along the path.

*Tip: Use the **Forward Axis** dropdown if your model is facing the wrong direction.*

---

### 🛡️ Element Specific Constraints (Layer Overrides)

When using **Mixed Meshes**, each layer can have its own set of powerful constraints. This allows you to mix rigid objects (like pillars) with deforming objects (like high-tension cables) on the same spline.

#### 🔧 Core Constraints per Element:
*   **Stretch to Index Ends**: 🎯 Forces the model to scale perfectly along the forward axis to fill the exact range between its Start and End indices. This is essential for modular bridge sections or pipes where gaps are unacceptable.
*   **Force 100% Upright**: ⬆️ Ensures the object always points "World Up," ignoring any banking or twisting of the spline. Perfect for street lamps, trees, or fence posts on hills.
*   **Deform Props**: ⛓️ By default, props are rigid. Enabling this allows individual small pieces (like a flexible segment of a railing) to bend along the curve while others remain stiff.
*   **Rotation Locks (X, Y, Z)**: 🔒 Freeze specific rotation axes. For example, lock **Z (Roll)** to keep a train carriage flat on a banking curve, or lock **X (Pitch)** to keep a ladder vertical.
*   **Allow Box Simplification**: 📦 If global "Simplify Props as Boxes" is on, you can uncheck this for specific high-detail elements that *must* have accurate mesh collision.

#### 🎨 Modeling Adjustments:
*   **Position & Rotation Offsets**: Shift your model sideways, upwards, or rotate it locally without touching the original asset.
*   **Local Scale & Flip**: Quickly resize or flip the mesh on X, Y, or Z axes.

> [!NOTE]
> **Priority Logic**: Elements lower in the list override those above them if their indices overlap. This allows you to "layer" a base road and then place specialized bridge sections or decorative props exactly where needed.

---

### 3. Longitudinal Cut (Subdivision) 📐

To make meshes bend smoothly on curves without looking faceted, you can use the **Longitudinal Cut (subdivision)** slider (supports values from `0` to `4`):

* **Directional Splits**: Unlike traditional subdivision which splits triangles across all axes (multiplying triangle counts by $4^N$), the Longitudinal Cut algorithm splits triangles **only along edges parallel/diagonal to the spline's forward axis**.
* **Linear Performance**: The polygon/vertex counts scale linearly ($2^N$) rather than quadratically ($4^N$). The width profile of your road or model remains clean and low-poly, saving massive rendering and baking budget.
* **Control**: Available as a global slider in the main settings (visible mesh only) or per-layer in the Mixed Meshes settings for individual control.

---

#### Scaling Nodes (Widening & Narrowing)
*   Select an **Anchor Point** (white sphere).
*   Switch to the **Scale Tool (R)**. 
*   Pull the **Red (X)** or **Green (Y)** handles to smoothly taper or widen the road at that specific point.

#### Banking / Tilt Tool 🎢
*   Select an **Anchor Point**.
*   Switch to the **Rotate Tool (E)**. A **Cyan Ring** will appear.
*   Rotate the ring to bank the mesh. The system smoothly interpolates the twist between anchors.

> [!TIP]
> Use **Smooth Bend Strength** in Advanced Options to keep high-speed turns from looking jagged.

---

### 4. Tips for Perfect Paths ⚡

*   **Stretch to Fit Curve**: Stretches the mesh to cover exactly 100% of the spline, eliminating gaps at the end.
*   **Overlap Offset**: Fix models with incorrect pivots. A small negative value can close tiny gaps between segments.
*   **Loop Curve**: Perfectly closes the spline into an infinite circle (ideal for racetracks).

---

### 5. Conveyors and Animation 🏃‍♂️

EasyLine includes an optimized module for moving meshes (tank tracks, factory belts, rivers).
1.  Enable **Animate Conveyor** in the inspector.
2.  Set your **Conveyor Speed**.

#### Optimization Features:
*   **Static Collider**: Keeps the physical collider stationary while the visual mesh moves. This saves massive CPU overhead by avoiding constant physics rebaking.
*   **Fast Animation Mode (LUT)**: Uses a pre-calculated Look-Up Table for per-vertex math, allowing hundreds of animated splines to run simultaneously.

---

### 6. Lighting and Smooth Normals 💡

If segments show hard edges at the joins:
*   Ensure **Smooth Normals** is enabled in the Advanced Options.
*   The tool will automatically blend vertex normals along the entire path, even across loop seams.

---

### 7. Export & Baking (Finalizing) 📦

Once your path is perfect, you can "bake" it for production:
*   **Bake To Prefab**: Saves the deformed 3D model to disk and builds a static object. Inherits all colliders/lighting.
*   **Export To OBJ**: Generates a standard `.obj` file for further editing in Blender, Maya, or any DCC tool.

---

### 🏗️ Spline Prefab Instantiator

Beyond simple meshes, the Instantiator allows you to scatter complex GameObjects along the spline.
*   **Hybrid Deformation**: Toggle **Deform Meshes** to bend child geometry while keeping scripts, lights, and VFX intact.
*   **Precise Spacing**: Choose between **Total Count** or **Fixed Distance**.
*   **Natural Randomization**: Add jitter to position, rotation, and scale for organic-looking forests or debris.
*   **Preserve Geometry**: A single toggle ensures prefabs keep their exact scale and shape.

---

## 📖 Official Documentation
Detailed online documentation:  
👉 [EasyLine Sites Guide](https://sites.google.com/view/easyline-ripvertices/home?authuser=1)
