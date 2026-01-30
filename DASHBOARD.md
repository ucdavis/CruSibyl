# Dashboard Implementation

## Overview
The CruSibyl dashboard provides real-time monitoring and management capabilities for application developers and maintainers, focusing on WebJob health monitoring and dependency/platform currency tracking.

## Features Implemented

### 1. WebJobs Health Monitoring
- **Environment Toggle**: Switch between Production and Test environments (Production is default)
- **App Health Cards**: Per-application overview showing:
  - Total, running, and failed WebJob counts
  - Last failure information
  - App importance/priority rating (0-1 scale)
  - Critical failure alerts (3+ failures in 24 hours)
- **Auto-refresh**: Global 60-second refresh interval
- **Visual Alerts**: DaisyUI alert components for critical failures

### 2. Failure History (30 Days)
- **Filtered Table View**: 
  - Filter by app name or job name
  - Environment-specific filtering
  - Displays up to 100 most recent failures
- **Failure Pattern Detection**:
  - Consistent vs Intermittent classification
  - Consistent: >5 failures in last 7 days
- **Sparkline Charts**: 30-day visual trend using Chart.js
- **Drill-Down Links**: Click through to detailed WebJob view

### 3. WebJob Drill-Down
- **Job Information**: Type, status, schedule, run mode
- **Recent Runs Table** (30 days, up to 50 runs):
  - Run ID, timestamps, duration, status
  - Expandable log output preview (500 chars)
  - Links to full logs when available
- **Future Action Placeholders**:
  - Rerun button (disabled, pending backend)
  - Stop button (disabled, pending backend)
  - RBAC-ready (hidden when not authorized)

### 4. Dependency & Platform Currency
- **Prioritized View**: 
  - Priority Score = (Environment Weight × App Importance × Version Delta Severity)
  - Environment Weight: Production = 1.0, Test = 0.5
  - Version Delta Severity: Major behind = 3.0, Minor behind = 2.0, Current = 1.0
- **Version Comparison**:
  - Current version vs Latest Major
  - Current version vs Latest Minor (same major)
  - Prerelease versions shown separately
- **Severity Badges**: Low, Medium, High, Critical
- **Sortable/Filterable**: DaisyUI table component

### 5. Future DevOps Pipelines
- **Collapsed Card**: Reserved space for future pipeline status
- **Placeholder UI**: Ready for integration when backend is available

## Data Architecture

### Database Schema Changes
- **App.Importance** (new field): `double` (0.0 - 1.0, default 0.5)
  - Migration: `20260113174427_AddImportanceToApp` (SqlServer)
  - Migration: `20260113174422_AddImportanceToApp` (Sqlite)

### Data Sources
- **WebJob Status**: Events table with `EventType.WebJobStatus`
- **WebJob Run Details**: `WebJobStatusPayload` JSON in Event.Payload
- **Dependencies**: Existing Dependency/PackageVersion tables
- **Apps**: Apps table with subscriptions (filters for Prod/Test)

### Environment Detection
- **Production**: `SubscriptionId` does NOT contain "test" (case-insensitive)
- **Test**: `SubscriptionId` CONTAINS "test" (case-insensitive)

## Services & Components

### DashboardService
Location: `/CruSibyl.Web/Services/DashboardService.cs`

**Methods:**
- `GetDashboardOverviewAsync(environment)`: Main dashboard with health cards and alerts
- `GetFailureHistoryAsync(environment, appFilter, jobFilter)`: 30-day failure history with sparklines
- `GetWebJobDrillDownAsync(webJobId)`: Detailed job view with recent runs
- `GetDependencyCurrencyAsync(environment)`: Prioritized dependency upgrade list

### View Models
Location: `/CruSibyl.Web/Models/Dashboard/`

- `DashboardViewModel`: Main dashboard
- `WebJobFailureHistoryViewModel`: Failure history with sparkline data
- `WebJobDrillDownViewModel`: Individual WebJob details
- `DependencyCurrencyViewModel`: Dependency currency information

### Controllers
Location: `/CruSibyl.Web/Controllers/DashboardController.cs`

**Endpoints:**
- `GET /Dashboard/Index?env={environment}`: Main dashboard
- `GET /Dashboard/FailureHistory?env={environment}&app={filter}&job={filter}`: Failure history
- `GET /Dashboard/WebJobDrillDown/{webJobId}`: Job drill-down
- `GET /Dashboard/DependencyCurrency?env={environment}`: Dependency currency

### Views
Location: `/CruSibyl.Web/Views/Dashboard/`

- `_Content.cshtml`: Main dashboard layout with health cards
- `_FailureHistory.cshtml`: Failure history table with sparklines
- `_WebJobDrillDown.cshtml`: Detailed job view with run history
- `_DependencyCurrency.cshtml`: Dependency currency table

## UI Framework

### DaisyUI Components Used
- **Cards**: App health cards, detail panels
- **Alerts**: Critical failure alerts
- **Badges**: Status, severity, environment indicators
- **Buttons**: Actions, environment toggle (join component)
- **Tables**: Failure history, run history, dependency currency
- **Stats**: WebJob counts (total, running, failed)
- **Progress**: App importance visualization
- **Radial Progress**: Priority score visualization
- **Collapse**: DevOps pipelines placeholder

### Chart.js Integration
- **Library**: Chart.js 4.4.0 (loaded from CDN)
- **Usage**: Sparkline charts for 30-day failure trends
- **Location**: Conditionally loaded via `@section ChartScripts`

### HTMX Integration
- **Navigation**: `hx-get`, `hx-target`, `hx-swap`
- **Auto-refresh**: JavaScript interval triggering HTMX refresh
- **Environment Toggle**: HTMX-powered environment switching
- **Drill-Down**: HTMX partial loading into detail panel

## Admin Features

### Apps Management
- **New Admin Page**: `/Admin/Apps`
- **Editable Fields**: Importance, IsEnabled
- **Display Fields**: Name, ResourceGroup, SubscriptionId
- **CRUD Operations**: Update only (via Htmx.Components table)

## Configuration & Setup

### Service Registration
Location: `/CruSibyl.Web/Program.cs`

```csharp
appBuilder.Services.AddScoped<IDashboardService, DashboardService>();
```

### Migration Required
Run migrations before first use:
```bash
cd CruSibyl.Core
dotnet ef database update
```

## Usage Patterns

### HTMX Navigation Flow
1. User clicks environment toggle → HTMX GET to `/Dashboard/Index?env={env}`
2. Server returns new `_Content.cshtml` with filtered data
3. HTMX swaps `#main-content` → dashboard refreshes

### Failure Investigation Flow
1. User sees critical alert on dashboard
2. Clicks "View Failure History" → HTMX loads `_FailureHistory.cshtml`
3. User filters by app/job → HTMX reloads with filters
4. User clicks "Details" → HTMX loads `_WebJobDrillDown.cshtml`
5. User expands run logs → Pure client-side toggle

### Auto-Refresh Flow
1. JavaScript interval (60s) triggers `htmx.trigger('#main-content', 'refresh')`
2. HTMX re-fetches current dashboard state
3. DOM updates with latest data
4. Event listener reattaches refresh handler after swap

## Future Enhancements

### Pending Backend Features
1. **WebJob Actions**: Rerun/Stop commands (UI placeholders exist)
2. **RBAC Integration**: Show/hide actions based on permissions
3. **DevOps Pipelines**: Pipeline status monitoring
4. **Real-time Notifications**: Toast alerts for new failures
5. **Email Alerts**: Critical failure notifications

### Potential UI Improvements
1. **Filtering**: Advanced filters (date range, severity)
2. **Sorting**: Multi-column table sorting
3. **Export**: CSV/Excel export for reports
4. **Dashboards**: Multiple dashboard views/layouts
5. **Customization**: User-specific widget preferences

## Testing Recommendations

### Manual Testing Checklist
- [ ] Dashboard loads with Production data by default
- [ ] Environment toggle switches between Prod/Test
- [ ] Critical alerts appear when 3+ failures in 24 hours
- [ ] Health cards show correct counts and importance
- [ ] Failure history filters work correctly
- [ ] Sparklines render for WebJobs with history
- [ ] Drill-down shows correct run history
- [ ] Log previews expand/collapse correctly
- [ ] Dependency currency calculates priority correctly
- [ ] Auto-refresh updates data every 60 seconds
- [ ] Apps admin page allows editing Importance field

### Performance Considerations
- Dashboard queries include `.Take()` limits
- Indexes exist on critical fields (EventType, Timestamp)
- Auto-refresh can be disabled by user if needed
- Chart.js loaded conditionally (only when needed)
- HTMX partial loading reduces full page refreshes

## Architecture Notes

### MultiSwapViewResult Pattern
Not currently used in dashboard but available for future enhancements where multiple UI components need OOB updates.

### Filter Usage
Dashboard uses standard MVC filter pipeline. Could be extended with custom filters for:
- Dashboard-specific authorization
- Telemetry/analytics tracking
- Cache headers for auto-refresh optimization

### State Management
- Environment selection: URL parameter (stateless)
- Filters: Form inputs with HTMX
- Expand/collapse: Client-side only (no server state)
- Page state: Htmx.Components page state (encrypted, session-scoped)
