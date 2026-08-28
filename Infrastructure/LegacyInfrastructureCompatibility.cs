// Temporary compatibility bridge for the namespace migration to MewNX.Infrastructure.
// Existing Core/UI files still import MewSwitchManager.Infrastructure; keep those imports
// compiling while the remaining source files are migrated incrementally.
global using AppLogger = MewNX.Infrastructure.AppLogger;
global using AppPaths = MewNX.Infrastructure.AppPaths;
global using ConfigLoader = MewNX.Infrastructure.ConfigLoader;
global using DependencyService = MewNX.Infrastructure.DependencyService;
global using GitHubReleaseClient = MewNX.Infrastructure.GitHubReleaseClient;
global using JsonStore = MewNX.Infrastructure.JsonStore;
global using ProcessRunner = MewNX.Infrastructure.ProcessRunner;
global using UpdateService = MewNX.Infrastructure.UpdateService;

namespace MewSwitchManager.Infrastructure;
