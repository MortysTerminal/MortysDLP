# .NET 10 Migration - Execution Tasks

## Progress Dashboard

**Migration Status:** ?? In Progress  
**Target Framework:** .NET 10.0 (LTS)  
**Project:** MortysDLP.csproj  
**Branch:** upgrade-to-NET10

**Progress**: 7/16 tasks complete (44%) ![44%](https://progress-bar.xyz/44)

## Task List

### [?] TASK-001: Verify Prerequisites *(Completed: 2026-03-04 10:38)*
**Description:** Ensure all required tools and SDKs are installed
**References:** Plan §1.1

**Actions:**
- [?] (1) Verify .NET 10 SDK is installed on the machine
        Command: `dotnet --list-sdks`
        Expected: .NET 10.x SDK appears in the list
- [?] (2) Validate global.json compatibility (if exists)
        Check if global.json file exists and validate SDK version
- [?] (3) Verify current solution state
        Expected: Solution loads without errors in .NET 9

**Verification:**
- .NET 10 SDK is confirmed available
- No global.json conflicts
- Solution loads successfully

**Commit:** Do not commit (verification only)

---

### [?] TASK-002: Update Project Target Framework *(Completed: 2026-03-04 10:43)*
**Description:** Change target framework from net9.0-windows7.0 to net10.0-windows
**References:** Plan §2.1
**Dependencies:** TASK-001

**Actions:**
- [?] (1) Open MortysDLP.csproj file
- [?] (2) Locate `<TargetFramework>` element
- [?] (3) Change value from `net9.0-windows7.0` to `net10.0-windows`
- [?] (4) Save the file
- [?] (5) Reload the project in Visual Studio

**Verification:**
- Project file contains `<TargetFramework>net10.0-windows</TargetFramework>`
- No XML syntax errors
- Solution reloads successfully

**Commit:** Yes - "Update target framework to .NET 10"

---

### [?] TASK-003: Enable Latest C# Language Features (Optional) *(Completed: 2026-03-04 10:46)*
**Description:** Enable C# 13 features for .NET 10
**References:** Plan §2.2
**Dependencies:** TASK-002

**Actions:**
- [?] (1) Open MortysDLP.csproj file
- [?] (2) Add or update `<LangVersion>latest</LangVersion>` in PropertyGroup
- [?] (3) Save the file

**Verification:**
- Project file contains `<LangVersion>latest</LangVersion>`
- Project reloads without errors

**Commit:** Yes - "Enable C# 13 language features"

---

### [?] TASK-004: Restore NuGet Packages *(Completed: 2026-03-04 10:52)*
**Description:** Restore all NuGet packages for .NET 10
**References:** Plan §3.1
**Dependencies:** TASK-002

**Actions:**
- [?] (1) Clean solution (Build ? Clean Solution)
- [?] (2) Restore NuGet packages
        Command: `dotnet restore` or Visual Studio Restore
- [?] (3) Verify Ookii.Dialogs.Wpf v5.0.1 restores successfully

**Verification:**
- All packages restore successfully
- No version conflicts
- No security vulnerabilities reported

**Commit:** No (automatic restore)

---

### [?] TASK-005: Add Configuration Manager Bridge Package *(Completed: 2026-03-04 10:56)*
**Description:** Add System.Configuration.ConfigurationManager to maintain legacy config support
**References:** Plan §4.2
**Dependencies:** TASK-004

**Actions:**
- [?] (1) Add NuGet package System.Configuration.ConfigurationManager
        Command: `dotnet add package System.Configuration.ConfigurationManager --version 8.0.0`
        Or use Visual Studio NuGet Package Manager
- [?] (2) Verify package installation
- [?] (3) Restore packages if needed

**Verification:**
- Package appears in MortysDLP.csproj
- Package version is 8.0.0 or compatible
- No package conflicts

**Commit:** Yes - "Add System.Configuration.ConfigurationManager bridge package"

---

### [?] TASK-006: Initial Build Attempt *(Completed: 2026-03-04 11:20)*
**Description:** Perform first build after target framework change
**References:** Plan §7.1
**Dependencies:** TASK-005

**Actions:**
- [?] (1) Clean solution completely
- [?] (2) Rebuild entire solution
- [?] (3) Analyze build output:
        - Count errors (Expected: 0)
        - Count warnings (Document any)
        - Note any obsolete API warnings
- [?] (4) Document build results

**Verification:**
- Build completes (even if with warnings)
- Zero compilation errors
- All projects compile successfully

**Commit:** No (diagnostic step)

---

### [?] TASK-007: Fix Compilation Errors (If Any)
**Description:** Address any compilation errors from initial build
**References:** Plan §7.2
**Dependencies:** TASK-006
**Conditional:** Only if TASK-006 produces errors

**Actions:**
- [?] (1) Review each compilation error
- [?] (2) Categorize errors:
        - Missing namespaces
        - API signature changes
        - Type conversion issues
        - Obsolete API usage
- [?] (3) Fix errors file by file
- [?] (4) Test each fix with incremental builds
- [?] (5) Document any workarounds needed

**Verification:**
- Build completes with 0 errors
- All files compile successfully
- Only acceptable warnings remain

**Commit:** Yes - "Fix compilation errors for .NET 10"

---

### [?] TASK-008: Review System.Uri Behavioral Changes *(Completed: 2026-03-04 11:24)*
**Description:** Verify and test System.Uri usage for behavioral changes
**References:** Plan §5.2
**Dependencies:** TASK-007

**Actions:**
- [?] (1) Locate all System.Uri instantiations in codebase
        Files to check:
        - App.xaml.cs
        - Services/YtDlpUpdateService.cs
        - Services/UpdateService.cs
        - Views/MainWindow.xaml.cs
- [?] (2) Review URI construction patterns
- [?] (3) Document any URIs that might be affected
- [?] (4) Prepare test cases for URI-related functionality

**Verification:**
- All URI usages identified
- Patterns documented
- Test plan ready

**Commit:** No (analysis only)

---

### [?] TASK-009: Test Application Startup
**Description:** Verify application launches correctly on .NET 10
**References:** Plan §8.2.1
**Dependencies:** TASK-007

**Actions:**
- [?] (1) Build solution in Debug mode
- [ ] (2) Start application (F5 or Ctrl+F5)
- [ ] (3) Verify:
        - Application launches without exceptions
        - Main window appears correctly
        - UI renders properly
        - No error dialogs appear
- [ ] (4) Check Output window for warnings or errors
- [ ] (5) Verify configuration loads (check Settings)

**Verification:**
- Application starts successfully
- UI is fully functional
- Configuration loads correctly
- No runtime errors in Output window

**Commit:** No (testing phase)

---

### [ ] TASK-010: Test Core Functionality
**Description:** Validate main application features work on .NET 10
**References:** Plan §8.2.2
**Dependencies:** TASK-009

**Actions:**
- [ ] (1) Test download operations:
        - Start a download
        - Verify progress tracking
        - Verify download completion
- [ ] (2) Test file conversion features (if applicable)
- [ ] (3) Test UI interactions:
        - Button clicks
        - Text input
        - Checkbox toggles
        - ComboBox selections
        - MessageBox displays
- [ ] (4) Test external tool integration:
        - yt-dlp update check
        - Tool downloads
- [ ] (5) Test settings:
        - Load existing settings
        - Modify settings
        - Save settings
        - Verify persistence

**Verification:**
- All downloads complete successfully
- UI elements respond correctly
- Settings persist properly
- No runtime exceptions
- No behavioral regressions

**Commit:** No (testing phase)

---

### [ ] TASK-011: Test Threading and Dispatcher
**Description:** Verify async operations and UI threading work correctly
**References:** Plan §8.2.6
**Dependencies:** TASK-010

**Actions:**
- [ ] (1) Test long-running operations:
        - Verify UI remains responsive
        - Check progress indicators update
        - Ensure cancel operations work
- [ ] (2) Test Dispatcher.Invoke calls:
        - UI updates from background threads
        - No cross-thread exceptions
- [ ] (3) Monitor for deadlocks or freezes
- [ ] (4) Test concurrent operations (if supported)

**Verification:**
- UI remains responsive during operations
- No cross-thread access violations
- Background tasks complete successfully
- UI updates properly from worker threads

**Commit:** No (testing phase)

---

### [ ] TASK-012: Performance Validation
**Description:** Ensure performance is acceptable or improved on .NET 10
**References:** Plan §9.1, §11.1
**Dependencies:** TASK-011

**Actions:**
- [ ] (1) Measure application startup time
- [ ] (2) Test download speed with sample files
- [ ] (3) Monitor memory usage during operations
- [ ] (4) Check UI responsiveness under load
- [ ] (5) Compare with .NET 9 behavior (if baseline exists)
- [ ] (6) Document any performance improvements

**Verification:**
- Startup time is acceptable
- Download performance is good
- Memory usage is reasonable
- No performance regressions detected

**Commit:** No (validation phase)

---

### [ ] TASK-013: Apply .NET 10 Optimizations (Optional)
**Description:** Implement .NET 10-specific enhancements
**References:** Plan §9
**Dependencies:** TASK-012
**Optional:** Can be done in future phase

**Actions:**
- [ ] (1) Review async methods for ConfigureAwait optimization
        Files: UpdateService.cs, YtDlpUpdateService.cs, DownloadHistoryService.cs
- [ ] (2) Consider collection expressions (C# 13) where beneficial
- [ ] (3) Review string interpolation usage (already optimized in .NET 10)
- [ ] (4) Test changes incrementally

**Verification:**
- Optimizations applied successfully
- Build still succeeds
- Tests still pass
- Performance improves or stays same

**Commit:** Yes - "Apply .NET 10 performance optimizations"

---

### [ ] TASK-014: Update Documentation
**Description:** Update project documentation for .NET 10
**References:** Plan §10.2
**Dependencies:** TASK-012

**Actions:**
- [ ] (1) Open README.md
- [ ] (2) Update .NET version requirement to .NET 10
- [ ] (3) Update development environment requirements
- [ ] (4) Update build instructions if needed
- [ ] (5) Add migration notes (optional)

**Verification:**
- README.md accurately reflects .NET 10 requirement
- Instructions are clear and correct
- No broken links

**Commit:** Yes - "Update documentation for .NET 10"

---

### [ ] TASK-015: Final Build and Verification
**Description:** Perform final clean build and verification
**References:** Plan §12
**Dependencies:** TASK-014

**Actions:**
- [ ] (1) Clean entire solution
- [ ] (2) Delete bin/ and obj/ folders
- [ ] (3) Restore NuGet packages
- [ ] (4) Rebuild solution in Release mode
- [ ] (5) Verify 0 errors, acceptable warnings
- [ ] (6) Test Release build execution
- [ ] (7) Create execution summary

**Verification:**
- Clean Release build succeeds
- Application runs correctly in Release mode
- All success criteria met (Plan §12)
- Ready for deployment

**Commit:** Yes - "Final verification for .NET 10 migration complete"

---

### [ ] TASK-016: Create Execution Log Summary
**Description:** Document migration execution results
**References:** Plan §10.3
**Dependencies:** TASK-015

**Actions:**
- [ ] (1) Create execution_log.md with migration summary
- [ ] (2) Document all tasks completed
- [ ] (3) List any issues encountered and resolutions
- [ ] (4) Record build results
- [ ] (5) Record test results
- [ ] (6) Note performance observations
- [ ] (7) Confirm all success criteria met

**Verification:**
- execution_log.md exists and is complete
- All important details documented
- Clear migration status

**Commit:** Yes - "Add execution log for .NET 10 migration"

---

## Success Criteria Checklist

**Must-Have (Blocking):**
- [ ] Project builds with 0 errors
- [ ] Application launches successfully
- [ ] Core functionality (download/convert) works
- [ ] Settings load and save correctly
- [ ] No crashes during normal operations

**Should-Have (Important):**
- [ ] All warnings addressed or documented
- [ ] Performance is same or better than .NET 9
- [ ] All UI elements function correctly
- [ ] External tool integration works

**Nice-to-Have (Enhancement):**
- [ ] C# 13 features utilized
- [ ] Performance optimizations applied
- [ ] Documentation updated
- [ ] Benchmarks show improvements

---

## Notes

- **Rollback:** If migration fails, switch back to `master` branch
- **Testing:** Test thoroughly after each major task
- **Documentation:** Keep notes of any issues encountered
- **Timeline:** Estimated 3-4 hours for complete migration

---

**Tasks File Version:** 1.0  
**Created:** Based on plan.md  
**Total Tasks:** 16  
**Status:** Ready for execution
