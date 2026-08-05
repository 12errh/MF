mod commands;

use clap::{Parser, Subcommand};

#[derive(Parser)]
#[command(name = "mf", about = "Mate Framework CLI", version)]
struct Cli {
    #[command(subcommand)]
    command: Commands,

    /// Output in JSON format
    #[arg(long, global = true)]
    json: bool,
}

#[derive(Subcommand)]
enum Commands {
    /// Create a new Mate Framework project
    New {
        /// Project name
        name: String,
    },
    /// Diagnose issues with the current project
    Doctor,
    /// Start the development server
    Dev,
    /// Manage runtime versions
    Runtime {
        #[command(subcommand)]
        command: RuntimeCommands,
    },
    /// Build the project into a distributable directory
    Build {
        /// Output directory (default: build/)
        #[arg(long, short)]
        output: Option<String>,
    },
    /// Package the built project into a tar.gz archive
    Package,
    /// Show platform capabilities
    Capabilities,
}

#[derive(Subcommand)]
enum RuntimeCommands {
    /// List installed runtime versions
    List,
    /// Show runtime status
    Status,
    /// Install a runtime version
    Install {
        /// Version to install (e.g. 1.0.0)
        version: Option<String>,
    },
}

fn main() -> anyhow::Result<()> {
    let cli = Cli::parse();

    match cli.command {
        Commands::New { name } => commands::new::run(&name, cli.json),
        Commands::Doctor => commands::doctor::run(cli.json),
        Commands::Dev => commands::dev::run(cli.json),
        Commands::Runtime { command } => {
            let subcmd = match &command {
                RuntimeCommands::List => "list",
                RuntimeCommands::Status => "status",
                RuntimeCommands::Install { .. } => "install",
            };
            let version = match &command {
                RuntimeCommands::Install { version } => version.as_deref(),
                _ => None,
            };
            commands::runtime::run(subcmd, version, cli.json)
        }
        Commands::Build { output } => commands::build::run(output.as_deref(), cli.json),
        Commands::Package => commands::package::run(cli.json),
        Commands::Capabilities => commands::capabilities::run(cli.json),
    }
}
