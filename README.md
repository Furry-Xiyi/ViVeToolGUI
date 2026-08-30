# 惜忆 ViVeTool 图形界面

<p align="right">
  <a href="README_EN.md">🇺🇸English</a>
</p>

<p align="center">
  <strong>现代化的 Windows ViVeTool 图形界面</strong>
</p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9P1BW5WB82MH">
    <picture>
      <source media="(prefers-color-scheme: dark)" srcset="https://get.microsoft.com/images/zh-cn%20dark.svg">
      <source media="(prefers-color-scheme: light)" srcset="https://get.microsoft.com/images/zh-cn%20light.svg">
      <img src="https://get.microsoft.com/images/zh-cn%20light.svg" width="220" alt="从 Microsoft Store 获取">
    </picture>
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

---

### 简介

惜忆 ViVeTool 图形界面 是一个面向 Windows 用户的使用 WinUI 3 构建的现代化 ViVeTool 图形用户界面。

它将 ViVeTool 的命令行操作转换为直观的图形界面，使用户可以查看、搜索和管理 Windows Feature，而无需手动输入 ViVeTool 命令。

本项目主要面向 Windows 11、Windows Insider 以及对 Windows 隐藏功能和实验性 Feature 感兴趣的用户。

### 主要功能

* 查看 Windows Feature
* 搜索 Feature ID
* 搜索 Feature 名称
* 查看 Feature 状态
* 启用 Feature
* 禁用 Feature
* 重置 Feature
* 管理 Runtime State
* 管理 Boot State
* 图形化执行 ViVeTool 操作
* Windows 现代化 GUI
* 降低 ViVeTool 命令行操作的使用门槛

### 为什么需要 GUI？

传统 ViVeTool 主要通过命令行操作：

vivetool /enable /id:12345678

对于熟悉 Windows 内部 Feature 的高级用户来说，命令行非常高效；但对于普通用户而言，需要记忆命令格式和 Feature ID。

惜忆 ViVeTool 图形界面提供可视化的 Feature 管理方式，让用户能够直接浏览、搜索并修改 Feature。

### ViVeTool 是什么？

ViVeTool 是一个用于配置 Windows Feature 的命令行工具。

Windows 内部存在大量由 Feature ID 控制的功能。这些 Feature 可以处于不同状态，例如：

* Default
* Enabled
* Disabled
* Boot
* Runtime
* Experimental

ViVeTool 可以通过 Feature ID 修改这些状态。

### 本工具适用于

* Windows 11 用户
* Windows Insider 用户
* Windows Feature 研究
* Windows 实验性功能测试
* ViVeTool 用户
* Windows Feature ID 管理
* Windows 隐藏功能探索
* Windows 系统高级配置

### 注意事项

Windows Feature 与 Windows Build 密切相关。

同一个 Feature ID 在不同 Windows 版本、内部版本或 Insider Channel 中可能具有不同的行为，也可能不存在。

### 启用实验性 Feature 可能造成：

* 功能异常
* UI 异常
* 系统组件行为改变
* Feature 之间产生兼容性问题
* Insider Build 稳定性下降

使用前请确认 Feature ID 与当前 Windows Build 相匹配。

### 本项目不是以下项目的分支：

* PeterStrick/ViVeTool-GUI
* MadCkull/ViVe-3
* 其他第三方 ViVeTool GUI 项目

### 许可证

#### ⚠️ 本软件包含 ViVeTool，ViVeTool 根据 GPLv3 许可证授权。

ViVeTool 源代码：

https://github.com/thebookisclosed/ViVe