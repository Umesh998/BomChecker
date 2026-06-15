# BOM Checker Component Verification System

This application is an automated, data-driven **Bill of Materials (BOM) verification and alternate-part matching engine** designed for the Electronic Manufacturing Services (EMS) industry. It streamlines component sourcing by eliminating the manual, time-consuming process of cross-referencing raw engineering descriptions with real-time distributor data.

## Key Features
* **Dynamic Column Mapping:** Handles complex source sheets containing multiple part number columns. Users can choose exactly which columns to analyze during the upload process.
* **Intelligent Title Analysis:** Aggregates and compares the primary "Original Description" of a component against real-time API descriptions of selected online parts.
* **One-to-Many Part Resolution:** Since a single component description can fetch multiple candidate parts online, the core algorithm systematically scores each variant to determine the absolute **Best Match** for maximum compatibility.
* **Granular Technical Verification:** Extracts and verifies critical hardware constraints directly onto a unified dashboard, including:
  * **Package / Footprint Case Size** (e.g., 0603, 1206, 0805)
  * **Moisture Sensitivity Level (MSL)** (e.g., MSL 1 - Unlimited)
  * **Mounting Type** (SMT vs. Through-Hole)

## Tech Stack
* **Backend:** ASP.NET Core MVC (C#), Entity Framework Core
* **Frontend:** Responsive HTML5, CSS Custom Variables (featuring native Dark/Light mode overrides), and Bootstrap 5
* **Desktop Wrapper:** Electron.NET (Compiled as a standalone native desktop application)
