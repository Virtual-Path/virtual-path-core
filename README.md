
<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/Avalonia-11.3-8B5CF6?style=for-the-badge&logo=avalonia" alt="Avalonia 11.3"/>
  <img src="https://img.shields.io/badge/OpenGL-ES%203.00-5586A4?style=for-the-badge&logo=opengl" alt="OpenGL ES 3.0"/>
  <img src="https://img.shields.io/badge/license-MIT-green?style=for-the-badge" alt="MIT License"/>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D4?style=flat-square" alt="Cross-platform"/>
  <img src="https://img.shields.io/badge/build-passing-brightgreen?style=flat-square" alt="Build"/>
  <img src="https://img.shields.io/badge/PRs-welcome-orange?style=flat-square" alt="PRs Welcome"/>
</p>

<h1 align="center">VirtualPath Core</h1>

<p align="center">
  <b>A cross-platform 3D scene engine with PBR rendering, real-time editing, and multi-viewport support — built with Avalonia UI + OpenGL.</b>
</p>

<p align="center">
  <i>Edit, visualize, and export 3D scenes with a modern dark-themed interface.</i>
</p>

---

## Features

<table>
  <tr>
    <td width="50%">
      <h3> PBR Rendering</h3>
      <p>Physically Based Rendering with Cook-Torrance BRDF, metallic-roughness workflow, IBL-ready lighting, and normal/albedo texture mapping.</p>
    </td>
    <td width="50%">
      <h3> Multi-Viewport</h3>
      <p>Up to 4 simultaneous views: Perspective + Top / Front / Right orthographic projections with dynamic reflow layout and per-viewport labels.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3> Primitive Library</h3>
      <p>Built-in primitives: Cube, Sphere, Cylinder, Cone, Torus — each with generated geometry, custom colors, and PBR materials.</p>
    </td>
    <td>
      <h3> Transform Editing</h3>
      <p>Real-time position, rotation, and scale sliders with numeric readouts. Gizmo overlay for visual translation.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3> GLTF Import</h3>
      <p>Import GLTF/GLB models with full mesh data: positions, normals, texture coordinates, and node hierarchy.</p>
    </td>
    <td>
      <h3> MSAA Anti-Aliasing</h3>
      <p>Configurable multi-sample anti-aliasing (up to 16x) for crisp, high-quality rendering.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3> Interactive Gizmo</h3>
      <p>RGB axis gizmo at selected object position with distance-adaptive scaling for precise manipulation.</p>
    </td>
    <td>
      <h3> Grid & Axis Indicator</h3>
      <p>Configurable scene grid with subdivisions and a 3-axis orientation widget in the viewport corner.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3> Undo / Redo</h3>
      <p>Full undo/redo support for scene modifications (add, delete, transform, material changes) with 100-level stack.</p>
    </td>
    <td>
      <h3> Dark Theme</h3>
      <p>Modern Apple-inspired dark UI with card-based layout, consistent typography, and smooth interactions.</p>
    </td>
  </tr>
</table>

---

## Screenshots

> *(Replace these with actual screenshots of your app)*

| Multi-Viewport Layout |
|:---:|
| ![Multi-Viewport Layout](https://github.com/user-attachments/assets/06009b4a-3bdd-4e04-83ee-bd6b5c3d7571) |

---

##  Architecture

---

<img width="1717" height="916" alt="Image" src="https://github.com/user-attachments/assets/1ff8cfd0-d315-4b90-892b-0e471c6d7b73" />

---

### Design Principles

- **MVVM Architecture** — Clean separation between UI and business logic via `CommunityToolkit.Mvvm` with source generators
- **Abstraction-First** — `IGraphicsHost<T>` interface decouples the GL context from rendering services
- **Observable Everywhere** — `INotifyPropertyChanged` drives both UI updates and render invalidation
- **Thread-Safe Scene** — Lock-free snapshot pattern for thread-safe scene iteration during rendering

---

## 🛠️ Technology Stack

| Category | Technology |
|----------|-----------|
| **UI Framework** | [Avalonia UI](https://www.avaloniaui.net/) 11.3 — cross-platform desktop |
| **Language** | C# 12 (.NET 8.0) with nullable reference types |
| **Graphics API** | OpenGL ES 3.0 via [Silk.NET](https://github.com/dotnet/Silk.NET) 2.22 |
| **MVVM Toolkit** | [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) 8.4 — source generators |
| **Model Import** | GLTF/GLB via Silk.NET.Assimp + custom parser |
| **Serialization** | System.Text.Json for project files (.vpproj) |
| **Packaging** | Single executable with self-contained publish support |

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- A GPU with OpenGL ES 3.0 support (integrated or discrete)

### Build & Run

```bash
# Clone the repository
git clone https://github.com/yourusername/VirtualPath-Core.git
cd VirtualPath-Core

# Build the solution
dotnet build

# Run the desktop application
dotnet run --project VirtualPathCore.Desktop
```

### Publish as Standalone

```bash
dotnet publish VirtualPathCore.Desktop -c Release -r win-x64 --self-contained
```

---

## Virtual Camera (headless MJPEG server)

`VirtualPathCore.CameraBridge` can run the production-line scene **without any window** and serve
it as an MJPEG stream, so machine-vision software can treat the 3D engine as a camera:

```bash
dotnet run --project VirtualPathCore.CameraBridge -- --serve [--port 8080] [--width 1280] [--height 720] [--fps 30]
```

```
=== virtual camera server ===
  resolution : 1280x720
  fps        : 30
  stream url : http://127.0.0.1:8080/cam1
```

- The endpoint is **`/cam1`** — and only that. Any other path gets a 404.
- Consume it with OpenCV directly: `new VideoCapture("http://127.0.0.1:8080/cam1")`.
- Log lines worth knowing:
  - `[serve] client connected: <addr>` — a consumer attached
  - `[serve] streaming... frame N, pushed M` — frames flowing (`M < N` means a write failed)
  - `[serve] rejected /xxx (only /cam1 is served)` — a consumer asked for the wrong path; this
    repeats once per OpenCV backend attempt, so a wall of them means "the path is wrong",
    not "the server is broken"
- The scene (`DemoScene`) is a production inspection station: gantry, yellow warning lines,
  a three-colour tower beacon, conveyor, upstream/downstream totes, and workpieces that include
  deliberately non-conforming parts (dark red with a black marker post). Same-coloured decoy
  blocks sit on the back wall so detection is not trivially easy.
- `DemoScene.InspectionX` and `DemoScene.InspectionHalfWidth` (0 and 0.75) give the inspection
  station's world position — use them to align a vision ROI.

Pairing with `virtual-path-vision`: source *Network Stream*, host `127.0.0.1`, port `8080`,
path **`/cam1`**.

---

##  Controls

| Input | Action |
|-------|--------|
| **Left-click + drag** | Orbit camera |
| **Scroll wheel** | Zoom in/out |
| **Right-click + drag** | Pan camera |
| **Click object in tree** | Select object |
| **Slider drag** | Modify transform / material |

---

##  Project Structure

```
VirtualPath-Core/
├── VirtualPathCore/                    # Core library
│   ├── Contracts/                      # Abstraction interfaces
│   ├── Graphics/
│   │   ├── Core/                       # Scene graph, Transform, Camera, Material, Light
│   │   └── OpenGL/                     # Renderer, Shader, Mesh, Texture, Buffer
│   ├── Helpers/                        # MeshFactory, Math utilities, Converters
│   ├── Models/                         # Serializable data models
│   ├── Resources/
│   │   ├── Shaders/                    # GLSL shaders (PBR, Grid, Gizmo)
│   │   ├── Languages/                  # i18n JSON files (zh-CN, en-US)
│   │   └── Themes/                     # UI theme resources (Dark)
│   ├── Services/                       # Scene management, settings, i18n, undo/redo
│   ├── ViewModels/                     # MVVM view models
│   └── Views/                          # Avalonia XAML+code-behind
├── VirtualPathCore.Desktop/            # Desktop host executable (Avalonia)
├── VirtualPathCore.CameraBridge/        # Headless host: offscreen rendering + MJPEG server
│   ├── Program.cs                      # Entry point / argument dispatch
│   ├── ServeProgram.cs                 # --serve: virtual camera
│   ├── MjpegServer.cs                  # Minimal multipart/x-mixed-replace server (/cam1)
│   ├── DemoScene.cs                    # Production inspection station scene
│   ├── HeadlessGraphicsHost.cs         # Offscreen GL context
│   └── *Probe.cs, *Program.cs          # One-off diagnostics (matrix layout, GPU NDC, …)
└── VirtualPathCore.Tests/              # xunit regression tests
    ├── TransformTests.cs               # Matrix/camera math
    └── RenderingRegressionTests.cs     # Projection and clipping invariants
```

> The `*Probe.cs` / `*Program.cs` files in `VirtualPathCore.CameraBridge` are **throwaway
> diagnostics** kept for reproducibility: each one settled a specific question that static
> reading could not (matrix upload conventions, GPU ground truth, tangents). Run them
> explicitly rather than expecting them in normal use.

---

##  Roadmap

- [x] PBR rendering pipeline
- [x] Primitive generation (Cube, Sphere, Cylinder, Cone, Torus)
- [x] GLTF model import
- [x] Multi-viewport layout
- [x] Transform editing (position, rotation, scale)
- [x] Undo / Redo system
- [x] Translation gizmo
- [x] Dark theme UI
- [ ] Rotation & scale gizmo
- [ ] Material editor (texture selection)
- [ ] Scene hierarchy drag-and-drop
- [ ] Export to GLTF
- [ ] Scene animation / keyframing
- [ ] Linux & macOS packaging

---

##  Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feat/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: add amazing feature'`)
4. Push to the branch (`git push origin feat/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

Distributed under the **MIT License**. See `LICENSE` for more information.

---

<p align="center">
  Made with ❤️ by <a href="https://github.com/anomalyco">anomalyco</a>
</p>
