<p align="center">
  <img src="src/icon.png" alt="WinAPX Logo" width="160"/>
</p>

<h1 align="center">WinAPX</h1>
<p align="center"><strong>Linux Development in Windows, Simplified.</strong></p>

<p align="center">
  <a href="https://github.com/HarshTheSharma/WinAPX/actions/workflows/build.yml">
    <img src="https://github.com/HarshTheSharma/WinAPX/actions/workflows/build.yml/badge.svg" alt="Build Status">
  </a>
</p>

---

## Overview

WinAPX is a Windows application for creating, managing, and exporting Linux environments through the Windows Subsystem for Linux (WSL).  
Inspired by the APX package manager, WinAPX provides an intuitive desktop interface and a powerful CLI to make Linux development on Windows seamless.

The application supports:
- Ubuntu and Arch Linux sandboxes  
- Environment creation and import  
- Exporting Linux applications as native Windows shortcuts  
- A consistent and complete command-line interface  
- Both GUI and CLI workflows

Built with:
- C#
- .NET 8
- WinUI 3
- WSL

---

## Features

### Create & Import Environments
Easily create Ubuntu or Arch environments, or import existing ones from a `.tar` archive.  
During creation, users can configure:
- Custom install folder  
- Default working directory  
- Optional recommended packages  

### Environment Management
All environments you create are accessible from a unified view.  
Quickly:
- Switch between Ubuntu and Arch sandboxes  
- Launch a shell  
- Export apps  
- Export entire environments into archives  

### Application Exporting
Run Linux apps like native Windows applications.  
You can export:
- Browsers (Firefox, etc.)  
- Development tools (VS Code)  
- Any Linux command or GUI app  

Exported apps appear as standard Windows shortcuts and can be launched with a double-click.

### Command Line Interface
Prefer the terminal?  
Everything available in the GUI is also supported through the CLI via a consistent and clear grammar:

```

winapx list
winapx create <env> [--distro <distro>] [--install-dir <path>] [--packages] […]
winapx import <env> <tar>
winapx export <env> <app> [--command <cmd>] […]
winapx delete <env>
winapx launch <app>

```

The CLI and GUI are fully interchangeable.

---

## Use Cases

- Isolated development environments  
- Sandbox containers for specific applications  
- Clean per-project Linux setups  
- Exportable, shareable Linux workloads  

---

## Download

The latest builds are available in Releases.

---

## License

This project is licensed under the MIT License.

---

## Author

Harsh Sharma  
Spring 2026 Senior Capstone Project  
Advisor: Oleksiy Al-saadi  
California State University, Chico
