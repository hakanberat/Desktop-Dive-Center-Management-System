# Dive Center Management System — Windows Desktop

A Windows desktop application for managing scuba diving center operations, built with **C#, .NET 10, WPF, MVVM, Entity Framework Core and SQLite**.

Manage reservations, schedules, staff, boats, equipment and financial transactions locally, with automatic database initialization and built-in database backup.

Developed by **Hakan Berat Demircan**, drawing on experience as a scuba diving instructor and former dive center co-owner.

## Overview

The application brings everyday dive center administration into a single desktop workspace.

It runs locally without requiring a backend server, SQL Server installation or internet connection for core operations. Data is stored in SQLite, and Entity Framework Core migrations initialize and update the database.

The interface uses **Dive Hub Management** branding.

## Main Features

| Module | Functions |
|---|---|
| Dashboard | Operational overview and currency-specific financial summaries |
| Reservations | Booking creation, editing, search, status management and linked transactions |
| Calendar | Day/week planning, participant totals and reservation access |
| Activities / Courses | Activity types, default prices, currencies and activation |
| Dive Sites | Location, coordinates, depth information and notes |
| Staff | Contact details, certification information and multiple operational roles |
| Boats | Registration details, capacity and activation |
| Equipment | Type, brand, model, serial number, size and operational status |
| Transactions | Income, expenses, categories, currencies and reservation links |
| Reports | Reservation summaries and financial reporting |
| Settings | Reference data, storage information and optional catalog defaults |
| Backup | SQLite snapshots with integrity verification |

## Reservations and Scheduling

Reservations include:

- Automatically generated reference numbers
- Booking date and activity date
- Start time
- Activity and participant count
- Optional instructor, boat and dive site
- Participant information
- Status and notes
- Linked cash transactions

Reservation numbers follow an annual sequence:

```text
RES-2026-000001
```

Supported statuses are:

- Pending
- Confirmed
- Completed
- Cancelled
- NoShow

The reservation list supports text search and filtering by activity date, activity and status.

The calendar provides **day and week views**, with previous/next navigation, participant totals and access to reservation details.

Cancelling a reservation retains its participant and financial history.

## Financial Tracking

The transaction module records income and expenses with:

- Date
- Amount and currency
- Category
- Optional activity and reservation links
- Optional participant and dive counts
- Description

Reservation-related payments are recorded as linked cash transactions. These links let staff review the financial entries associated with a booking.

Currencies remain independent. The application does not convert exchange rates or combine different currencies into one balance.

Initial currencies are:

- TRY
- EUR
- USD
- GBP

## Reports

The current reporting interface includes:

- Reservations by date
- Reservations by activity
- Participant counts
- Cancelled reservations
- No-shows
- Income and expenses by date
- Income and expenses by category
- Monthly financial summaries
- Activity income summaries

Available filters depend on the selected report.

Monthly summaries show all twelve months of the selected year. Financial values remain separated by currency.

## Operational Validation

Application services validate rules such as:

- Required fields and supported values
- Boat capacity
- Instructor-role eligibility
- Duplicate customer references within participant collections
- Participant records belonging to the correct reservation
- Income/expense category compatibility
- Valid currency and catalog references

Inactive reference records can remain attached to existing historical records where supported, while new selections require active records.

Boat capacity validation is implemented. This should not be interpreted as a complete boat or instructor scheduling conflict engine.

## Local Database

The default database location is:

```text
%LOCALAPPDATA%\DiveHubManagement\DiveCenter.db
```

At startup, the application:

1. Validates the storage directory.
2. Checks for pending EF Core migrations.
3. Backs up an existing database before applying pending migrations.
4. Applies migrations.
5. Enables SQLite write-ahead logging.
6. Adds initial reference data when the relevant tables are empty.

Initial reference data includes currencies, staff roles and transaction categories.

Normal startup does not automatically populate sample customers, reservations or financial transactions.

## Backup

The application includes a database backup action and a shortcut to open the backup folder.

The backup implementation:

- Uses SQLite's online backup API
- Captures committed data, including WAL content
- Checks database integrity before publishing the backup
- Produces a standalone database file
- Prevents overwriting the live database or its journal files
- Remembers the selected backup folder

Backup creation is implemented; an in-application restore workflow is not provided.

## Architecture

The application uses an **MVVM-based WPF architecture** with dependency injection.

| Component | Responsibility |
|---|---|
| Models | Entities, relationships, enums and validation attributes |
| Data | EF Core DbContext, migrations and initialization |
| Services | Business rules, queries, persistence, reporting and backup |
| ViewModels | Screen state, commands, filtering and navigation |
| Views | XAML screens and dialogs |
| Commands | Reusable synchronous and asynchronous commands |
| Helpers | Storage paths, configuration, validation support and error messages |
| Resources | Shared colors, typography, controls and branding |
| Diagnostics | Acceptance checks and persistence verification |

Database access uses `IDbContextFactory<DiveCenterDbContext>` and asynchronous EF Core operations.

The application also provides reusable catalog screens and metadata-driven editing components to reduce repeated form and grid code.

## Technology Stack

| Area | Technology |
|---|---|
| Language | C# |
| Platform | .NET 10 |
| Desktop Interface | WPF / XAML |
| Presentation Structure | MVVM |
| Data Access | Entity Framework Core |
| Database | SQLite |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| Automation | PowerShell |
| Target Framework | `net10.0-windows` |

## Getting Started

### Requirements

- Windows
- .NET SDK **10.0.300** or a compatible patch in that feature band, according to `global.json`
- Git

### Clone and Build

```powershell
git clone https://github.com/hakanberat/Desktop-Dive-Center-Management-System.git
cd Desktop-Dive-Center-Management-System

dotnet restore DiveCenterManager.sln
dotnet build DiveCenterManager.sln
```

### Run

```powershell
dotnet run --project .\DiveCenterManager\DiveCenterManager.csproj
```

The application initializes its local database automatically.

### Try the Sample Workspace

```powershell
dotnet run --project .\DiveCenterManager\DiveCenterManager.csproj -- --sample-data
```

Sample mode uses a separate workspace:

```text
%LOCALAPPDATA%\DiveHubManagement\Sample
```

The window title identifies the sample workspace. Sample mode is prevented from targeting the default live data directory.

### Use a Custom Data Directory

```powershell
dotnet run --project .\DiveCenterManager\DiveCenterManager.csproj -- --data-dir "C:\DiveHubData"
```

The selected directory must be writable. The database file is created inside it.

## Verification

The repository includes an application-level acceptance runner and PowerShell verification script.

Run the baseline verification on Windows:

```powershell
.\scripts\Verify.ps1
```

The script:

1. Builds the solution in Release mode.
2. Creates an isolated directory under `artifacts/`.
3. Runs the application's baseline acceptance checks.
4. Restarts the runner to verify persisted data.
5. Reports results from generated JSON files.

Additional diagnostic modes are implemented for focused verification phases and startup checks. The baseline script does not automatically run every diagnostic mode.

Diagnostics require an explicit isolated data directory and reject the default live workspace.

These are application-specific acceptance checks rather than a separate xUnit or NUnit test project.

## Publishing

Create a framework-dependent Windows x64 build:

```powershell
.\scripts\Publish.ps1
```

Create a self-contained Windows x64 build:

```powershell
.\scripts\Publish.ps1 -SelfContained
```

The script publishes to:

```text
artifacts\DiveHubManagement
```

The executable is:

```text
DiveCenterManager.exe
```

Framework-dependent deployment requires a compatible .NET Desktop Runtime on the destination computer. Self-contained deployment includes the runtime.

## Additional Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Validation](docs/VALIDATION.md)
- [Branding](docs/BRANDING.md)

## Current Scope

The current interface focuses on internal reservations, scheduling, reference data and cash tracking.

The repository also retains customer, dive-operation and logbook models and services from earlier development stages. These are not exposed as standalone modules in the current main navigation.

The application does not currently provide:

- Public online booking
- Online payment processing
- Cloud synchronization
- A hosted API or mobile client
- Application user accounts and role-based login

## Author

**Hakan Berat Demircan**

Software developer, PADI IDC Staff Instructor and former dive center co-owner.

This project demonstrates Windows desktop development, MVVM, relational data modeling, EF Core migrations, local persistence, business validation, reporting and database backup.

[GitHub Profile](https://github.com/hakanberat)
