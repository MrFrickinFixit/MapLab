# Map Lab Changelog

## 1.0.3.7-beta - 2026-09-08

### Added

- Added a Learn Apply table that accepts signed VE percentage corrections and transfers them to the Fueling table, regardless of whether Fueling is displayed as VE% or estimated lb/hr.
- Added optional smoothing and clearing choices for Learn Apply transfers, with one Fueling Undo step for the complete operation.
- Added interactive 3D Raise, Lower, Smooth, and Flatten sculpting plus flatten/smooth paths between two fixed points.
- Added focused 3D workspaces: select one solid 2D rectangle of at least 2 by 2 cells, then open 3D View to edit only that cropped surface until the viewer closes.
- Added transition-ring selection, Smooth to Surroundings, directional smoothing, and advanced smoothing algorithms suited to narrow wrinkles, spikes, edges, and broad contours.
- Added an independent MAP unit control for the Ignition Timing table.

### Changed

- Reworked the VE Setup wizard around engine, injector, pressure, MAP sensor, and VE-target inputs used by its calculations.
- Corrected estimated fuel-flow calculations and clarified total lb/hr, per-injector flow, capacity, and duty-cycle preview values.
- Made optional final VE Setup smoothing equivalent to Smooth Rows followed by Smooth Columns across the generated map.
- Replaced the standalone Interpolate command with selection-aware Auto-populate for rows, columns, rectangles, and axis ranges.
- Improved compact table sizing, fit-to-window behavior, manual zoom, corner-drag resizing, visual alignment, and recent-file recovery.
- Preserved full stored precision independently from display formatting throughout paste, edit, smoothing, autosave, and export workflows.

### Performance

- Full Fueling and Timing matrix pastes are validated before mutation, applied in a batch, and committed as one atomic Undo step.
- Reduced redundant cell styling, tooltip creation, selection redraws, and Learn Apply heat-map allocations during large table updates.
- In local 64 by 64 performance checks, Learn Apply paste time improved by about 32%, Learn-to-Fuel transfer redraws improved by roughly 35-39%, and full-table selection improved by about 29%.

### Installer

- Updated the WiX MSI and Inno Setup defaults to `1.0.3.7`, producing `MapLab-1.0.3.7-beta-win-x64.msi` and `MapLab-1.0.3.7-beta-win-x64-setup.exe`.
- Retained current-user/all-users installation, destination selection, optional desktop and Start menu shortcuts, and bundled license notices.
- Removed a duplicate WiX scope dialog definition and corrected MSI shortcuts to target the versioned Map Lab executable.
