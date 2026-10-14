# Dive Center Management System

A Windows desktop management application designed for scuba diving centers.

Built with **.NET 10**, **WPF**, **MVVM**, **Entity Framework Core**, and **SQLite**, the application provides an offline-first solution for managing reservations, daily operations, staff, equipment, financial transactions, reporting, and local data.

Developed in **2026**.

---

## Overview

Dive Center Management System is designed to centralize the operational workflow of a scuba diving center in a single desktop application.

The application works locally without requiring a permanent internet connection and stores its data in a SQLite database.

The project focuses on practical dive center operations, structured application architecture, data integrity, validation, and maintainability.

---

## Features

### Dashboard

Provides an overview of current dive center activity, including:

- Today's reservations
- Participant totals
- Upcoming reservations
- Completed reservations
- Recent activity
- Income and expense summaries
- Currency-specific balances

### Reservations

Manage dive and course reservations with:

- Reservation numbers
- Activity / course selection
- Date and participant count
- Optional instructor assignment
- Optional dive site assignment
- Optional boat assignment
- Reservation status management
- Linked payments

Supported reservation statuses include:

- Pending
- Confirmed
- Completed
- Cancelled
- No Show

### Calendar

A calendar-based operational view for planning and reviewing reservations.

Supports:

- Daily planning
- Weekly planning
- Chronological reservation display
- Participant totals
- Opening and editing existing reservations
- Creating reservations for selected dates

### Activities / Courses

Manage the activities and courses offered by the dive center.

Records can be:

- Created
- Edited
- Searched
- Activated
- Deactivated

### Dive Sites

Maintain the dive center's dive site catalog.

### Staff

Manage staff members and their operational roles.

The system supports multiple roles per staff member, including instructor-related assignments.

### Boats

Manage boats used in dive center operations.

### Equipment

Maintain basic equipment inventory and operational information.

### Transactions

Manage cash movements such as:

- Income
- Expenses
- Reservation payments
- Transaction categories
- Multiple currencies

Currencies are tracked independently and are never automatically converted or combined.

### Reports

The application includes operational and financial reporting with filters for areas such as:

- Date ranges
- Activities
- Dive sites
- Instructors
- Currencies
- Reservations
- Financial transactions

### Settings

Manage application and reference data used throughout the system.

---

## Architecture

The application follows an **MVVM-based desktop architecture**.

```text
DiveCenterManager
│
├── Models
├── Data
│   └── Migrations
├── Services
├── ViewModels
├── Views
├── Commands
├── Converters
├── Helpers
├── Resources
└── Diagnostics
```

### Models

Contain application entities, enums, and validation rules.

### Data

Contains:

- Entity Framework Core DbContext
- SQLite configuration
- Code First migrations
- Database initialization
- Reference data
- Optional development sample data

### Services

Application logic and data access are separated into focused services such as:

- ReservationService
- ActivityService
- DiveSiteService
- StaffService
- BoatService
- EquipmentService
- TransactionService
- DashboardService
- ReportService
- BackupService

### ViewModels

Handle presentation state, commands, filtering, navigation, and communication between the user interface and application services.

ViewModels do not directly access SQLite or the Entity Framework DbContext.

### Views

WPF views are implemented using XAML and reusable application resources.

---

## Technology Stack

| Technology | Purpose |
|---|---|
| C# | Application development |
| .NET 10 | Application platform |
| WPF | Windows desktop user interface |
| MVVM | Presentation architecture |
| Entity Framework Core | Data access |
| SQLite | Local relational database |
| Microsoft Dependency Injection | Dependency management |
| XAML | UI definition |
| PowerShell | Build, verification and publishing scripts |

---

## Database

The application uses **SQLite** as its local database.

Entity Framework Core Code First migrations are used to manage database schema changes.

The default database is stored in the user's local application data directory.

```text
%LOCALAPPDATA%\DiveHubManagement\DiveCenter.db
```

The application includes safeguards for:

- Database migrations
- Historical data
- Foreign key relationships
- Invalid data paths
- Read-only databases
- Database backups

---

## Backup

The application includes built-in database backup functionality.

Users can create a verified SQLite backup directly from the application.

The backup process:

1. Creates a database snapshot
2. Includes committed SQLite WAL data
3. Verifies database integrity
4. Prevents the live database from being overwritten
5. Saves the verified backup to a user-selected location

---

## Validation

The application includes validation for operational and financial data.

Examples include:

- Required fields
- Email validation
- Boat capacity
- Duplicate participants
- Staff role requirements
- Reservation status transitions
- Dive measurements
- Nitrox oxygen percentage
- Transaction categories
- Currency rules
- Historical record protection

---

## Verification

The project contains an automated acceptance and verification system.

Current verification includes:

- Release build verification
- EF Core / SQLite integration checks
- Persistence checks across application restarts
- WPF binding diagnostics
- UI rendering checks
- Database migration validation
- Database backup validation
- Multiple culture / decimal handling checks
- Isolated sample database testing

The project has successfully passed more than **120 application and persistence checks**.

---

## Build

### Requirements

- Windows
- .NET 10 SDK

Clone the repository:

```powershell
git clone https://github.com/hakanberat/Desktop-Dive-Center-Management-System.git
```

Open the project directory:

```powershell
cd Desktop-Dive-Center-Management-System
```

Restore dependencies:

```powershell
dotnet restore
```

Build the solution:

```powershell
dotnet build DiveCenterManager.sln
```

Run the application:

```powershell
dotnet run --project DiveCenterManager
```

---

## Release Build

```powershell
dotnet build DiveCenterManager.sln -c Release
```

---

## Publish

Framework-dependent Windows x64 build:

```powershell
dotnet publish DiveCenterManager\DiveCenterManager.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false
```

A reusable PowerShell publishing script is also included:

```text
scripts/Publish.ps1
```

---

## Project Documentation

Additional technical documentation is available in the `docs` directory:

```text
docs/
├── ARCHITECTURE.md
├── BRANDING.md
└── VALIDATION.md
```

These documents contain additional information about the application architecture, validation strategy, data rules, and project resources.

---

## Project Goals

This project was developed to demonstrate practical experience with:

- C# application development
- .NET desktop development
- WPF
- MVVM architecture
- Relational data modelling
- Entity Framework Core
- SQLite
- Dependency Injection
- Validation and business rules
- Database migrations
- Backup and recovery
- Reporting
- Application testing and verification
- Real-world operational software design

---

## Author

**Hakan Berat Demircan**

GitHub: [hakanberat](https://github.com/hakanberat)
