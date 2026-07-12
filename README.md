
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

<h1 align="center">🖥️ VirtualPath Core</h1>

<p align="center">
  <b>A cross-platform 3D scene engine with PBR rendering, real-time editing, and multi-viewport support — built with Avalonia UI + OpenGL.</b>
</p>

<p align="center">
  <i>Edit, visualize, and export 3D scenes with a modern dark-themed interface.</i>
</p>

---

## ✨ Features

<table>
  <tr>
    <td width="50%">
      <h3>🎨 PBR Rendering</h3>
      <p>Physically Based Rendering with Cook-Torrance BRDF, metallic-roughness workflow, IBL-ready lighting, and normal/albedo texture mapping.</p>
    </td>
    <td width="50%">
      <h3>📐 Multi-Viewport</h3>
      <p>Up to 4 simultaneous views: Perspective + Top / Front / Right orthographic projections with dynamic reflow layout and per-viewport labels.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3>🧊 Primitive Library</h3>
      <p>Built-in primitives: Cube, Sphere, Cylinder, Cone, Torus — each with generated geometry, custom colors, and PBR materials.</p>
    </td>
    <td>
      <h3>🔄 Transform Editing</h3>
      <p>Real-time position, rotation, and scale sliders with numeric readouts. Gizmo overlay for visual translation.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3>🌐 GLTF Import</h3>
      <p>Import GLTF/GLB models with full mesh data: positions, normals, texture coordinates, and node hierarchy.</p>
    </td>
    <td>
      <h3>⚡ MSAA Anti-Aliasing</h3>
      <p>Configurable multi-sample anti-aliasing (up to 16x) for crisp, high-quality rendering.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3>🎯 Interactive Gizmo</h3>
      <p>RGB axis gizmo at selected object position with distance-adaptive scaling for precise manipulation.</p>
    </td>
    <td>
      <h3>🌍 Grid & Axis Indicator</h3>
      <p>Configurable scene grid with subdivisions and a 3-axis orientation widget in the viewport corner.</p>
    </td>
  </tr>
  <tr>
    <td>
      <h3>↩️ Undo / Redo</h3>
      <p>Full undo/redo support for scene modifications (add, delete, transform, material changes) with 100-level stack.</p>
    </td>
    <td>
      <h3>🌙 Dark Theme</h3>
      <p>Modern Apple-inspired dark UI with card-based layout, consistent typography, and smooth interactions.</p>
    </td>
  </tr>
</table>

---

## 🖼️ Screenshots

> *(Replace these with actual screenshots of your app)*

| Perspective Viewport | Multi-Viewport Layout |
|:---:|:---:|
| ![Perspective](https://github.com/user-attachments/assets/5cc7c503-105e-431e-b3c9-3776e36c87ab) | ![Multi-Viewport](https://github.com/user-attachments/assets/d25ec287-d6cc-46b4-8cd1-0a2227b791e9) |

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   VirtualPath Core                       │
├────────────┬────────────┬──────────────┬────────────────┤
│   Views     │  ViewModels │   Services    │   Graphics     │
│ (Avalonia)  │   (MVVM)   │              │   (OpenGL)      │
├────────────┼────────────┼──────────────┼────────────────┤
│ MainWindow │ MainVM     │ SceneService │ Renderer        │
│ Properties │ SceneObjVM │ DrawService  │ RenderPipeline  │
│ Viewport   │ AppVM      │ Settings     │ Mesh / Shader   │
│ TreeView   │            │ Language     │ Transform       │
│ Menu       │            │ UndoRedo     │ Camera / Light  │
│            │            │ ModelImport  │ SceneObject     │
└────────────┴────────────┴──────────────┴────────────────┘
```

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

## 📦 Getting Started

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

## 🎮 Controls

| Input | Action |
|-------|--------|
| **Left-click + drag** | Orbit camera |
| **Scroll wheel** | Zoom in/out |
| **Right-click + drag** | Pan camera |
| **Click object in tree** | Select object |
| **Slider drag** | Modify transform / material |

---

## 📁 Project Structure

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
└── VirtualPathCore.Desktop/            # Desktop host executable
```

---

## 🧪 Roadmap

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

## 🤝 Contributing

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
