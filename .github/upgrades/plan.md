# .NET 10 Migration Plan for MortysDLP

## Executive Summary

This plan outlines the step-by-step migration of the MortysDLP WPF application from .NET 9.0 to .NET 10.0 (LTS). The migration is classified as **Medium Complexity** with an estimated impact on ~1,674 lines of code (35.6% of the codebase).

### Key Highlights

- **Target Framework:** net10.0-windows
- **NuGet Packages:** All compatible (no upgrades needed) ?
- **Security:** No vulnerabilities detected ?
- **Main Challenges:** WPF API binary compatibility, Legacy Configuration System
- **Estimated Duration:** 2-4 hours
- **Risk Level:** Medium (extensive testing required)

---

## 1. Pre-Migration Preparation

### 1.1 Verify Prerequisites

**Objective:** Ensure all required tools and SDKs are installed

**Actions:**
- Verify .NET 10 SDK is installed on the machine
- Check Visual Studio 2022 version (17.12 or later recommended)
- Validate global.json compatibility (if exists)
- Create backup branch (already done: `upgrade-to-NET10`)

**Verification:**
- Run `dotnet --list-sdks` and confirm .NET 10 SDK is present
- Solution loads without errors in current state

---

## 2. Project Configuration Update

### 2.1 Update Target Framework

**Objective:** Change the project target framework from net9.0-windows7.0 to net10.0-windows

**File:** `MortysDLP.csproj`

**Changes:**
```xml
<TargetFramework>net10.0-windows</TargetFramework>
```

**Rationale:** .NET 10 is the new LTS version with improved performance, security updates, and will be supported until November 2027. The `-windows` suffix ensures WPF and Windows Forms APIs remain available.

**Verification:**
- Project file saved successfully
- No XML syntax errors
- Solution reload succeeds

### 2.2 Update Language Version (Optional Enhancement)

**Objective:** Enable latest C# language features available in .NET 10

**File:** `MortysDLP.csproj`

**Changes:**
```xml
<LangVersion>latest</LangVersion>
```

**Rationale:** .NET 10 supports C# 13, which includes:
- Improved pattern matching
- Primary constructors for all classes (not just records)
- Collection expressions enhancements
- Better params support

**Verification:**
- Language features compile without warnings

---

## 3. NuGet Package Management

### 3.1 Restore and Verify Packages

**Objective:** Ensure all NuGet packages are compatible with .NET 10

**Current Package:**
- Ookii.Dialogs.Wpf v5.0.1 ? ? Already compatible

**Actions:**
1. Restore NuGet packages
2. Verify no package conflicts
3. Check for any deprecation warnings

**Verification:**
- All packages restore successfully
- No version conflicts reported
- No security vulnerabilities

**Note:** No package upgrades are required for this migration. If future updates are needed, consider checking for newer versions that leverage .NET 10 features.

---

## 4. Configuration System Migration

### 4.1 Legacy Configuration Assessment

**Objective:** Handle the 70 source-incompatible issues related to `System.Configuration`

**Current State:** 
- Project uses legacy `app.config` with `Properties.Settings.Default`
- 64 instances of `ApplicationSettingsBase.Item[String]` access
- Configuration used in: App.xaml.cs, Services, Views

**Migration Options:**

#### Option A: Bridge Approach (Recommended for Quick Migration)
**Add NuGet Package:**
```xml
<PackageReference Include="System.Configuration.ConfigurationManager" Version="8.0.0" />
```

**Rationale:** 
- Minimal code changes required
- Maintains existing app.config structure
- Quick migration path
- Compatible with .NET 10

**Verification:**
- Package restores successfully
- Build succeeds with 0 errors
- Settings are accessible at runtime

#### Option B: Modern Configuration (Future Enhancement)
**Long-term Goal:** Migrate to `Microsoft.Extensions.Configuration`

**Benefits:**
- Modern, flexible configuration model
- JSON-based appsettings
- Environment variable support
- Dependency injection ready

**Implementation:** This can be done in a future phase after successful .NET 10 migration.

**Decision:** Proceed with **Option A** for this migration to minimize risk and complexity.

### 4.2 Apply Configuration Bridge

**Actions:**
1. Add `System.Configuration.ConfigurationManager` NuGet package (v8.0.0)
2. Verify all `Properties.Settings.Default` calls still work
3. Test configuration read/write operations

**Files Potentially Affected:**
- App.xaml.cs
- Properties/Settings.settings
- Properties/Settings.Designer.cs
- Various service and view files

**Verification:**
- Build succeeds
- Application launches
- Settings load correctly
- Settings save correctly

---

## 5. WPF API Compatibility

### 5.1 Binary Compatibility Resolution

**Objective:** Resolve 1,573 binary-incompatible WPF API references

**Context:** 
The "binary incompatible" warnings are expected when targeting a new framework version. These APIs are still available in .NET 10 but need recompilation. Most WPF APIs are source-compatible, meaning:
- No code changes required for standard WPF usage
- Recompilation will resolve binary compatibility
- Runtime behavior remains consistent

**Most Affected Controls:**
- TextBox (102 instances)
- TextBlock (95 instances)
- Button (85 instances)
- CheckBox (83 instances)
- ComboBox (52 instances)
- MessageBox (20 instances)
- Dispatcher threading (50 instances)

**Strategy:**
1. **Initial Build:** Attempt full build after target framework change
2. **Identify Real Errors:** Separate actual compilation errors from warnings
3. **Fix Compilation Errors:** Address any true breaking changes
4. **Test Behavior Changes:** Focus on the 27 behavioral changes identified

**Expected Outcome:** Most binary incompatibilities will resolve automatically during recompilation.

### 5.2 Address Behavioral Changes

**Objective:** Handle 27 behavioral changes, primarily in `System.Uri`

**Key Changes:**
- `System.Uri` constructor behavior (8 instances)
- URI parsing rules may differ slightly

**Files to Review:**
- App.xaml.cs
- Services/YtDlpUpdateService.cs
- Services/UpdateService.cs
- Any code using `System.Uri`

**Actions:**
1. Locate all `System.Uri` instantiations
2. Review URI construction patterns
3. Test with actual URIs used in the application
4. Verify download operations still work

**Verification:**
- All URI-related functionality works correctly
- Downloads succeed
- No runtime URI parsing errors

---

## 6. Windows Forms Compatibility

### 6.1 DialogResult Usage

**Objective:** Handle 7 instances of `System.Windows.Forms.DialogResult`

**Context:** 
Limited Windows Forms usage, primarily for dialog results with `Ookii.Dialogs.Wpf`

**Files Affected:**
- Views/ConvertWindow.xaml.cs
- Views/MainWindow.xaml.cs
- Services/YtDlpUpdateService.cs

**Strategy:**
- Verify `Ookii.Dialogs.Wpf` v5.0.1 works correctly with .NET 10
- Test folder browser dialogs
- Ensure dialog results are handled properly

**Verification:**
- Dialogs open correctly
- User selections are captured
- No runtime exceptions

---

## 7. Build and Compilation

### 7.1 Initial Build

**Objective:** Perform first build after target framework change

**Actions:**
1. Clean solution
2. Restore NuGet packages
3. Build solution
4. Categorize any errors:
   - True breaking changes (must fix)
   - Warnings (evaluate severity)
   - Info messages (document only)

**Expected Result:** Build should succeed with possible warnings but zero errors.

### 7.2 Resolve Compilation Errors

**Objective:** Fix any actual compilation errors

**Common Issues to Watch For:**
- Missing namespace imports
- API signature changes
- Obsolete API usage
- Type conversion issues

**Approach:**
- Address errors file by file
- Use IDE quick fixes where applicable
- Document any workarounds needed
- Test each fix incrementally

**Verification:**
- Build completes with 0 errors
- All projects compile successfully
- Only acceptable warnings remain

---

## 8. Testing and Validation

### 8.1 Unit Testing Preparation

**Objective:** Identify test projects and prepare test execution

**Current State:** Need to discover if test projects exist

**Actions:**
1. Scan solution for test projects
2. Update test project target frameworks if needed
3. Restore test packages

**Verification:**
- All test projects identified
- Test infrastructure ready

### 8.2 Functional Testing

**Objective:** Validate application behavior on .NET 10

**Test Scenarios:**
1. **Application Startup**
   - Application launches without errors
   - UI renders correctly
   - Configuration loads properly

2. **Core Functionality**
   - Download operations work
   - File conversions succeed
   - Progress tracking functions
   - Error handling works

3. **UI Interactions**
   - All buttons respond
   - Text input works
   - Checkboxes toggle
   - Combo boxes populate
   - MessageBoxes display correctly

4. **Settings Management**
   - Settings load from app.config
   - Settings save correctly
   - User preferences persist

5. **External Tool Integration**
   - yt-dlp update service works
   - External downloads succeed
   - File system operations function

6. **Threading and Dispatcher**
   - UI remains responsive during operations
   - Background tasks execute correctly
   - UI updates from worker threads work

**Verification:**
- All test scenarios pass
- No runtime exceptions
- No behavioral regressions
- Performance is acceptable or improved

---

## 9. Performance and .NET 10 Optimizations

### 9.1 Leverage .NET 10 Improvements

**Objective:** Apply .NET 10-specific enhancements for better performance

**Potential Optimizations:**

#### 9.1.1 Async/Await Improvements
.NET 10 includes performance improvements to async state machines.

**Review Areas:**
- Async methods in services (UpdateService, YtDlpUpdateService, DownloadHistoryService)
- Consider using `ConfigureAwait(false)` in service layer methods

**Example Enhancement:**
```csharp
// Before
var result = await httpClient.GetAsync(url);

// After (if not needing UI context)
var result = await httpClient.GetAsync(url).ConfigureAwait(false);
```

#### 9.1.2 Collection Expressions (C# 13)
Use modern collection syntax where applicable.

**Example:**
```csharp
// Before
var items = new List<string> { "item1", "item2", "item3" };

// After (C# 13)
var items = ["item1", "item2", "item3"];
```

#### 9.1.3 String Interpolation Improvements
.NET 10 has optimized string interpolation.

**Review:** Already using string interpolation; no changes needed, but will benefit from runtime improvements.

#### 9.1.4 LINQ Performance
.NET 10 includes LINQ performance enhancements, especially for:
- `.Where()` followed by `.Select()`
- `.Any()` and `.Count()` operations

**Review:** Existing LINQ queries will automatically benefit.

### 9.2 File I/O Optimizations

**Objective:** Leverage improved file I/O in .NET 10

**Areas to Review:**
- File download operations
- History service file operations
- Log file writing

**Considerations:**
- Use `File.ReadAllTextAsync()` / `WriteAllTextAsync()` consistently
- Consider `MemoryStream` optimizations for in-memory operations

### 9.3 HTTP Client Improvements

**Objective:** Benefit from HttpClient enhancements in .NET 10

**Current Usage:**
- UpdateService.cs
- YtDlpUpdateService.cs
- ToolDownloadHelper.cs

**Enhancements:**
- HTTP/3 support (if applicable)
- Improved connection pooling
- Better timeout handling

**Note:** These are mostly automatic runtime improvements; code changes are optional.

---

## 10. Source Control and Documentation

### 10.1 Commit Strategy

**Approach:** Atomic commits for each major change

**Commit Sequence:**
1. "Update target framework to .NET 10"
2. "Add System.Configuration.ConfigurationManager package"
3. "Fix compilation errors (if any)"
4. "Apply .NET 10 optimizations"
5. "Update documentation"

**Verification:**
- Each commit builds successfully
- Clear commit messages
- Easy to revert if needed

### 10.2 Update Documentation

**Files to Update:**
- README.md: Update .NET version requirement
- Any developer setup guides
- Build instructions

**Content:**
```markdown
## Requirements

- .NET 10 SDK or later
- Visual Studio 2022 (17.12+) or JetBrains Rider
- Windows 10/11
```

### 10.3 Execution Log

**Create:** `execution_log.md`

**Purpose:** Document everything that happens during migration execution

**Content:**
- Tasks completed
- Issues encountered and resolutions
- Build results
- Test results
- Performance observations
- Final verification status

---

## 11. Post-Migration Tasks

### 11.1 Performance Benchmarking

**Objective:** Measure performance improvements

**Metrics to Track:**
- Application startup time
- Download operation speed
- UI responsiveness
- Memory usage

**Comparison:** .NET 9 vs .NET 10

### 11.2 Deployment Preparation

**Objective:** Ensure deployment is ready for .NET 10

**Actions:**
- Update deployment scripts (if any)
- Update installer requirements
- Test on clean Windows machine
- Verify .NET 10 Runtime deployment

### 11.3 CI/CD Pipeline Updates

**Objective:** Update build pipelines for .NET 10

**Actions:**
- Update GitHub Actions workflows (if exist)
- Update build agents to support .NET 10
- Update package restore processes

---

## 12. Rollback Plan

### 12.1 Rollback Strategy

**If Migration Fails:**
1. Switch back to `master` branch
2. Review execution_log.md for failure points
3. Address blockers
4. Retry migration

**Rollback Command:**
```bash
git checkout master
```

**Note:** All changes are on the `upgrade-to-NET10` branch, so rollback is safe and simple.

---

## Risk Assessment

### High Priority Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Configuration system breaks | Low | High | Use ConfigurationManager bridge package |
| WPF runtime behavior changes | Medium | Medium | Extensive functional testing |
| Third-party library incompatibility | Low | Medium | Already verified Ookii.Dialogs compatibility |
| Performance regression | Low | Low | Benchmark before/after |

### Risk Mitigation Strategy

- **Incremental approach:** Make changes step by step
- **Continuous testing:** Test after each major change
- **Clear rollback path:** Keep master branch clean
- **Documentation:** Track all changes in execution log

---

## Success Criteria

### Must-Have (Blocking)
- ? Project builds with 0 errors
- ? Application launches successfully
- ? Core functionality (download/convert) works
- ? Settings load and save correctly
- ? No crashes during normal operations

### Should-Have (Important)
- ? All warnings addressed or documented
- ? Performance is same or better than .NET 9
- ? All UI elements function correctly
- ? External tool integration works

### Nice-to-Have (Enhancement)
- ? C# 13 features utilized
- ? Performance optimizations applied
- ? Modern configuration system migrated (future)
- ? Benchmarks show improvements

---

## Timeline Estimate

| Phase | Estimated Duration | Complexity |
|-------|-------------------|-----------|
| 1-2: Project Configuration | 15 minutes | Low |
| 3: Package Management | 10 minutes | Low |
| 4: Configuration Migration | 30 minutes | Medium |
| 5-6: API Compatibility | 45 minutes | Medium |
| 7: Build Resolution | 30 minutes | Medium |
| 8: Testing | 60 minutes | High |
| 9: Optimizations | 30 minutes | Low |
| 10-11: Documentation | 20 minutes | Low |

**Total Estimated Time:** 3-4 hours

**Buffer for Issues:** +1 hour

**Total with Buffer:** 4-5 hours

---

## Conclusion

This migration plan provides a comprehensive, step-by-step approach to upgrading MortysDLP from .NET 9 to .NET 10. The plan prioritizes safety, testability, and incremental progress while taking advantage of .NET 10's LTS support and performance improvements.

The migration is classified as **Medium Complexity** due to the number of API compatibility issues, but most of these will resolve automatically during recompilation. The use of the configuration bridge package minimizes code changes, and thorough testing will ensure no regressions.

By following this plan systematically and documenting progress in the execution log, the migration should complete successfully within 4-5 hours.

---

**Plan Version:** 1.0  
**Created:** Based on assessment.md analysis  
**Target:** .NET 10.0 (LTS)  
**Status:** Ready for Execution
