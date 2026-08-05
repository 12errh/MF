using System;
using System.IO;

namespace Mate.Bootstrap
{
    /// <summary>
    /// Command-line argument helpers for the runtime player. The mf CLI
    /// launches the player with `--projectPath <dir>` (crates/mf-core/process.rs).
    /// </summary>
    public static class BootstrapArgs
    {
        public const string ProjectPathArg = "--projectPath";
        public const string ProjectDirEnv = "MATE_PROJECT_DIR";

        /// <summary>
        /// Extract the project directory from player args. Resolution order:
        /// `--projectPath <dir>` argument, then MATE_PROJECT_DIR env var, then
        /// the current working directory.
        /// </summary>
        public static string ParseProjectPath(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == ProjectPathArg && !string.IsNullOrEmpty(args[i + 1]))
                    return args[i + 1];
            }

            var env = Environment.GetEnvironmentVariable(ProjectDirEnv);
            if (!string.IsNullOrEmpty(env))
                return env;

            return Directory.GetCurrentDirectory();
        }
    }
}
