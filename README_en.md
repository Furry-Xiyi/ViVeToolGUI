Xiyi ViVeTool GUI

<p align="center">
  <strong>A modern graphical user interface for ViVeTool on Windows</strong>
</p>
<p align="center">
  <a href="README.md">简体中文</a>
</p>
<p align="center">
  <a href="https://apps.microsoft.com/detail/9P1BW5WB82MH">
    <img src="https://get.microsoft.com/images/en-us%20dark.svg" width="220" alt="Get it from the Microsoft Store">
  </a>
</p>
<p align="center">
  <a href="https://apps.microsoft.com/detail/9P1BW5WB82MH">
    <img src="https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?style=flat-square&logo=microsoft&logoColor=white" alt="Microsoft Store">
  </a>
  <img src="https://img.shields.io/badge/C%23-.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt="C# .NET">
  <img src="https://img.shields.io/badge/WinUI-Windows%20UI-0078D4?style=flat-square&logo=windows&logoColor=white" alt="WinUI">
  <img src="https://img.shields.io/badge/Windows-11-0078D4?style=flat-square&logo=windows11&logoColor=white" alt="Windows 11">
  <img src="https://img.shields.io/badge/ViVeTool-GUI-5C2D91?style=flat-square" alt="ViVeTool GUI">
</p>

⸻

Introduction

Xiyi ViVeTool GUI is a modern graphical user interface (GUI) for ViVeTool designed for Windows users.

It provides a visual interface for common ViVeTool operations, allowing users to view, search, and manage Windows Features without manually entering ViVeTool commands.

The project is primarily intended for Windows 11 users, Windows Insider users, and users interested in Windows hidden features and experimental Features.

Features

* View Windows Features
* Search by Feature ID
* Search by Feature name
* View Feature states
* Enable Features
* Disable Features
* Reset Features
* Manage Runtime State
* Manage Boot State
* Perform ViVeTool operations through a graphical interface
* Modern Windows GUI
* Simplify ViVeTool usage for users who prefer a graphical interface

Why a GUI?

ViVeTool is traditionally operated through the command line:

vivetool /enable /id:12345678

The command-line interface is highly efficient for advanced Windows users. However, it requires users to remember command syntax and Feature IDs.

Xiyi ViVeTool GUI provides a visual way to browse, search, and modify Windows Features.

What is ViVeTool?

ViVeTool is a command-line utility used to configure Windows Features.

Windows contains many internal Features controlled by Feature IDs. These Features can have different states, including:

* Default
* Enabled
* Disabled
* Boot
* Runtime
* Experimental

ViVeTool can modify these states using Feature IDs.

Suitable For

* Windows 11 users
* Windows Insider users
* Windows Feature research
* Windows experimental feature testing
* ViVeTool users
* Windows Feature ID management
* Exploring Windows hidden features
* Advanced Windows configuration

Notes

Windows Features are closely related to the Windows Build version.

The same Feature ID may behave differently, or may not exist, across different Windows versions, builds, or Insider Channels.

Enabling experimental Features may cause:

* Unexpected behavior
* UI issues
* Changes to system component behavior
* Compatibility issues between Features
* Reduced stability on Insider Builds

Please make sure that the Feature ID is compatible with your current Windows Build before using it.

This project is not:

* PeterStrick/ViVeTool-GUI
* MadCkull/ViVe-3
* ViveTool WinUI
* Other third-party ViVeTool GUI projects

License

⚠️ This package includes ViVeTool, licensed under GPLv3.

ViVeTool source:

