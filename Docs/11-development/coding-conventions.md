# Coding Conventions

Rules for new and changed C# code in this repository. They were derived from the code as it is (about 550 files) and then decided where the code disagreed with itself. Existing code is **not** reformatted in bulk; align a file with these rules when you touch it for another reason, and keep the diff focused.

The machine-readable subset lives in `.editorconfig` at the repository root (IDE suggestions and warnings, no build enforcement). Rules that no IDE option expresses are marked *(manual)*.

Reference files are cited so that a rule can be checked against real code.

## 1. Project settings

- Every project: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `LangVersion` default. Do not add `#nullable disable` or `#pragma warning disable` without a comment that says why.
- Libraries multi-target `net10.0;net9.0;net8.0`. Test projects and `ConfigWizard.Web` are `net10.0` only. Use APIs available on .NET 8 in library code, or guard them with `#if NET9_0_OR_GREATER`.
- Package versions go into `Directory.Packages.props` (central package management).
- Shared `using` directives go into a `GlobalUsings*.cs` file (`Raycoon.RayMigrator.ConfigWizard.Core/GlobalUsings.Shared.cs`).
- XML comments in csproj files are English, like everything else.

## 2. Files and namespaces

- One top-level type per file, file name equals type name. Exceptions that exist: `Shared/Exceptions/CustomExceptions.cs`, `Core/Configuration/Options/RayMigratorOptions.cs`, `Services.Abstractions/Models/Results.cs`. Do not add new multi-type files.
- File-scoped namespaces only (`namespace Raycoon.RayMigrator.Core.Logging;`). All 538 namespaced files already do this.
- Namespace equals folder path below the project root. `Tests.Unit` is flat by design and uses `Raycoon.RayMigrator.Tests.Unit`.
- `using` order: `System.*`, then `Microsoft.*`, then `Raycoon.*`, then third party, each block sorted alphabetically. *(partly manual: `.editorconfig` only sorts `System` first)*
- Encoding UTF-8. Indent with four spaces, never tabs. Files end with a newline.

## 3. Naming

| Element | Style | Example |
|---|---|---|
| Types, methods, properties, events | PascalCase | `TemplateExecutor`, `MigrateUpAsync` |
| Interfaces | `I` + PascalCase | `IDal`, `IValidationRule` |
| Private and protected instance fields | `_camelCase` | `_logger`, `_ctxAccessor` (`Services/MigrationService.cs`) |
| Private static readonly fields | PascalCase | `Translations`, `CultureMap` (`ConfigWizard.Web/Services/LocalizationService.cs`). Not `s_` and not `_`. |
| Constants (`const`) | PascalCase | `AbortMessage`, `EnvironmentVariablePrefix`. Exception: the rule catalog `Validation/RuleIds.cs` keeps `RULE_3_8` because the IDs are data. |
| Local variables, parameters, local consts | camelCase | `productAlias` |
| Protected fields in test base classes | PascalCase | `protected readonly SqlServerFixture Fixture;` |
| Task / ValueTask returning methods | suffix `Async`, also private ones | `ExecuteMigrationFileAsync`. Existing members without it (`IDal.IsConnectionValid`, `*AsyncInternal`) stay as they are; renaming `IDal` is a breaking NuGet change. |
| Enums | PascalCase members, explicit values in steps of 10, first member `Undefined = 0` | `Core/Configuration/Enums/MigrationErrorAction.cs` |
| Template types | `Area_Entity_Action`, identical to the SQL file name | `Repository_MigrationRun_Insert` (`Core/Templates/TemplateType.cs`) |
| Test classes | `<Subject>Tests`; engine tests `<Db><Feature>Tests` | `RetryHelperTests`, `SqlServerBaselineTests`, `PostgreSqlBaselineTests` |
| Unit test files | `P<0-3>_<Subject>Tests.cs`, class without the prefix | `P1_RetryHelperTests.cs` (see `Docs/10-testing/unit-tests.md`) |
| Test methods | `Subject_Scenario_ExpectedResult`, three parts | `Baseline_ToRelease2_MarksAllFilesAsMigrated` |

## 4. Formatting

- Allman braces: every `{` on its own line, including lambdas that span lines and object initializers.
- Braces always, with one exception: a guard that fits on one line may stay on one line without braces.

  ```csharp
  if (request == null) return;
  if (product == null) throw new ConfigurationValidationException($"Product alias [{alias}] not found.");

  if (result.Success)
  {
      counter++;
  }
  ```

- Line length: aim for 140 characters. Structured log templates, URLs and long string literals may exceed it. Do not break a log message template across lines just to fit.
- Parameter lists with more than three parameters, or that do not fit on one line, wrap one parameter per line (`MigrationService` constructor).
- Expression-bodied members are fine for one-line getters and one-line methods. Multi-line bodies use a block.
- `#region` is allowed for grouping in large files; the closing tag repeats the name (`#endregion MigrateUp`). Box comments (`// ── Product-level ──`) are equally fine. Splitting the file is usually better than either.
- One blank line between members. No double blank lines inside a type.
- No `this.` unless required to disambiguate.

## 5. Types and members

- Visibility: new classes are `internal sealed`. Make a type `public` only when it belongs to a NuGet contract (`Core`, `Shared`, `Services.Abstractions`, `Database.Common`, `Validation`, `Testing`) or another assembly needs it. Drop `sealed` only when inheritance is intended. Tests reach internals through the existing `InternalsVisibleTo` entries. Reference: `Validation/Rules/IValidationRule.cs` ("Implementations are `internal sealed`").
- Immutable value objects and results are `sealed record` types (`Validation/Models/ValidationIssue.cs`). Options classes bound from configuration stay mutable classes with `{ get; set; }` and nullable properties (`Core/Configuration/Options/RayMigratorOptions.cs`). Service result DTOs (`Services.Abstractions/Models/Results.cs`) keep their current shape; new result types are records.
- Classes with dependencies use a classic constructor that assigns `readonly` fields (`_logger = logger;`). No primary constructors on classes; positional records are the only primary-constructor form used.
- Static classes for stateless helpers (`ConfigWizard.Core/Services/*`, `RuleCatalog`).
- `required` and `init` are welcome on models that must be fully initialised (`Core/MigrationStateSnapshot.cs`, `Validation/Models/TargetInput.cs`).
- Object creation: target-typed `new()` when the type is on the left, collection expressions for lists and arrays.

  ```csharp
  List<string> messages = [];
  string[] separators = [";", "GO"];
  DalParameterList parameters = new();
  var options = new RayMigratorOptions();          // var + explicit new when the type is only on the right
  ```

- `var` when the type is apparent from the right-hand side (`new`, cast, `as`, LINQ, obvious factory). Explicit type when it is not:

  ```csharp
  var executor = new TemplateExecutor(cache, logger, accessor);
  var names = products.Select(p => p.Alias).ToList();
  int successfulMigrations = 0;
  MigrationOperationResult result = await service.MigrateUpAsync(request);
  ```

- Argument guards: `ArgumentNullException.ThrowIfNull(request);` and `ArgumentException.ThrowIfNullOrWhiteSpace(alias);` at the top of public methods and constructors. Not `if (x == null) throw new ArgumentNullException(nameof(x));` in new code.
- Enum options in configuration are `string?` properties validated by `[RayEnum(typeof(X))]` and resolved through `ParsedEnumOption<T>`; follow the existing pattern in `RayMigratorOptions.cs` (property, private `ParsedEnumOption<T>` field, `XEnum` getter).

## 6. Null handling *(manual)*

- Compare with the operators: `x == null` and `x != null`. Do not use `is null` / `is not null` in new code. Property patterns for other purposes (`x is { Success: true }`) are fine.
- Prefer `?.`, `??` and `??=` over explicit branches.
- The null-forgiving operator `!` needs a reason. Use it after a validation step that guarantees the value (`Repository!.DatabaseType!` right after `ValidateOnStart`), never to silence a warning you have not understood.

## 7. Async

- Every Task-returning method ends in `Async`, is declared `async` unless it only forwards a task, and returns `Task`/`Task<T>` (`ValueTask` only on hot paths that measurably benefit).
- New public async methods in `Services`, `Pipeline` and `Database.*` take `CancellationToken cancellationToken = default` as the last parameter and pass it on. Existing interfaces (`IDal`, `IMigrationService`) keep their signatures until a deliberate breaking release.
- No `ConfigureAwait(false)`: there is no synchronisation context in a console, host or WASM app that would justify it.
- No new `.GetAwaiter().GetResult()`, `.Result` or `.Wait()`. The existing `TemplateExecutor` bridging is legacy; do not extend it. Do not wrap synchronous code in `Task.Run` just to make it awaitable.
- No fire-and-forget (`_ = SomethingAsync()`) outside UI code, and there only with a comment that says why.

## 8. Strings, culture, time

- String comparison always names the comparison: `StringComparison.OrdinalIgnoreCase` for aliases, file names, option values and database identifiers; `Ordinal` for exact matches; `StringComparer.OrdinalIgnoreCase` for dictionaries and sets keyed by such values.
- `ToLowerInvariant()` / `ToUpperInvariant()`, never `ToLower()` / `ToUpper()`.
- Numbers and dates that end up in files, SQL or messages are formatted with `CultureInfo.InvariantCulture` (`Validation/Messages/ValidationMessages.Format`). The Console sets the invariant UI culture at start-up.
- `DateTime.UtcNow` for timestamps; never `DateTime.Now` outside the three console fatal-error fallbacks.
- Values inside messages are wrapped in square brackets: `$"Product alias [{alias}] not found."`, `$"Invalid value [{raw}] for property [{propertyName}]"`.
- Newlines in multi-line strings use `Environment.NewLine` unless the text is written to a log template (`\n` there).

## 9. Documentation comments

- Every `public` and `protected` type and member in the packable projects (`Core`, `Shared`, `Infrastructure`, `Services`, `Services.Abstractions`, `Pipeline`, `Database`, `Database.Common`, `Database.<Db>`, `Validation`, `Testing`) has a `///<summary>` that says what it is for, not what its name already says. `internal` members and Console/Wizard code: document when the behaviour is not obvious.
- Empty `<summary>` placeholders are forbidden. Remove the 17 that exist when you touch their files.
- Use `<see cref="..."/>`, `<paramref name="..."/>`, `<c>...</c>` and `<remarks>` where they help (`Validation/Rules/SchemaRule.cs`, `Core/Logging/MigrationEvent.cs`). Use `<inheritdoc/>` on interface implementations that add nothing.
- Engine tests keep their test-ID summaries (`/// B1: Baseline to Release_2.0 should ...`).
- Comments are full English sentences that explain *why*. Issue references in parentheses (`(#23)`).
- No commented-out code. Delete it; git remembers. No `TODO` without an issue number, except the intentional scaffolding hints in `Database.Example`.

## 10. Logging

- Inject `ILogger<T>` into a `private readonly ILogger<T> _logger` field. Serilog is the sink, configured by `Pipeline/SerilogFactory`; do not use the static `Serilog.Log` in new code (the two calls in `RayMigratorOptionsValidator` are legacy).
- Message templates are structured, never interpolated: `_logger.LogDebug("Migration file {FileName} parsed with {BlockCount} blocks", fileName, blocks.Count)`. Property names are PascalCase. Failure lines follow the pipe layout `"Migration FAILED | Product: {Product} | Env: {Environment} | ..."`.
- Levels (from the `/logging` skill):

| Level | Use for |
|---|---|
| Trace | Full dumps (settings JSON, SQL text), per-iteration detail |
| Debug | Parameters, decisions, intermediate state |
| Information | Business start and finish: run started, file migrated, run completed |
| Warning | Recoverable or skipped: retry, missing optional file, orphaned run fixed |
| Error | An operation failed and the caller is told |
| Critical | RayMigrator cannot continue (`LogFatalError` only) |

- Event IDs come from `Core/Logging/MigrationEvent.cs` (numbered in bands of 10 and 100). Add a new event there and seed the `MigrationEvent` lookup table in every `DatabaseLogging_CheckCreate` template when you introduce one.
- Add `{MigrationContext}` with `_ctxAccessor.Current.Clone()` to template-execution log lines so that the database sink can persist the context.
- Anything that may contain a connection string or password goes through `SensitiveDataMasker`; the placeholder is `*** HIDDEN ***`. Never log raw configuration.
- Log an exception once, where it is handled. Re-thrown exceptions are not logged again on the way up.

## 11. Error handling

- Custom exceptions live in `Shared/Exceptions/CustomExceptions.cs`, derive from `Exception`, expose a public `const string AbortMessage` prefix and provide `(message)` and `(message, innerException)` constructors plus overloads for context (`ResultCode`, `ExitCode`, `MigrationRunId`). Follow that pattern for a new one.
- Inside services and the pipeline, failures are exceptions. Wrap lower-level exceptions in the domain exception with the original as `InnerException`; use exception filters to avoid double wrapping: `catch (Exception ex) when (ex is not ApplicationStartupException)`.
- At the service boundary (`IMigrationService`), return `OperationResult` / `MigrationOperationResult` with `Success`, `ErrorCode`, `ErrorMessage` and `Messages`. The pipeline maps results to exit codes (`0` success, `1` failure, `4` and `100` for the specific cases in `DirectModePipeline`).
- Validation reports, it does not throw: `ValidationReport.AddError/AddWarning` in `Validation`, `ValidationResult` in the `Ray*Attribute`s.
- SQL template result codes are catalogued in `Shared/Constants/TemplateResultCode.cs`: negative values come from templates (bands of ten per area), positive values 1001+ from C#. Register new codes there and in `IsKnown()`.
- A bare `catch { }` is acceptable only for best-effort clean-up and carries a comment (`// ignored: localStorage unavailable`). Use `throw;` to rethrow, never `throw ex;`.
- Format exception text for output with `ex.GetExceptionDetails()` (`Core/Extensions/ExceptionExtensions.cs`).

## 12. Dependency injection

- Engine services are registered in `Services/ServiceCollectionExtensions.AddRayMigratorServices` and in `Pipeline/DirectModePipeline` (options, post-configure, validators, singletons). Add new registrations next to their peers; do not create a second composition root.
- Lifetimes: stateless services `Singleton`, per-run services `Scoped` (`IMigrationService`, `RayMigratorService`), `Transient` only for `IPostConfigureOptions`. In CLI mode the context accessor is a singleton; in API mode it is scoped (`AsyncLocal`).
- Options are consumed through `IOptions<RayMigratorOptions>`; `IOptionsMonitor` and `IOptionsSnapshot` are not used.
- DAL plugins are not in the container. They are discovered by `DalFactory` through `[DatabaseType("SqlServer")]`.
- The wizard registers its services as `Scoped` in `ConfigWizard.Web/Program.cs`.

## 13. Configuration and validation

- Options classes: mutable, nullable properties, DataAnnotations (`[Required]`, `[ValidateObjectMembers]`, `[ValidateEnumeratedItems]`) plus the custom `Ray*Attribute`s in `Core/Configuration/Validation/RayAttributes/`. `RayRangeInt(min, max, default)` writes the default back when the value is null.
- Cross-field and cross-section rules belong in the `Validation` project so that engine and wizard share them: an `internal sealed class XxxRule : IValidationRule`, a `RULE_<section>_<n>` constant in `RuleIds.cs` with the symbolic name as a trailing comment, a `const string` message template in `Messages/ValidationMessages.cs`, a manual entry in the `RuleCatalog` array (no reflection, on purpose), and a row in `Docs/appendix/validation-rules.md`.
- Paths in validation issues are written `Products > MyApp > TargetGroups > Backend`.
- Configuration arrays whose elements carry an `Alias` merge by alias across the `appsettings*.json` hierarchy (ADR-021). Keep that invariant when adding a new array type.

## 14. SQL templates

- Location `Raycoon.RayMigrator.Database.<Db>/Templates/<Area>_<Entity>_<Action>.sql`; the name equals the `TemplateType` member. Templates are `Content` items copied to `DataAccessLayers/<Db>/` and packed as `contentFiles`, not embedded resources.
- Every template starts with the `[RayMigratorTemplate]` header block (`TemplateType`, `DatabaseType`, `Author`, `Version = "YYYY-MM-DD.n"`), followed by `[Description]`, `[ConfigPlaceholders]`, `[Parameters]`, `[ReturnValues]` and `[ModificationNotes]`. Bump `Version` on every change.
- Placeholders: `{CFG:SchemaName}` and `{CFG:TableBaseName}` are substituted at load time and must appear in the allow-list in `Core/Configuration/ConfigurationConstants.cs`. Runtime values are ADO parameters (`@ProductId`), never string-concatenated.
- Result contract: the last statement returns `'code[,code...],message'`; the first `code >= 0` is success, negative codes come from `TemplateResultCode`, further integer codes are template-specific (`Repository_CheckCreate` returns `RepositoryWasCreated`); messages contain no commas and never start with an integer.
- SQL Server templates use `SET NOCOUNT ON; SET XACT_ABORT ON;`, `BEGIN TRY ... END TRY BEGIN CATCH ... ;THROW; END CATCH` and `SYSUTCDATETIME()`. PostgreSQL identifiers are quoted PascalCase. Keep the five dialects functionally identical; a change in one template is a change in five.

## 15. Blazor Config Wizard

- Components use `@code { }` blocks, not code-behind files. Dependencies are `[Inject] private X Name { get; set; } = default!;`; `@inject` is not used. The localizer is named `L`, the state service `Wizard`.
- Every `.razor` file starts with a one-line `@* What this component does *@` comment.
- UI text goes through `L.Get("Section.Key")`. Add the key to both the `en` and the `de` dictionary in `ConfigWizard.Web/Services/LocalizationService.cs`. Field and section help texts live in the `.resx` pairs under `ConfigWizard.Core/Resources/`.
- `[Parameter]` strings default to `""`; two-way binding uses `XxxChanged` `EventCallback`s.
- `ConfigWizard.Core` keeps zero dependencies. Anything that needs the engine belongs in `Validation` (shared rules) or in the Web project.
- Folder layout: `Components/{Hub,Phase1,Phase2,Phase3,Sections,Shared}`, `Layout`, `Pages`, namespaces from `_Imports.razor`.

## 16. Tests

- Stack: xunit.v3, AwesomeAssertions (`Should()`; FluentAssertions fork chosen for licence reasons), NSubstitute for the few interface mocks (`Substitute.For<IDal>()`). No Moq, no bUnit.
- Structure every test with the three comment markers and blank lines between the phases:

  ```csharp
  [Fact]
  public async Task MigrateUp_WithMissingRollbackFile_ReturnsFailure()
  {
      // Arrange
      await using var scenario = await CreateScenario().RemoveRollback("002").BuildAsync(MigrationCommand.MigrateUp);

      // Act
      var result = await scenario.RunAsync();

      // Assert
      result.Success.Should().BeFalse(because: "a missing rollback file must stop the run when RequireRollbackFile is true");
  }
  ```

- Put the reason into the `because` argument, not into a comment. No `Assert.*` in the unit test projects.
- Data-driven tests use `[Theory]` with `[InlineData]`; `[MemberData]` when the data needs code.
- Hand-written fakes (`Tests.Unit/Helpers/CapturingLogger.cs`) and `TestFactories` are preferred over reflection into private state. Reflection (`BindingFlags.NonPublic`) is a last resort and needs a comment.
- `Tests.Unit` is flat: `P<0-3>_<Subject>Tests.cs` plus `Helpers/`. Validation and wizard code have their own test projects; put tests there.
- `Tests.Engine`: one `Fixtures/<Db>Fixture.cs` (`IAsyncLifetime`, hard-coded Docker connection string, `IsDatabaseAvailable`), one `Collections/<Db>Collection.cs`, one `Infrastructure/<Db>TestBase.cs`; test classes under `Tests/{MigrateUp,MigrateDown,Compound,CliTool,Features}` carry `[Collection("<Db>")]`, `[Trait("Engine", "<Db>")]` and `[Trait("Category", "<Category>")]`, start with `Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available")` and build their state with `ScenarioBuilder`. New engine test files are named `<Db><Feature>Tests.cs` for every database, PostgreSQL included.
- A feature that touches the engine gets the same test in all five databases. SQLite runs without Docker, so it is the fastest smoke test.

## 17. Known inconsistencies (measured, left in place)

| Topic | State of the code | Rule for new code |
|---|---|---|
| `private static readonly` naming | PascalCase 53, `s_camelCase` 15 (DAL plugins), `_camelCase` 2 | PascalCase |
| `sealed` | 25 sealed classes of about 600; concentrated in `Validation` | `internal sealed` by default |
| `Async` suffix | about 30 Task methods without it | Always |
| Braces | about 1,100 braced single statements, about 560 unbraced, about 115 one-line guards | Braces, except one-line guards |
| Null checks | `is null` 242 / `== null` 78, but `!= null` 295 / `is not null` 9 | `== null` / `!= null` |
| `var` | about 6,200 `var` vs about 1,100 explicit; `Services` is the most mixed | `var` when apparent |
| Object creation | `new()` 161, `new List<T>()` 119, collection expressions 42 | `new()` and `[]` |
| Argument guards | `throw new ArgumentNullException(nameof(x))` 7, `ThrowIfNull` 0 | `ThrowIfNull` |
| Engine test file names | PostgreSQL files without database prefix | `<Db><Feature>Tests` |
| Empty `<summary>` placeholders | 17 | none |
| Commented-out code blocks | about 30 | none |
| German comments | a few csproj comments | English |
| `#region` vs box comments | regions in engine code and `Tests.Unit`, box comments in newer projects | both allowed |

Fix an item from this table only in a file you are already changing, or in a dedicated clean-up commit that changes nothing else.
