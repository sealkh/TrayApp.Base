# Base Tray Application Template (.NET 10)

A lightweight, high-performance infrastructure boilerplate for native Windows tray applications using pure Win32 API endpoints. It acts as an abstract foundation meant to be inherited, imported, or extended by down-stream modular services.

## Core Features Included
*   **Zero-UI Overhead:** Completely headless launch directly into the Windows System Tray.
*   **Interactive Controls:** Single-process double-click to open status window / configuration dashboard.
*   **Standardized Context Menu Layout:**
    *   `Open` - Restores interface context.
    *   `Settings` - Loads fundamental configuration blocks.
    *   `---` (Visual Separator)
    *   `Exit` - Graceful application teardown.
*   **Global Configuration Parameters:** Localization context switching, Automated Upstream Deployment toggles, On-demand version validation.

## Self-Updating Workflow
